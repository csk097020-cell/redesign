namespace MomentaryMomentos.Services;

/// <summary>
/// Schedules local push notifications for user engagement, insights, and memory flashbacks.
/// Three notification categories:
///   1. Encouragement — reminds users who haven't captured in 3/7/14 days
///   2. Weekly Insight — Sunday snapshot of their stats
///   3. Memory Flashback — tomorrow-morning "On This Day" or random favorite
/// </summary>
public interface IAppNotifications
{
    /// <summary>
    /// Requests the OS-level notification permission (iOS requires explicit; Android 13+ requires
    /// it too), plus Android's separate exact-alarm consent. Returns whether it was granted —
    /// scheduling anything after a false is pointless, the OS drops it silently.
    /// </summary>
    Task<bool> RequestPermissionAsync();

    /// <summary>
    /// Posts a notification 15 seconds from now so a user can confirm delivery works. Every other
    /// notification here is at least 12 hours out, which makes them impossible to test on demand.
    /// Returns false if permission was refused.
    /// </summary>
    Task<bool> SendTestNotificationAsync();

    /// <summary>
    /// Schedules engagement reminders based on when the last memory was captured.
    /// Fires at a future time so the notification appears even when the app is closed.
    /// Cancels the previous schedule and resets the countdown, so do NOT call it on every app
    /// open — that resets the 3-day timer daily and the reminder can never fire.
    /// </summary>
    Task ScheduleEngagementRemindersAsync(DateTimeOffset lastCaptureTime);

    /// <summary>Schedules (or re-schedules) a weekly insight notification for the coming Sunday at 10 AM.</summary>
    Task ScheduleWeeklyInsightAsync(int totalMemories, int thisWeekCount, string topTagName);

    /// <summary>Schedules a tomorrow-morning notification previewing the featured memory clip.</summary>
    Task ScheduleMemoryFlashbackAsync(string memoryTitle, string subtitle);

    /// <summary>
    /// Schedules the daily "Momento Challenge" notifications for the coming four weeks — a
    /// capture prompt ("make a momento tagged Joy today") to build the capture habit, one per day
    /// in a fixed 28-prompt order. Replaces the pending schedule.
    /// </summary>
    Task ScheduleDailyChallengesAsync();

    /// <summary>Cancels all pending notifications (e.g. on sign-out).</summary>
    void CancelAll();
}
