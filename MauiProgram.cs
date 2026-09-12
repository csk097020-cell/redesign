// GENERAL-PURPOSE COMPONENT - Licensed to Client per contract Section 6.2
using CommunityToolkit.Maui;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MomentaryMomentos.Data;
using MomentaryMomentos.Models;
using MomentaryMomentos.Services;
using MomentaryMomentos.ViewModels;
using MomentaryMomentos.Views;
using Plugin.LocalNotification;


/*
 * To the baby,
 * who only lived for a day
 * and was never given a name,
 * in memory of you,
 * these memories are made.
 *
 * Thank you all.
 *
 */

namespace MomentaryMomentos;

/// <summary>
/// Composition root. All services, ViewModels, and Pages are registered here.
/// Dependency Injection lifetimes:
///   Singleton  = shared state, lives for the entire app session (services, db)
///   Transient  = fresh instance per navigation (ViewModels, Pages)
/// </summary>
public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        // ── Configuration ──────────────────────────────────────────────────────
        // appsettings.json is embedded as a resource (EmbeddedResource in .csproj).
        // The manifest resource name for LogicalName="appsettings.json" is just "appsettings.json".
        //
        // Loaded before anything reads it. This used to sit *below* the builder chain while
        // UseSentry read the DSN above it, which worked only because Sentry defers its options
        // lambda until Build() — an easy dependency to break by accident.
        var assembly = typeof(MauiProgram).Assembly;
        var resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith("appsettings.json", StringComparison.OrdinalIgnoreCase))
            ?? "appsettings.json";

        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is not null)
            builder.Configuration.AddJsonStream(stream);

        var cfg = builder.Configuration;

        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .UseMauiCommunityToolkitMediaElement(false)
            .UseLocalNotification()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-SemiBold.ttf", "OpenSansSemiBold");
            });

        // ── Crash reporting (best effort) ──────────────────────────────────────
        // Sentry starts during app launch, before any of our own error handling exists, so an
        // unhandled failure here takes the whole app down at the splash screen. Telemetry must
        // never be able to do that: skip it entirely when no DSN is configured, and swallow a
        // startup failure rather than trading a working app for a crash report.
        var sentryDsn = cfg["Sentry:Dsn"];
        if (!string.IsNullOrWhiteSpace(sentryDsn))
        {
            try
            {
                builder.UseSentry(options =>
                {
                    options.Dsn = sentryDsn;
                    options.TracesSampleRate = 0.1;
                    options.IsGlobalModeEnabled = true;
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MoMo] Sentry init failed, continuing without it: {ex}");
            }
        }

        var appSettings = AppSettings.Load(cfg);
        builder.Services.AddSingleton(appSettings);

        builder.Services.AddSingleton(new SupabaseSettings
        {
            Url = appSettings.SupabaseUrl,
            AnonKey = appSettings.SupabaseAnonKey,
            StorageBucket = appSettings.StorageBucketUserVideos
        });

        // ── HTTP ───────────────────────────────────────────────────────────────
        // The default HttpClient timeout is 100 seconds, which a 50 MB single-shot video
        // upload on cellular blows straight through — the resulting TaskCanceledException
        // was indistinguishable from being offline.
        builder.Services.AddSingleton(new HttpClient { Timeout = TimeSpan.FromMinutes(10) });

        // ── Core / General-Purpose Services ───────────────────────────────────
        builder.Services.AddSingleton<SupabaseService>();
        builder.Services.AddSingleton<SecureTokenStore>();
        builder.Services.AddSingleton<LocalDb>();
        builder.Services.AddSingleton<ConnectivityService>();
        builder.Services.AddSingleton<LocalStorageService>();
        builder.Services.AddSingleton<UserPreferencesService>();
        builder.Services.AddSingleton<ColorThemeService>();

        // ── Application-Specific Services ─────────────────────────────────────
        builder.Services.AddSingleton<IAuthService, AuthService>();
        builder.Services.AddSingleton<SyncService>();
        builder.Services.AddSingleton<OfflineService>();
        builder.Services.AddSingleton<IInAppBillingService, InAppBillingService>();
        builder.Services.AddSingleton<ISubscriptionService, SubscriptionService>();
        builder.Services.AddSingleton<IVideoService, VideoService>();
        builder.Services.AddSingleton<FeaturedClipService>();
        builder.Services.AddSingleton<IAppNotifications, NotificationService>();

        // ── Platform video recorder ────────────────────────────────────────────
        // Android: MediaStore intent with 10-second duration limit via FileProvider.
        // iOS:     UIImagePickerController with VideoMaximumDuration = 10 (OS enforces limit).
#if ANDROID || IOS
        builder.Services.AddSingleton<IVideoRecorderService, VideoRecorderService>();
#else
        builder.Services.AddSingleton<IVideoRecorderService, NoOpVideoRecorderService>();
#endif

        // ── Platform video trimmer ─────────────────────────────────────────────
        // Android: MediaExtractor + MediaMuxer (lossless remux, no re-encode)
        // iOS:     AVAssetExportSession
#if ANDROID || IOS
        builder.Services.AddSingleton<IVideoTrimService, VideoTrimService>();
#endif

        // ── ViewModels (Transient = fresh per navigation) ──────────────────────
        builder.Services.AddTransient<LoginViewModel>();
        builder.Services.AddTransient<RegisterViewModel>();
        builder.Services.AddTransient<ForgotPasswordViewModel>();
        builder.Services.AddTransient<HomeViewModel>();
        builder.Services.AddTransient<CaptureViewModel>();
        builder.Services.AddTransient<ReliveViewModel>();
        builder.Services.AddTransient<ProfileViewModel>();
        builder.Services.AddTransient<SubscriptionViewModel>();
        builder.Services.AddTransient<AdminViewModel>(); // ISubscriptionService injected via DI
        builder.Services.AddTransient<MemoryDetailViewModel>();
        builder.Services.AddTransient<TagManagementViewModel>();

        // ── Pages (Transient = new instance per Shell navigation) ─────────────
        builder.Services.AddTransient<SplashPage>();
        builder.Services.AddTransient<TrimPage>();
        builder.Services.AddTransient<TrimViewModel>();
        builder.Services.AddTransient<LoginPage>();
        builder.Services.AddTransient<RegisterPage>();
        builder.Services.AddTransient<ForgotPasswordPage>();
        builder.Services.AddTransient<HomePage>();
        builder.Services.AddTransient<CapturePage>();
        builder.Services.AddTransient<RelivePage>();
        builder.Services.AddTransient<ProfilePage>();
        builder.Services.AddTransient<SubscriptionPage>();
        builder.Services.AddTransient<AdminPage>();
        builder.Services.AddTransient<VideoPlayerPage>();
        builder.Services.AddTransient<MemoryDetailPage>();
        builder.Services.AddTransient<TagManagementPage>();
        builder.Services.AddTransient<HelpPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}

// ── iOS / fallback stub ────────────────────────────────────────────────────────
// GENERAL-PURPOSE COMPONENT - Licensed to Client per contract Section 6.2
// On iOS, CaptureViewModel detects IsSupported=false and switches to MediaPicker.
public sealed class NoOpVideoRecorderService : MomentaryMomentos.Services.IVideoRecorderService
{
    public bool IsSupported => false;
    public Task<string?> CaptureVideoAsync(int maxDurationSeconds = 0, bool facingFront = false, bool muteAudio = false)
        => Task.FromResult<string?>(null);
}
