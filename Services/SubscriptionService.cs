// APPLICATION-SPECIFIC CODE - Property of Client (Corinne Kelley)
// This service implements subscription and payment logic specific to Momentary Momentos

using MomentaryMomentos.Models;

namespace MomentaryMomentos.Services;

/// <summary>
/// Manages subscription state and payment integration for Momentary Momentos.
/// Integrates with SupabaseService for backend configuration and user tier status.
/// Platform-specific payment processing delegated to platform implementations.
/// </summary>
public class SubscriptionService : ISubscriptionService
{
    private readonly SupabaseService _supabase;
    private readonly IInAppBillingService _billing;
    private SubscriptionConfig? _cachedConfig;

    public SubscriptionService(SupabaseService supabase, IInAppBillingService billing)
    {
        _supabase = supabase;
        _billing = billing;
    }

    /// <summary>
    /// Gets subscription configuration from backend (cacheable).
    /// </summary>
    private async Task<SubscriptionConfig> GetConfigAsync()
    {
        if (_cachedConfig != null)
            return _cachedConfig;

        _cachedConfig = await _supabase.GetSubscriptionConfigAsync();
        return _cachedConfig;
    }

    public async Task<SubscriptionTier> GetCurrentTierAsync()
    {
        var profile = await _supabase.GetProfileAsync();
        if (profile?.IsPremium != true)
            return SubscriptionTier.Free;

        // Comps have no expiry and no store subscription behind them.
        if (profile.IsComped)
            return SubscriptionTier.Premium;

        // Fail closed on a stale row: the server owns is_premium, but between refreshes a
        // lapsed subscription would otherwise keep reading Premium off a cached profile.
        if (profile.PremiumExpiresAt is { } expiry && expiry <= DateTimeOffset.UtcNow)
            return SubscriptionTier.Free;

        return SubscriptionTier.Premium;
    }

    public async Task<SubscriptionTier> RefreshEntitlementAsync()
    {
        var profile = await _supabase.GetProfileAsync();

        // A comp grant is a deliberate permanent entitlement (the app owner, staff). It has
        // no receipt, so verification could only ever revoke it. Never ask.
        if (profile?.IsComped == true)
            return SubscriptionTier.Premium;

        var proof = await _billing.GetPurchaseProofAsync();
        if (string.IsNullOrEmpty(proof))
        {
            // No proof is "unknown", not "not subscribed" — Android always lands here, and so
            // does an iOS device that cannot produce a receipt. Leave the entitlement as-is.
            return await GetCurrentTierAsync();
        }

        try
        {
            var isPremium = await _supabase.VerifySubscriptionAsync(StoreProofPlatform, proof);
            return isPremium ? SubscriptionTier.Premium : SubscriptionTier.Free;
        }
        catch (Exception ex)
        {
            // The store was unreachable or the function errored. Downgrading here would strip
            // a paying customer's Premium over a bad connection, so hold the current state.
            Diagnostics.Report(ex, "subscription.refresh-entitlement");
            return await GetCurrentTierAsync();
        }
    }

    public async Task<bool> CanUploadVideoAsync(string userId)
    {
        var config = await GetConfigAsync();
        var tier   = await GetCurrentTierAsync();
        var count  = await _supabase.GetUserVideoCountAsync(userId);

        var limit = tier == SubscriptionTier.Premium
            ? config.PaidUserVideoLimit
            : config.FreeUserVideoLimit;

        return count < limit;
    }

    public async Task<int> GetRemainingVideoSlotsAsync(string userId)
    {
        var config = await GetConfigAsync();
        var tier   = await GetCurrentTierAsync();
        var count  = await _supabase.GetUserVideoCountAsync(userId);

        var limit = tier == SubscriptionTier.Premium
            ? config.PaidUserVideoLimit
            : config.FreeUserVideoLimit;

        return Math.Max(0, limit - count);
    }

    public void InvalidateConfigCache() => _cachedConfig = null;

    public async Task<List<SubscriptionPlan>> GetAvailablePlansAsync()
    {
        var config = await GetConfigAsync();
        var plans = new List<SubscriptionPlan>();

        if (config.MonthlyEnabled)
        {
            plans.Add(new SubscriptionPlan
            {
                Id = "monthly_premium",
                Name = "Premium Monthly",
                Period = SubscriptionPeriod.Monthly,
                Price = config.MonthlyPrice,
                Description = $"1,000 momentos, billed monthly — ${config.MonthlyPrice:F2}/mo"
            });
        }

        if (config.AnnualEnabled)
        {
            plans.Add(new SubscriptionPlan
            {
                Id = "annual_premium",
                Name = "Premium Annual",
                Period = SubscriptionPeriod.Annual,
                Price = config.AnnualPrice,
                Description = $"1,000 momentos, billed annually — ${config.AnnualPrice:F2}/yr"
            });
        }

        return plans;
    }

    public async Task PurchaseSubscriptionAsync(SubscriptionPlan plan)
    {
        // Throws OperationCanceledException if user cancels, InvalidOperationException on failure
        var purchased = await _billing.PurchaseAsync(plan.Id);
        if (purchased)
            await RefreshEntitlementAsync();
    }

    public async Task RestorePurchasesAsync()
    {
        var restored = await _billing.RestoreAsync();
        if (!restored)
            throw new InvalidOperationException("No active premium subscriptions found to restore.");

        // StoreKit 1 replays *historical* transactions, so RestoreAsync reports success for a
        // subscription that lapsed years ago. The server decides whether it is actually live.
        var tier = await RefreshEntitlementAsync();
        if (tier != SubscriptionTier.Premium)
            throw new InvalidOperationException("No active premium subscriptions found to restore.");
    }

    /// <summary>Which store issued the proof returned by IInAppBillingService.</summary>
    private static string StoreProofPlatform =>
#if IOS
        "apple";
#else
        "google";
#endif
}
