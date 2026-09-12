namespace MomentaryMomentos.Services;

/// <summary>
/// Manages user preferences and app settings stored locally.
/// Uses .NET MAUI Preferences API for simple key-value storage.
/// </summary>
public class UserPreferencesService
{
    private const string KEY_OFFLINE_MODE = "offline_mode";
    private const string KEY_WIFI_ONLY_UPLOAD = "wifi_only_upload";
    private const string KEY_AUTO_SYNC = "auto_sync";
    private const string KEY_VIDEO_QUALITY = "video_quality";
    private const string KEY_THEME = "theme";
    private const string KEY_NOTIFICATIONS_SCHEDULED_ON = "notifications_scheduled_on";
    private const string KEY_CHALLENGE_CYCLE_START = "challenge_cycle_start";

    /// <summary>
    /// Enable/disable offline mode (work without Supabase).
    /// Default: false (online mode).
    /// </summary>
    public bool OfflineModeEnabled
    {
        get => Preferences.Default.Get(KEY_OFFLINE_MODE, false);
        set => Preferences.Default.Set(KEY_OFFLINE_MODE, value);
    }

    /// <summary>
    /// Only upload videos when connected to WiFi.
    /// Default: true (WiFi only).
    /// </summary>
    public bool WiFiOnlyUpload
    {
        get => Preferences.Default.Get(KEY_WIFI_ONLY_UPLOAD, true);
        set => Preferences.Default.Set(KEY_WIFI_ONLY_UPLOAD, value);
    }

    /// <summary>
    /// Automatically sync data when connection is available.
    /// Default: true.
    /// </summary>
    public bool AutoSyncEnabled
    {
        get => Preferences.Default.Get(KEY_AUTO_SYNC, true);
        set => Preferences.Default.Set(KEY_AUTO_SYNC, value);
    }

    /// <summary>
    /// Video recording quality: "low", "medium", "high".
    /// Default: "medium".
    /// </summary>
    public string VideoQuality
    {
        get => Preferences.Default.Get(KEY_VIDEO_QUALITY, "medium");
        set => Preferences.Default.Set(KEY_VIDEO_QUALITY, value);
    }

    /// <summary>
    /// App theme: "light", "dark", "system".
    /// Default: "system".
    /// </summary>
    public string Theme
    {
        get => Preferences.Default.Get(KEY_THEME, "system");
        set => Preferences.Default.Set(KEY_THEME, value);
    }

    /// <summary>
    /// The local date notifications were last (re)scheduled. Rescheduling cancels and restarts
    /// every countdown, so doing it on every app open meant the 3-day engagement reminder reset
    /// daily and could never fire. Once per day is enough to keep the schedule fresh.
    /// </summary>
    public DateTime? NotificationsScheduledOn
    {
        get
        {
            var raw = Preferences.Default.Get(KEY_NOTIFICATIONS_SCHEDULED_ON, string.Empty);
            return DateTime.TryParse(raw, null, System.Globalization.DateTimeStyles.RoundtripKind, out var d)
                ? d.Date
                : null;
        }
        set => Preferences.Default.Set(
            KEY_NOTIFICATIONS_SCHEDULED_ON,
            value?.Date.ToString("O") ?? string.Empty);
    }

    /// <summary>
    /// The local date this device started the Momento Challenge sequence. The prompt for a given
    /// day is its offset from this anchor, so every user walks the list from the first prompt to
    /// the last in order instead of dropping into it at a spot determined by the calendar.
    /// Set once, on the first schedule, and never moved — moving it would replay prompts.
    /// </summary>
    public DateTime? ChallengeCycleStart
    {
        get
        {
            var raw = Preferences.Default.Get(KEY_CHALLENGE_CYCLE_START, string.Empty);
            return DateTime.TryParse(raw, null, System.Globalization.DateTimeStyles.RoundtripKind, out var d)
                ? d.Date
                : null;
        }
        set => Preferences.Default.Set(
            KEY_CHALLENGE_CYCLE_START,
            value?.Date.ToString("O") ?? string.Empty);
    }

    /// <summary>
    /// Clear all preferences (reset to defaults).
    /// </summary>
    public void ClearAll()
    {
        Preferences.Default.Clear();
    }

    /// <summary>
    /// Get a summary of current settings.
    /// </summary>
    public string GetSettingsSummary()
    {
        return $@"
Offline Mode: {(OfflineModeEnabled ? "Enabled" : "Disabled")}
WiFi Only Upload: {(WiFiOnlyUpload ? "Yes" : "No")}
Auto Sync: {(AutoSyncEnabled ? "Enabled" : "Disabled")}
Video Quality: {VideoQuality}
Theme: {Theme}
        ".Trim();
    }
}
