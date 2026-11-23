using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using FortnitePirateToken.Runtime;
using HarmonyLib;
using Photon.Pun;
using UnityEngine;

namespace FortnitePirateToken;

// Every valuable the host spawns has a 1-in-N chance (1375 by default; see Chance and CustomOneIn in the config / REPOConfig) of becoming a Pirate Shop Token instead.
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

// A shop is starting (host): use up one banked free shop, if there is one. The items are spawned after this, and each
// prices itself a little later (see FreeShopPricePatch), so by then the shop is already marked free.
[HarmonyPatch]
internal static class FreeShopEnterPatch
{
    // ShopInitialize has two overloads; the one with the item list is the one that does the work.
    private static MethodBase TargetMethod() =>
        AccessTools.GetDeclaredMethods(typeof(ShopManager)).First(m => m.Name == "ShopInitialize" && m.GetParameters().Length == 1);

    private static void Prefix()
    {
        if (SemiFunc.IsMasterClientOrSingleplayer() && SemiFunc.RunIsShop()) FreeShop.EnterShop();
    }
}

// A normal level is starting: the next shop counts again. Not in the shop itself: its items are spawned before this
// runs but only price themselves a frame later, so clearing there made the last few cost money.
[HarmonyPatch(typeof(ValuableDirector), nameof(ValuableDirector.SetupHost))]
internal static class FreeShopLeavePatch
{
    private static void Prefix()
    {
        if (!SemiFunc.RunIsShop()) FreeShop.LeaveShop();
    }
}

// Extracting a token banks one free shop. Host only: that is where the game destroys extracted valuables. The tokens
// are counted once they have really left the haul list, because the game calls these methods over and over.
[HarmonyPatch(typeof(ExtractionPoint), "DestroyAllPhysObjectsInHaulList")]
internal static class TokenExtractedAllPatch
{
    private static void Prefix(out int __state)
    {
        __state = 0;
        if (!SemiFunc.IsMasterClientOrSingleplayer()) return;

        __state = RoundDirector.instance.dollarHaulList.Count(item => item != null && item.GetComponent<TokenMarker>() != null);
    }

    private static void Postfix(int __state)
    {
        if (__state > 0 && RoundDirector.instance.dollarHaulList.Count == 0) FreeShop.Add(__state);
    }
}

[HarmonyPatch(typeof(ExtractionPoint), "DestroyTheFirstPhysObjectsInHaulList")]
internal static class TokenExtractedFirstPatch
{
    private static void Prefix(out GameObject? __state)
    {
        __state = null;
        if (!SemiFunc.IsMasterClientOrSingleplayer()) return;

        var list = RoundDirector.instance.dollarHaulList;
        if (list.Count > 0 && list[0] != null && list[0].GetComponent<TokenMarker>() != null) __state = list[0];
    }

    private static void Postfix(GameObject? __state)
    {
        // ReferenceEquals: a Unity object that was just destroyed would compare equal to null.
        if (!ReferenceEquals(__state, null) && !RoundDirector.instance.dollarHaulList.Contains(__state)) FreeShop.Add(1);
    }
}

// Free shops belong to one save. The game merges a loaded save into the stats already in memory instead of clearing
// them first, so wipe ours whenever a save is loaded, created or deleted (losing a run deletes its save) and whenever
// stats are reset. A loaded save then brings back only the free shops that were saved in it.
[HarmonyPatch(typeof(StatsManager), nameof(StatsManager.LoadGame))]
internal static class FreeShopClearOnLoadPatch
{
    private static void Prefix() => FreeShop.Clear();
}

[HarmonyPatch(typeof(StatsManager), nameof(StatsManager.SaveFileCreate))]
internal static class FreeShopClearOnCreatePatch
{
    private static void Prefix() => FreeShop.Clear();
}

[HarmonyPatch(typeof(StatsManager), nameof(StatsManager.SaveFileDelete))]
internal static class FreeShopClearOnDeletePatch
{
    private static void Prefix() => FreeShop.Clear();
}

[HarmonyPatch(typeof(StatsManager), nameof(StatsManager.ResetAllStats))]
internal static class FreeShopClearOnResetPatch
{
    private static void Postfix() => FreeShop.Clear();
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
