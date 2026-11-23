using System.Linq;
using FortnitePirateToken.Runtime;
using HarmonyLib;
using Photon.Pun;
using UnityEngine;

namespace FortnitePirateToken;

// Every valuable the host spawns has a 1-in-N chance (default 30000) of becoming a Pirate Shop Token instead.
[HarmonyPatch(typeof(ValuableDirector), "SpawnValuable")]
internal static class TokenSpawnPatch
{
    private static void Prefix(ref PrefabRef _valuable)
    {
        var token = TokenBuilder.Prefab;
        if (token == null || ReferenceEquals(_valuable, token)) return;

        var oneIn = Mathf.Max(1, FortnitePirateTokenPlugin.OneInN.Value);
        if (Random.Range(0, oneIn) != 0) return;

        FortnitePirateTokenPlugin.Logger.LogInfo("A Pirate Shop Token spawned!");
        _valuable = token;
    }
}

// A new level starts: whatever free-shop state was left over is over.
[HarmonyPatch(typeof(ValuableDirector), nameof(ValuableDirector.SetupHost))]
internal static class FreeShopResetPatch
{
    private static void Prefix()
    {
        FreeShop.Active = false;
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
