using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace FortnitePirateToken;

[BepInPlugin("Joja.FortnitePirateToken", "FortnitePirateToken", "1.0.0")]
public class FortnitePirateTokenPlugin : BaseUnityPlugin
{
    internal static FortnitePirateTokenPlugin Instance { get; private set; } = null!;
    internal new static ManualLogSource Logger => Instance._logger;
    private ManualLogSource _logger => base.Logger;
    internal Harmony? Harmony { get; set; }

    private void Awake()
    {
        Instance = this;

        // Prevent the plugin from being deleted
        gameObject.transform.parent = null;
        gameObject.hideFlags = HideFlags.HideAndDontSave;

        Harmony ??= new Harmony(Info.Metadata.GUID);
        Harmony.PatchAll();

        Logger.LogInfo($"{Info.Metadata.GUID} v{Info.Metadata.Version} has loaded!");
    }

    private void OnDestroy()
    {
        Harmony?.UnpatchSelf();
    }
}
