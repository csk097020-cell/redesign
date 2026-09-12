using MomentaryMomentos.Services;

namespace MomentaryMomentos.Views;

public partial class SplashPage : ContentPage
{
    private readonly IAuthService _auth;
    private readonly IAppNotifications _notifications;

    public SplashPage(IAuthService auth, IAppNotifications notifications)
    {
        InitializeComponent();
        _auth          = auth;
        _notifications = notifications;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await RunSplashSequenceAsync();
    }

    private async Task RunSplashSequenceAsync()
    {
        // ── Phase 1: Glow rings bloom in ──────────────────────────────
        var glowIn = Task.WhenAll(
            GlowRingOuter.FadeTo(1, 600, Easing.CubicOut),
            GlowRingInner.FadeTo(0.8, 400, Easing.CubicOut)
        );

        // ── Phase 2: Logo springs into view ───────────────────────────
        LogoStack.Opacity = 1;
        var logoScale = LogoImage.ScaleTo(1.0, 700, Easing.SpringOut);
        var logoFade  = LogoImage.FadeTo(1, 500, Easing.CubicOut);

        await Task.WhenAll(glowIn, logoScale, logoFade);

        // ── Phase 3: App title slides up and fades in ─────────────────
        await Task.WhenAll(
            AppTitle.FadeTo(1, 500, Easing.CubicOut),
            AppTitle.TranslateTo(0, 0, 500, Easing.CubicOut)
        );

        // ── Phase 4: Tagline fades in ─────────────────────────────────
        await Task.WhenAll(
            Tagline.FadeTo(1, 400, Easing.CubicOut),
            Tagline.TranslateTo(0, 0, 400, Easing.CubicOut)
        );

        // ── Phase 5: Loading indicator appears ────────────────────────
        await LoadingIndicator.FadeTo(1, 300, Easing.CubicOut);

        // ── Phase 6: Start background glow pulse ──────────────────────
        _ = PulseGlowAsync();

        // ── Phase 7: Restore session + request notification permission in parallel ──
        bool restored = false;
        try
        {
            var restoreTask    = _auth.TryRestoreSessionAsync();
            var permissionTask = _notifications.RequestPermissionAsync();
            restored = await restoreTask;
            await permissionTask;
        }
        catch { /* stay on login */ }

        // Minimum display time so the splash feels intentional
        await Task.Delay(400);

        // ── Phase 8: Fade out and navigate ────────────────────────────
        await this.FadeTo(0, 350, Easing.CubicIn);

        if (Application.Current?.Windows.FirstOrDefault() is Window window)
        {
            var shell = new AppShell();
            window.Page = shell;

            if (restored && Shell.Current is not null)
            {
                await Shell.Current.GoToAsync("//main/home");
            }
        }
    }

    private async Task PulseGlowAsync()
    {
        while (IsVisible)
        {
            await Task.WhenAll(
                GlowRingOuter.ScaleTo(1.15, 900, Easing.SinInOut),
                GlowRingInner.ScaleTo(1.1, 900, Easing.SinInOut)
            );
            await Task.WhenAll(
                GlowRingOuter.ScaleTo(1.0, 900, Easing.SinInOut),
                GlowRingInner.ScaleTo(1.0, 900, Easing.SinInOut)
            );
        }
    }
}
