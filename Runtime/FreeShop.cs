namespace FortnitePirateToken.Runtime;

/// <summary>
/// Free shops earned from Pirate Shop Tokens. Every token extracted banks one free shop; each shop you visit uses one
/// up, so five tokens make the next five shops free and more tokens add on top.
///
/// Everything lives in the game's run stats, which are saved with the run and cleared when a new run starts. Only the
/// host's copy matters: the host decides prices.
/// </summary>
internal static class FreeShop
{
    private const string CreditsKey = "pirateTokenFreeShops";
    private const string ShopKey = "pirateTokenShopState";

    private const int NotCounted = 0;
    private const int Free = 1;
    private const int Paid = 2;

    /// <summary>Free shops banked and not used yet.</summary>
    public static int Credits
    {
        get => Get(CreditsKey);
        private set => Set(CreditsKey, value);
    }

    /// <summary>True while the shop you are in is a free one.</summary>
    public static bool Active => Get(ShopKey) == Free;

    /// <summary>A token was extracted: bank one free shop per token.</summary>
    public static void Add(int tokens)
    {
        if (tokens <= 0 || StatsManager.instance == null) return;

        Credits += tokens;
        FortnitePirateTokenPlugin.Logger.LogInfo($"Pirate Shop Token extracted: {Credits} free shop(s) banked.");
    }

    /// <summary>A shop is starting: use up a free shop if there is one. Does nothing the second time in the same shop.</summary>
    public static void EnterShop()
    {
        if (StatsManager.instance == null || Get(ShopKey) != NotCounted) return;

        if (Credits > 0)
        {
            Credits--;
            Set(ShopKey, Free);
            FortnitePirateTokenPlugin.Logger.LogInfo($"This shop is free! {Credits} free shop(s) left.");
        }
        else
        {
            Set(ShopKey, Paid);
        }
    }

    /// <summary>A normal level is starting, so the next shop counts again.</summary>
    public static void LeaveShop()
    {
        if (StatsManager.instance != null) Set(ShopKey, NotCounted);
    }

    /// <summary>
    /// Forget all free shops. Called whenever a save is loaded, created or deleted and when stats are reset, so they
    /// can never leak into another save: the game merges a loaded save into the stats already in memory.
    /// </summary>
    public static void Clear()
    {
        var stats = StatsManager.instance;
        if (stats == null) return;

        stats.runStats.Remove(CreditsKey);
        stats.runStats.Remove(ShopKey);
    }

    private static int Get(string key)
    {
        var stats = StatsManager.instance;
        return stats != null && stats.runStats.TryGetValue(key, out var value) ? value : 0;
    }

    private static void Set(string key, int value)
    {
        var stats = StatsManager.instance;
        if (stats != null) stats.runStats[key] = value;
    }
}
