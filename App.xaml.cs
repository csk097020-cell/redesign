using MomentaryMomentos.Services;
using MomentaryMomentos.Views;

namespace MomentaryMomentos;

public partial class App : Application
{
    private const string LastEntitlementCheckKey = "momo_entitlement_checked_at";

    /// <summary>
    /// How stale an entitlement check may be before a resume re-runs it. Long enough that
    /// app-switching does not hammer the App Store, short enough that a lapse or refund is
    /// picked up the same day.
    /// </summary>
    private static readonly TimeSpan EntitlementRecheckInterval = TimeSpan.FromHours(12);

    private readonly IAuthService _auth;
    private readonly IAppNotifications _notifications;
    private readonly SyncService _sync;
    private readonly LocalStorageService _localStorage;
    private readonly ISubscriptionService _subscription;

    public App(
        IAuthService auth,
        IAppNotifications notifications,
        SyncService sync,
        LocalStorageService localStorage,
        ISubscriptionService subscription)
    {
        InitializeComponent();

        // The app is designed dark-only; without this, controls that don't set an explicit
        // color (e.g. native pickers/keyboards) silently follow the device's system light/dark
        // setting, producing black-on-dark text and a bright system keyboard in light mode.
        UserAppTheme = AppTheme.Dark;

        _auth          = auth;
        _notifications = notifications;
        _sync          = sync;
        _localStorage  = localStorage;
        _subscription  = subscription;
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var splash = new SplashPage(_auth, _notifications);
        var window = new Window(splash);

        // SyncService only flushed the pending-upload queue on a connectivity *change*, so an
        // upload that failed while online sat there until the network happened to flap. Retry
        // whenever the app comes back to the foreground.
        window.Resumed += (_, _) => _ = FlushPendingUploadsAsync();

        // Entitlement can lapse while the app is backgrounded — a subscription expires, or
        // Apple refunds one. Nothing pushes that to us (App Store Server Notifications are a
        // later step), so re-check it on the way back in.
        window.Resumed += (_, _) => _ = RefreshEntitlementAsync();

        // Trim and compress intermediates were never cleaned up. Six hours is comfortably past
        // any in-flight import while still reclaiming space the same day.
        _ = Task.Run(() => _localStorage.CleanTemporaryFiles(TimeSpan.FromHours(6)));

        return window;
    }

    private async Task FlushPendingUploadsAsync()
    {
        try
        {
            await _sync.SyncPendingUploadsAsync();
        }
        catch (Exception ex)
        {
            Diagnostics.Report(ex, "app.resume-sync");
        }
    }

    private async Task RefreshEntitlementAsync()
    {
        try
        {
            if (!_auth.IsAuthenticated)
                return;

            var last = Preferences.Get(LastEntitlementCheckKey, 0L);
            if (last > 0)
            {
                var since = DateTimeOffset.UtcNow - DateTimeOffset.FromUnixTimeSeconds(last);
                if (since < EntitlementRecheckInterval)
                    return;
            }

            await _subscription.RefreshEntitlementAsync();

            // Only stamp on success, so a failed check retries on the next resume instead of
            // going quiet for another 12 hours.
            Preferences.Set(LastEntitlementCheckKey, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        }
        catch (Exception ex)
        {
            Diagnostics.Report(ex, "app.resume-entitlement");
        }
    }
}
