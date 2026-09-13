// APPLICATION-SPECIFIC CODE - Property of Client (Corinne Kelley)
// This code implements specific business requirements for the Momentary Momentos application

namespace MomentaryMomentos.Services;

/// <summary>
/// Defines subscription and payment operations for the Momentary Momentos application.
/// Integrates with platform-specific stores (Apple App Store, Google Play).
/// </summary>
public interface ISubscriptionService
{
    /// <summary>
    /// Gets the current user's subscription tier (Free or Premium).
    /// </summary>
    Task<SubscriptionTier> GetCurrentTierAsync();

    /// <summary>
    /// Checks if user can upload more videos based on their tier and current count.
    /// </summary>
    Task<bool> CanUploadVideoAsync(string userId);

    /// <summary>
    /// Gets the remaining video slots for free tier users.
    /// </summary>
    Task<int> GetRemainingVideoSlotsAsync(string userId);

    /// <summary>
    /// Initiates the purchase flow for a subscription plan.
    /// </summary>
    Task PurchaseSubscriptionAsync(SubscriptionPlan plan);

    /// <summary>
    /// Restores previous purchases (required by app stores).
    /// </summary>
    Task RestorePurchasesAsync();

    /// <summary>
    /// Gets available subscription plans with current pricing.
    /// </summary>
    Task<List<SubscriptionPlan>> GetAvailablePlansAsync();

    /// <summary>
    /// Re-validates entitlement against the store and returns the authoritative tier.
    /// This is what lets Premium go *down* — on expiry, cancellation, refund, or revocation
    /// — where the old client-side flag could only ever be switched on.
    /// Leaves the current tier untouched when the store cannot be reached, when the device
    /// has no receipt, or when the account holds a comp grant.
    /// </summary>
    Task<SubscriptionTier> RefreshEntitlementAsync();
}

/// <summary>
/// Represents subscription tiers defined in the contract.
/// </summary>
public enum SubscriptionTier
{
    Free,
    Premium
}

/// <summary>
/// Represents a subscription plan as defined in contract Section 4.
/// </summary>
public class SubscriptionPlan
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public SubscriptionPeriod Period { get; set; }
    public decimal Price { get; set; }
    public string CurrencyCode { get; set; } = "USD";
    public string Description { get; set; } = "";
    public string DisplayName => Period == SubscriptionPeriod.Monthly ? "Monthly Premium" : "Annual Premium";
}

public enum SubscriptionPeriod
{
    Monthly,
    Annual
}
