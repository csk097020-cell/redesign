using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using MomentaryMomentos.Services;
using Plugin.LocalNotification;

namespace MomentaryMomentos;

[Activity(
    Theme = "@style/Maui.SplashTheme",
    MainLauncher = true,
    LaunchMode = LaunchMode.SingleTop,
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode |
                           ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    // GENERAL-PURPOSE COMPONENT - Licensed to Client per contract Section 6.2
    // Bridge: VideoRecorderService needs activity results routed to it.
    private static VideoRecorderService? _videoRecorderService;

    public static void SetVideoRecorderService(VideoRecorderService svc)
        => _videoRecorderService = svc;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        RegisterNotificationChannels();

        // Without this the launch intent never reaches the plugin, so tapping a notification
        // just opens the app and drops whatever the notification was pointing at.
        LocalNotificationCenter.NotifyNotificationTapped(Intent);
    }

    // LaunchMode.SingleTop means a tap on an already-running app arrives here, not in OnCreate.
    protected override void OnNewIntent(Intent? intent)
    {
        base.OnNewIntent(intent);
        LocalNotificationCenter.NotifyNotificationTapped(intent);
    }

    protected override void OnActivityResult(int requestCode, Result resultCode, Intent? data)
    {
        base.OnActivityResult(requestCode, resultCode, data);
        _videoRecorderService?.OnActivityResult(requestCode, resultCode, data);
    }

    // Register Android notification channels (required on API 26+).
    // Channel IDs must match those in MomentaryMomentos.Services.AppNotificationService.
    private void RegisterNotificationChannels()
    {
        if (Build.VERSION.SdkInt < BuildVersionCodes.O) return;

        var manager = GetSystemService(Android.Content.Context.NotificationService) as NotificationManager;
        if (manager is null) return;

        manager.CreateNotificationChannel(new NotificationChannel(
            "momo_engagement", "Encouragement", NotificationImportance.Default)
        {
            Description = "Reminders to capture moments when you haven't recorded recently"
        });

        manager.CreateNotificationChannel(new NotificationChannel(
            "momo_insights", "Weekly Insights", NotificationImportance.Default)
        {
            Description = "Your weekly memory stats snapshot every Sunday"
        });

        manager.CreateNotificationChannel(new NotificationChannel(
            "momo_flashback", "Memory Flashbacks", NotificationImportance.High)
        {
            Description = "\"On This Day\" and surprise memory previews"
        });

        manager.CreateNotificationChannel(new NotificationChannel(
            "momo_challenge", "Momento Challenges", NotificationImportance.Default)
        {
            Description = "Daily capture challenges — a fun prompt to record one momento today"
        });
    }
}
