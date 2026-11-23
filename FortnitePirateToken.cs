using System.Collections;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using FortnitePirateToken.Runtime;
using HarmonyLib;
using UnityEngine;

namespace FortnitePirateToken;

[BepInPlugin("Joja.FortnitePirateToken", "FortnitePirateToken", "1.0.5")]
[BepInDependency("REPOLib", BepInDependency.DependencyFlags.HardDependency)]
public class FortnitePirateTokenPlugin : BaseUnityPlugin
{
    internal static FortnitePirateTokenPlugin Instance { get; private set; } = null!;
    internal new static ManualLogSource Logger => Instance._logger;
    private ManualLogSource _logger => base.Logger;
    internal Harmony? Harmony { get; set; }

    internal enum SpawnChance
    {
        OneIn1375,
        OneIn137500,
        Custom,
    }

    /// <summary>Preset chance. An enum so REPOConfig shows it as a dropdown.</summary>
    internal static ConfigEntry<SpawnChance> Chance { get; private set; } = null!;

    /// <summary>
    /// Your own odds, as text, used when Chance is set to Custom. REPOConfig turns a number setting into a slider capped
    /// at 100, but shows a text setting as a box you can type any number into; so it is parsed here.
    /// </summary>
    internal static ConfigEntry<string> CustomOneIn { get; private set; } = null!;

    /// <summary>Each valuable has a 1 in this many chance of being the token.</summary>
    internal static int OneInN
    {
        get
        {
            switch (Chance.Value)
            {
                case SpawnChance.OneIn137500:
                    return 137500;
                case SpawnChance.Custom:
                    var custom = ParseCustom(CustomOneIn.Value);
                    return custom > 0 ? custom : 1375; // nothing usable typed in: fall back to the default
                default:
                    return 1375;
            }
        }
    }

    // "50000", "50,000" and "50 000" all work. Empty, 0 or anything that is not a number counts as nothing typed in.
    private static int ParseCustom(string? text)
    {
        var cleaned = new string((text ?? "").Where(c => char.IsDigit(c)).ToArray());
        if (cleaned.Length == 0 || cleaned.Length > 9) return cleaned.Length > 9 ? int.MaxValue : 0;

        return int.Parse(cleaned, System.Globalization.CultureInfo.InvariantCulture);
    }

    private void Awake()
    {
        Instance = this;

        // Prevent the plugin from being deleted
        gameObject.transform.parent = null;
        gameObject.hideFlags = HideFlags.HideAndDontSave;

        Chance = Config.Bind("Spawning", "Chance", SpawnChance.OneIn1375,
            "Chance for each valuable spawned in a level to become a Pirate Shop Token. " +
            "OneIn1375 is the default; OneIn137500 is the original Fortnite drop chance; Custom uses CustomOneIn.");
        CustomOneIn = Config.Bind("Spawning", "CustomOneIn", "0",
            "Your own odds: each valuable has a 1 in this many chance of becoming a Pirate Shop Token (for example 5000). " +
            "Only used when Chance is set to Custom. 1 means every valuable.");

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
