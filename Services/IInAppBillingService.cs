namespace MomentaryMomentos.Services;

/// <summary>
/// Platform-specific in-app billing abstraction.
/// iOS: implemented via StoreKit (Platforms/iOS/InAppBillingService.cs)
/// Android: requires Google Play Billing — see Platforms/Android/InAppBillingService.cs
/// </summary>
public interface IInAppBillingService
{
    /// <summary>
    /// Initiates a purchase for the given product ID.
    /// Returns true on success.
    /// Throws OperationCanceledException if the user cancels.
    /// Throws InvalidOperationException on any other failure.
    /// </summary>
    Task<bool> PurchaseAsync(string productId);

    /// <summary>
    /// Restores previously purchased subscriptions.
    /// Returns true if an active subscription was found.
    /// Returns false if no active subscriptions exist.
    /// Throws InvalidOperationException on failure.
    /// </summary>
    Task<bool> RestoreAsync();

    /// <summary>
    /// Store-issued proof of purchase, for server-side validation by the
    /// verify-subscription Edge Function.
    /// iOS: the base64 App Store receipt.
    /// Android: null — Android still runs the client-side path until Play testing clears.
    /// Returns null when the device has no proof to offer (fresh install, never purchased);
    /// callers must treat null as "unknown", not as "not subscribed".
    /// </summary>
    Task<string?> GetPurchaseProofAsync();
}
