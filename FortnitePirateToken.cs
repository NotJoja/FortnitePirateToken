using System.Collections;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using FortnitePirateToken.Runtime;
using HarmonyLib;
using UnityEngine;

namespace FortnitePirateToken;

[BepInPlugin("Joja.FortnitePirateToken", "FortnitePirateToken", "1.0.0")]
[BepInDependency("REPOLib", BepInDependency.DependencyFlags.HardDependency)]
public class FortnitePirateTokenPlugin : BaseUnityPlugin
{
    internal static FortnitePirateTokenPlugin Instance { get; private set; } = null!;
    internal new static ManualLogSource Logger => Instance._logger;
    private ManualLogSource _logger => base.Logger;
    internal Harmony? Harmony { get; set; }

    internal enum SpawnChance
    {
        OneIn30000,
        OneIn137500,
    }

    /// <summary>Per-valuable chance of it being the token. An enum so REPOConfig shows it as a dropdown.</summary>
    internal static ConfigEntry<SpawnChance> Chance { get; private set; } = null!;

    internal static int OneInN => Chance.Value == SpawnChance.OneIn137500 ? 137500 : 30000;

    private void Awake()
    {
        Instance = this;

        // Prevent the plugin from being deleted
        gameObject.transform.parent = null;
        gameObject.hideFlags = HideFlags.HideAndDontSave;

        Chance = Config.Bind("Spawning", "Chance", SpawnChance.OneIn30000,
            "Chance for each valuable spawned in a level to become a Pirate Shop Token. " +
            "OneIn30000 is the default; OneIn137500 is the original Fortnite drop chance.");

        Harmony ??= new Harmony(Info.Metadata.GUID);
        Harmony.PatchAll();

        StartCoroutine(RegisterWhenReady());

        Logger.LogInfo($"{Info.Metadata.GUID} v{Info.Metadata.Version} has loaded!");
    }

    // Retries every frame for a while: the game's prefabs may not be loadable the instant plugins load.
    private static IEnumerator RegisterWhenReady()
    {
        for (var attempt = 0; attempt < 1200; attempt++)
        {
            if (TokenBuilder.TryRegister()) yield break;
            yield return null;
        }

        Logger.LogError($"Gave up registering '{TokenBuilder.Name}': no vanilla valuable template became available.");
    }

    private void OnDestroy()
    {
        Harmony?.UnpatchSelf();
    }
}
