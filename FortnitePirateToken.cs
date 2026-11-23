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

    /// <summary>Every valuable the game spawns has a 1 in this many chance of being the token.</summary>
    internal static ConfigEntry<int> OneInN { get; private set; } = null!;

    private void Awake()
    {
        Instance = this;

        // Prevent the plugin from being deleted
        gameObject.transform.parent = null;
        gameObject.hideFlags = HideFlags.HideAndDontSave;

        OneInN = Config.Bind("Spawning", "OneInN", 30000,
            "Each valuable spawned in a level has a 1 in this many chance of becoming a Pirate Shop Token.");

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
