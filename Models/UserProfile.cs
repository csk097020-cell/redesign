// APPLICATION-SPECIFIC CODE - Property of Client (Corinne Kelley)

namespace MomentaryMomentos.Models;

public sealed class UserProfile
{
    public string Id { get; set; } = "";
    public string? FullName { get; set; }
    public bool IsAdmin { get; set; }
    public string? AvatarUrl { get; set; }

    /// <summary>
    /// Indicates if user has premium subscription (Contract Section 2.6).
    /// Written server-side by the verify-subscription Edge Function, never by this client.
    /// </summary>
    public bool IsPremium { get; set; }

    /// <summary>
    /// Where the entitlement came from: "apple", "google", or "comp" for a permanent
    /// grant with no store subscription behind it (the app owner, staff). A comp is
    /// never revoked by receipt verification. Null on a DB without migration 006.
    /// </summary>
    public string? PremiumSource { get; set; }

    /// <summary>
    /// End of the paid period as reported by the store. Null for comp grants and for
    /// free accounts. Lets the client fail closed if a stale row outlives its subscription.
    /// </summary>
    public DateTimeOffset? PremiumExpiresAt { get; set; }

    /// <summary>Permanent grant that receipt verification must never revoke.</summary>
    public bool IsComped => string.Equals(PremiumSource, "comp", StringComparison.OrdinalIgnoreCase);
}
