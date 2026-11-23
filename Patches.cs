using System.Collections.Generic;
using System.Linq;
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
