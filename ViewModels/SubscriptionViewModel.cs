// APPLICATION-SPECIFIC CODE - Property of Client (Corinne Kelley)
// This ViewModel implements the subscription UI logic for Momentary Momentos

using System.Collections.ObjectModel;
using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MomentaryMomentos.Services;

namespace MomentaryMomentos.ViewModels;

/// <summary>
/// Manages the subscription and upgrade UI flow.
/// Displays current tier, available plans, and handles purchase flow.
/// Implements contract requirements from Section 2.6.
/// </summary>
public partial class SubscriptionViewModel : BaseViewModel
{
    private readonly ISubscriptionService _subscription;
    private readonly SupabaseService _supabase;

    public ObservableCollection<SubscriptionPlan> AvailablePlans { get; } = new();

    [ObservableProperty] private SubscriptionTier currentTier;
    [ObservableProperty] private int totalVideos;
    [ObservableProperty] private int remainingSlots;
    [ObservableProperty] private bool isPremium;
    [ObservableProperty] private string tierStatusMessage = "";
    [ObservableProperty] private SubscriptionPlan? selectedPlan;

    public SubscriptionViewModel(ISubscriptionService subscription, SupabaseService supabase)
    {
        _subscription = subscription;
        _supabase = supabase;
    }

    [RelayCommand]
    private async Task Load() => await RunBusyAsync(RefreshAsync);

    /// <summary>
    /// The actual status refresh, deliberately NOT wrapped in RunBusyAsync.
    /// RunBusyAsync is a re-entrancy guard (BaseViewModel: `if (IsBusy) return;`), so calling the
    /// Load command from inside another RunBusyAsync block silently did nothing — which left the
    /// Subscribe buttons on screen after a successful purchase even though the toast claimed
    /// success. Purchase and RestorePurchases call this directly instead.
    /// </summary>
    private async Task RefreshAsync()
    {
        var session = _supabase.Session ?? throw new InvalidOperationException("Not logged in.");

        // Re-validate against the store before reading the tier, so the paywall reflects a
        // lapsed, refunded, or revoked subscription instead of the last value written locally.
        // Safe on failure: RefreshEntitlementAsync holds the current tier rather than downgrading.
        CurrentTier = await _subscription.RefreshEntitlementAsync();
        IsPremium = CurrentTier == SubscriptionTier.Premium;
        
        // Get video statistics
        TotalVideos = await _supabase.GetUserVideoCountAsync(session.UserId);
        RemainingSlots = await _subscription.GetRemainingVideoSlotsAsync(session.UserId);
        
        // Update status message
        if (IsPremium)
        {
            TierStatusMessage = $"✨ Premium — 1,000 momentos\n{TotalVideos} saved";
        }
        else
        {
            var freeLimit = TotalVideos + RemainingSlots;
            TierStatusMessage = $"Free Tier — {RemainingSlots} of {freeLimit} slots remaining\n{TotalVideos} videos saved";
        }
        
        // Load available plans (only if not premium)
        if (!IsPremium)
        {
            var plans = await _subscription.GetAvailablePlansAsync();
            AvailablePlans.Clear();
            foreach (var plan in plans)
            {
                AvailablePlans.Add(plan);
            }
        }
        else
        {
            // Nothing renders them while premium, but a stale list would flash back on lapse.
            AvailablePlans.Clear();
        }
    }

    [RelayCommand]
    private async Task Purchase(SubscriptionPlan plan)
    {
        await RunBusyAsync(async () =>
        {
            try
            {
                await _subscription.PurchaseSubscriptionAsync(plan);

                // Refresh status after successful purchase
                await RefreshAsync();

                _ = Toast.Make("✨ You're now Premium! Enjoy 1,000 momentos.").Show();
            }
            catch (OperationCanceledException)
            {
                // User dismissed the purchase sheet — no message needed
            }
            catch (Exception ex)
            {
                _ = Toast.Make($"Purchase failed: {ex.Message}").Show();
            }
        });
    }

    // App Store Review Guideline 3.1.2(c) requires functional links to the privacy policy and the
    // terms of use inside the purchase flow itself, not just elsewhere in the app. Build 12 was
    // rejected for having them only on the Profile page.
    [RelayCommand]
    private async Task OpenPrivacyPolicy()
    {
        await Launcher.OpenAsync(new Uri(LegalUrls.PrivacyPolicy));
    }

    [RelayCommand]
    private async Task OpenTermsOfUse()
    {
        await Launcher.OpenAsync(new Uri(LegalUrls.TermsOfUse));
    }

    [RelayCommand]
    private async Task RestorePurchases()
    {
        await RunBusyAsync(async () =>
        {
            try
            {
                await _subscription.RestorePurchasesAsync();
                await RefreshAsync();
                _ = Toast.Make("✅ Purchases restored.").Show();
            }
            catch (Exception ex)
            {
                _ = Toast.Make($"Restore failed: {ex.Message}").Show();
            }
        });
    }
}
