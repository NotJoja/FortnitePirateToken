using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using FortnitePirateToken.Runtime;
using HarmonyLib;
using Photon.Pun;
using UnityEngine;

namespace FortnitePirateToken;

// Every valuable the host spawns has a 1-in-N chance (30000 or 137500, set in the config / REPOConfig) of becoming a Pirate Shop Token instead.
[HarmonyPatch(typeof(ValuableDirector), "SpawnValuable")]
internal static class TokenSpawnPatch
{
    private static void Prefix(ref PrefabRef _valuable, List<PrefabRef> ___bigValuables)
    {
        var token = TokenBuilder.Prefab;
        if (token == null) return;

        // The token is in the level presets (so spawn mods can list it) but must never come up in the normal pick:
        // swap it for another big valuable.
        if (IsToken(_valuable, token))
        {
            var others = ___bigValuables.Where(v => !IsToken(v, token)).ToList();
            if (others.Count > 0) _valuable = others[Random.Range(0, others.Count)];
        }

        var oneIn = FortnitePirateTokenPlugin.OneInN;
        if (Random.Range(0, oneIn) != 0) return;

        FortnitePirateTokenPlugin.Logger.LogInfo("A Pirate Shop Token spawned!");
        _valuable = token;
    }

    private static bool IsToken(PrefabRef? candidate, PrefabRef token) =>
        candidate != null && (ReferenceEquals(candidate, token) || candidate.PrefabName == token.PrefabName);
}

// A new level starts: whatever free-shop state was left over is over. Not in the shop itself: the shop's items are
// spawned before this runs but only price themselves a frame later, so clearing here made the last few cost money.
[HarmonyPatch(typeof(ValuableDirector), nameof(ValuableDirector.SetupHost))]
internal static class FreeShopResetPatch
{
    private static void Prefix()
    {
        if (!SemiFunc.RunIsShop()) FreeShop.Active = false;
    }
}

// Extracting the token (host only: that is where the game destroys extracted valuables) makes the next shop free.
[HarmonyPatch(typeof(ExtractionPoint), "DestroyAllPhysObjectsInHaulList")]
internal static class TokenExtractedAllPatch
{
    private static void Prefix()
    {
        if (!SemiFunc.IsMasterClientOrSingleplayer()) return;

        var list = RoundDirector.instance.dollarHaulList;
        if (list.Any(item => item != null && item.GetComponent<TokenMarker>() != null))
        {
            TokenExtractedPatchHelper.Mark();
        }
    }
}

[HarmonyPatch(typeof(ExtractionPoint), "DestroyTheFirstPhysObjectsInHaulList")]
internal static class TokenExtractedFirstPatch
{
    private static void Prefix()
    {
        if (!SemiFunc.IsMasterClientOrSingleplayer()) return;

        var list = RoundDirector.instance.dollarHaulList;
        if (list.Count > 0 && list[0] != null && list[0].GetComponent<TokenMarker>() != null)
        {
            TokenExtractedPatchHelper.Mark();
        }
    }
}

internal static class TokenExtractedPatchHelper
{
    public static void Mark()
    {
        if (FreeShop.Active) return;
        FreeShop.Active = true;
        FortnitePirateTokenPlugin.Logger.LogInfo("Pirate Shop Token extracted: the next shop is free.");
    }
}

// Shop items get their price here (host only); zero it and sync the new price to the clients.
[HarmonyPatch(typeof(ItemAttributes), nameof(ItemAttributes.GetValue))]
internal static class FreeShopPricePatch
{
    private static void Postfix(ItemAttributes __instance)
    {
        if (!FreeShop.Active || !SemiFunc.RunIsShop() || !SemiFunc.IsMasterClientOrSingleplayer()) return;

        __instance.value = 0;
        if (SemiFunc.IsMultiplayer() && __instance.photonView != null && __instance.photonView.ViewID != 0)
        {
            __instance.photonView.RPC("GetValueRPC", RpcTarget.Others, 0);
        }

        SemiFunc.ShopUpdateCost();
    }
}

// The upgrade stand's reroll costs $5k + $5k per previous reroll (UpgradeStand.RerollCost, a tiny private getter).
// Patching a getter that small is unreliable (the JIT can inline it into its callers), so instead every method that
// reads it is rewritten to ask RerollPricing, which returns 0 while the shop is free.
[HarmonyPatch]
internal static class FreeRerollPatch
{
    private static readonly MethodInfo Getter = AccessTools.PropertyGetter(typeof(UpgradeStand), "RerollCost");

    // Skip the whole patch (instead of failing PatchAll) if the game has no such property.
    private static bool Prepare()
    {
        if (Getter == null) FortnitePirateTokenPlugin.Logger.LogWarning("UpgradeStand.RerollCost not found: rerolls will not be free.");
        return Getter != null;
    }

    private static IEnumerable<MethodBase> TargetMethods()
    {
        var count = 0;
        foreach (var method in AccessTools.GetDeclaredMethods(typeof(UpgradeStand)))
        {
            if (method.IsAbstract || method.GetMethodBody() == null) continue;
            if (PatchProcessor.GetOriginalInstructions(method).Any(i => Equals(i.operand, Getter)))
            {
                count++;
                yield return method;
            }
        }

        FortnitePirateTokenPlugin.Logger.LogInfo($"Free rerolls: rewrote {count} UpgradeStand methods.");
    }

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var replacement = AccessTools.Method(typeof(RerollPricing), nameof(RerollPricing.Cost));
        foreach (var instruction in instructions)
        {
            if (instruction.Calls(Getter))
            {
                // Same shape as the getter: takes the UpgradeStand (already on the stack), returns the int cost.
                yield return new CodeInstruction(OpCodes.Call, replacement).WithLabels(instruction.labels);
            }
            else
            {
                yield return instruction;
            }
        }
    }
}

internal static class RerollPricing
{
    private static readonly FieldInfo CountField = AccessTools.Field(typeof(UpgradeStand), "rerollCount");

    public static int Cost(UpgradeStand stand)
    {
        return FreeShop.Active ? 0 : 5 + (int)CountField.GetValue(stand) * 5;
    }
}
