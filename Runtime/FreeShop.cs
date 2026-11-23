namespace FortnitePirateToken.Runtime;

/// <summary>Host-side state: has a Pirate Shop Token been extracted, so the next shop is free?</summary>
internal static class FreeShop
{
    /// <summary>Set when the token is extracted; stays set through the shop that follows, cleared when the next level starts.</summary>
    public static bool Active;
}
