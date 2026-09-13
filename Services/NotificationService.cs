using Plugin.LocalNotification;
using Plugin.LocalNotification.AndroidOption;

namespace MomentaryMomentos.Services;

/// <summary>
/// Implements <see cref="IAppNotifications"/> using Plugin.LocalNotification v11.
///
/// Notification IDs are stable so cancelling and rescheduling replaces the previous entry.
///
/// Channel IDs (Android) — must match the channels registered in MainActivity.OnCreate:
///   momo_engagement  — encouragement / re-engagement
///   momo_insights    — weekly stats snapshot
///   momo_flashback   — "On This Day" / memory preview
/// </summary>
public sealed class NotificationService : IAppNotifications
{
    // Stable notification IDs — never reuse across categories
    private const int IdEngagement3  = 2001;
    private const int IdEngagement7  = 2002;
    private const int IdEngagement14 = 2003;
    private const int IdInsight      = 2010;
    private const int IdFlashback    = 2020;
    // 2030–2057: one Momento Challenge per day for the coming four weeks. At the old 7-day horizon
    // the queue ran dry a week after the last app open and the prompts appeared to start over.
    //
    // The horizon is deliberately NOT the length of the prompt list. The list is 100 entries; iOS
    // caps an app at 64 pending local notifications and silently drops the rest, and IDs beyond
    // 2057 would run into IdTest. Four weeks of queue is plenty — each app open re-queues the next
    // 28 days, and the prompt for a day is chosen by its offset from the anchor, so a user still
    // walks the full 100 in order over 100 days.
    private const int IdChallengeBase     = 2030;
    private const int ChallengeHorizonDays = 28;
    private const int IdTest              = 2099;

    internal const string ChannelEngagement = "momo_engagement";
    internal const string ChannelInsights   = "momo_insights";
    internal const string ChannelFlashback  = "momo_flashback";
    internal const string ChannelChallenge  = "momo_challenge";

    private const string NotifyOperation = "notifications";

    private readonly UserPreferencesService _preferences;

    public NotificationService(UserPreferencesService preferences) => _preferences = preferences;

    /// <summary>
    /// Android options for a channel. Without an explicit small icon the plugin falls back to
    /// the adaptive launcher icon, which the status bar renders as a white blob.
    /// </summary>
    private static AndroidOptions AndroidFor(string channelId) => new()
    {
        ChannelId     = channelId,
        IconSmallName = new AndroidIcon("notification_icon"),
    };

    // ── Permission ────────────────────────────────────────────────────────────

    public async Task<bool> RequestPermissionAsync()
    {
        try
        {
            // The result used to be discarded, so a user who tapped "Don't allow" looked
            // identical to one who granted it, and the app went on scheduling notifications
            // that Android silently dropped.
            //
            // RequestPermissionToScheduleExactAlarm defaults to false; without it the exact-alarm
            // consent screen never appears and every reminder degrades to an inexact alarm that
            // Doze can defer for hours.
            var granted = await LocalNotificationCenter.Current.RequestNotificationPermission(
                new NotificationPermission
                {
                    AskPermission = true,
                    Android = new AndroidNotificationPermission
                    {
                        RequestPermissionToScheduleExactAlarm = true,
                    },
                });

            Diagnostics.Trace(NotifyOperation, $"permission granted = {granted}");
            return granted;
        }
        catch (Exception ex)
        {
            Diagnostics.Report(ex, NotifyOperation, new Dictionary<string, string> { ["stage"] = "permission" });
            return false;
        }
    }

    /// <summary>
    /// Posts a notification 15 seconds out so delivery can actually be verified. Everything else
    /// this class schedules is at least 12 hours away, which made "notifications don't work"
    /// impossible to confirm or disprove on the spot.
    /// </summary>
    public async Task<bool> SendTestNotificationAsync()
    {
        var granted = await RequestPermissionAsync();
        if (!granted) return false;

        await TrySchedule(new NotificationRequest
        {
            NotificationId = IdTest,
            Title          = "Notifications are working 🎉",
            Description    = "This is a test from Momentary Momentos. Tap to open the app.",
            Schedule       = new NotificationRequestSchedule { NotifyTime = DateTime.Now.AddSeconds(15) },
            Android        = AndroidFor(ChannelEngagement),
        });

        return true;
    }

    // ── Engagement reminders ──────────────────────────────────────────────────

    public async Task ScheduleEngagementRemindersAsync(DateTimeOffset lastCaptureTime)
    {
        LocalNotificationCenter.Current.Cancel(IdEngagement3, IdEngagement7, IdEngagement14);

        await TrySchedule(Engagement(
            id:    IdEngagement3,
            title: "Life is happening 🎬",
            body:  "It's been 3 days since your last memory. Even 10 seconds is enough — what made you smile today?",
            at:    FutureAt(lastCaptureTime, days: 3, hour: 18)));

        await TrySchedule(Engagement(
            id:    IdEngagement7,
            title: "A week of moments uncaptured ✨",
            body:  "Your future self will thank you for capturing memories now. What happened this week?",
            at:    FutureAt(lastCaptureTime, days: 7, hour: 10)));

        await TrySchedule(Engagement(
            id:    IdEngagement14,
            title: "We miss you 💙",
            body:  "It's been 2 weeks! Open Momentary Momentos and capture just one moment. Any moment.",
            at:    FutureAt(lastCaptureTime, days: 14, hour: 10)));
    }

    // ── Weekly insight ────────────────────────────────────────────────────────

    public async Task ScheduleWeeklyInsightAsync(int totalMemories, int thisWeekCount, string topTagName)
    {
        LocalNotificationCenter.Current.Cancel(IdInsight);

        var nextSunday = NextSundayAt(10);
        if (nextSunday <= DateTime.Now) return;

        string body = thisWeekCount > 0
            ? $"This week you captured {thisWeekCount} {(thisWeekCount == 1 ? "memory" : "memories")}! " +
              $"You have {totalMemories} in total. Keep it up 🌟"
            : $"No captures this week yet — but you have {totalMemories} amazing memories. " +
              $"Add one more before Sunday! 💫";

        if (!string.IsNullOrWhiteSpace(topTagName))
            body += $" Your most-used emotion: {topTagName}.";

        await TrySchedule(new NotificationRequest
        {
            NotificationId = IdInsight,
            Title          = "Your weekly memory snapshot 📊",
            Description    = body,
            Schedule = new NotificationRequestSchedule
            {
                NotifyTime = nextSunday,
                RepeatType = NotificationRepeat.Weekly,
            },
            Android = AndroidFor(ChannelInsights),
        });
    }

    // ── Memory flashback ──────────────────────────────────────────────────────

    public async Task ScheduleMemoryFlashbackAsync(string memoryTitle, string subtitle)
    {
        LocalNotificationCenter.Current.Cancel(IdFlashback);

        var tomorrow9am = DateTime.Today.AddDays(1).AddHours(9);
        if (tomorrow9am <= DateTime.Now) return;

        await TrySchedule(new NotificationRequest
        {
            NotificationId = IdFlashback,
            Title          = subtitle,
            Description    = $"Tap to relive: \"{TruncateTitle(memoryTitle)}\"",
            Schedule = new NotificationRequestSchedule
            {
                NotifyTime = tomorrow9am,
            },
            Android = AndroidFor(ChannelFlashback),
        });
    }

    // ── Momento Challenges (#9) ───────────────────────────────────────────────

    // Daily prompts, delivered in the order written. A day's prompt is its offset from
    // ChallengeCycleStart, so a user meets #1 on their first day and works down the list; the
    // cycle repeats only after all of them have been seen. The pick used to be day-of-year modulo
    // the list length, which dropped each user into the list at whatever spot the calendar happened
    // to land on and read as a random order.
    //
    // Written by Corinne Kelley and delivered 2026-08-12, replacing the 28 interim prompts that
    // were written without her input. Grouped by theme in her original order: nature (1-15),
    // food (16-25), delight (26-35), the ordinary (36-45), people (46-55), animals (56-60),
    // the senses (61-70), places (71-80), growth (81-90), meaning (91-100). Her capitalisation of
    // "Momento" as a proper noun is kept verbatim.
    private static readonly string[] Challenges =
    {
        "Take a Momento somewhere in nature.",
        "Capture the sky exactly as it looks right now.",
        "Find something growing where you didn't expect it.",
        "Capture your favorite tree.",
        "Find something tiny and beautiful.",
        "Capture the sound of birds.",
        "Take a Momento near water.",
        "Capture the wind moving something.",
        "Find a flower you've never noticed before.",
        "Capture tonight's sunset.",
        "Take a Momento during golden hour.",
        "Capture something that makes you feel small in a good way.",
        "Find something perfectly imperfect in nature.",
        "Capture the weather today.",
        "Take a Momento of the view from where you are right now.",
        "Take a food Momento.",
        "Capture your first sip of something delicious.",
        "Take a Momento of your comfort food.",
        "Capture something you're eating for the first time.",
        "Take a Momento while cooking.",
        "Capture someone making food for you.",
        "Record the meal you could eat over and over again.",
        "Capture a snack that makes you weirdly happy.",
        "Take a Momento from your favorite local restaurant.",
        "Capture the mess left after a really good meal.",
        "Capture something that made you laugh today.",
        "Take a Momento of something completely ridiculous.",
        "Capture an inside joke.",
        "Find something unexpectedly cute.",
        "Capture a moment when you can't stop smiling.",
        "Take a Momento of your current tiny obsession.",
        "Capture something that delighted your inner child.",
        "Find something that makes absolutely no sense and document it.",
        "Capture the funniest thing you see today.",
        "Take a Momento of something you love for no logical reason.",
        "Capture your morning exactly as it is.",
        "Take a Momento of your commute.",
        "Capture your current room before you clean it.",
        "Take a Momento of something you do every single day.",
        "Capture the view from your front door.",
        "Take a Momento while running an ordinary errand.",
        "Capture what your desk looks like today.",
        "Record the sound of your neighborhood.",
        "Take a Momento of your shoes wherever they carried you today.",
        "Capture an ordinary moment you think you'll someday miss.",
        "Take a Momento with someone you love.",
        "Capture someone's laugh.",
        "Ask someone, \"What made you happy today?\" and record their answer.",
        "Capture someone telling a story.",
        "Take a Momento of a hug.",
        "Capture a friend doing something completely ordinary.",
        "Ask someone what they're looking forward to.",
        "Capture a family tradition.",
        "Take a Momento with someone you haven't seen in a while.",
        "Capture the person who made your day better.",
        "Take a Momento of an animal you meet today.",
        "Capture your pet doing absolutely nothing.",
        "Find a bird and record what it's doing.",
        "Capture the next animal you see.",
        "Take a Momento from your pet's point of view.",
        "Capture something beautiful you can hear.",
        "Take a Momento somewhere that smells amazing.",
        "Find an interesting texture.",
        "Capture the loudest place you visit today.",
        "Capture the quietest place you can find.",
        "Take a Momento of something colorful.",
        "Find something that sparkles.",
        "Capture something moving.",
        "Take a Momento somewhere that feels peaceful.",
        "Capture a sound you want to remember.",
        "Take a Momento somewhere you've never been.",
        "Capture a place you pass all the time but never really notice.",
        "Take the scenic route and capture what you find.",
        "Visit somewhere from your childhood.",
        "Capture your favorite place in your city.",
        "Take a Momento from somewhere completely unplanned.",
        "Find a weird roadside attraction.",
        "Capture a place that feels like a secret.",
        "Take a Momento from the farthest place you go today.",
        "Capture somewhere that feels like home.",
        "Capture something you're proud of today.",
        "Take a Momento of something you're learning.",
        "Capture yourself doing something you're getting better at.",
        "Record one sentence about how you're feeling right now.",
        "Capture something you're currently working toward.",
        "Take a Momento of something that represents this season of your life.",
        "Capture something you've changed your mind about.",
        "Record something you want your future self to know.",
        "Capture one thing you did today that took courage.",
        "Take a Momento of something that feels very you.",
        "Capture something you're grateful exists.",
        "Take a Momento of something that reminds you of someone.",
        "Capture an object with a story behind it.",
        "Record someone explaining why something matters to them.",
        "Capture something you almost walked past.",
        "Take a Momento of a place where something important happened.",
        "Capture a moment you wish you could bottle up.",
        "Record one thing you hope you never forget about today.",
        "Capture something that feels like the beginning of something.",
        "Take a Momento of whatever made you stop and think, \"I want to remember this.\"",
    };

    public async Task ScheduleDailyChallengesAsync()
    {
        // Replace the pending schedule with a fresh one starting today.
        LocalNotificationCenter.Current.Cancel(
            Enumerable.Range(IdChallengeBase, ChallengeHorizonDays).ToArray());

        // First run anchors the sequence to today so this user starts at prompt #1.
        var anchor = _preferences.ChallengeCycleStart ?? DateTime.Today;
        _preferences.ChallengeCycleStart = anchor;

        for (int i = 0; i < ChallengeHorizonDays; i++)
        {
            var day    = DateTime.Today.AddDays(i);
            var notify = day.AddHours(12); // noon — mid-day nudge, leaves the day to complete it
            if (notify <= DateTime.Now) continue; // today's slot already passed

            // Keyed to the calendar date, not the loop slot, so rescheduling mid-cycle keeps
            // each day on the prompt it was always going to get.
            var offset    = (int)(day - anchor).TotalDays;
            var challenge = Challenges[((offset % Challenges.Length) + Challenges.Length) % Challenges.Length];

            await TrySchedule(new NotificationRequest
            {
                NotificationId = IdChallengeBase + i,
                Title          = "🏆 Momento Challenge of the Day",
                Description    = challenge,
                Schedule       = new NotificationRequestSchedule { NotifyTime = notify },
                Android        = AndroidFor(ChannelChallenge),
            });
        }
    }

    // ── Cancel all ────────────────────────────────────────────────────────────

    public void CancelAll()
    {
        try { _ = LocalNotificationCenter.Current.CancelAll(); }
        catch (Exception ex)
        {
            Diagnostics.Report(ex, NotifyOperation, new Dictionary<string, string> { ["stage"] = "cancel-all" });
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static NotificationRequest Engagement(int id, string title, string body, DateTime at) =>
        new()
        {
            NotificationId = id,
            Title          = title,
            Description    = body,
            Schedule       = new NotificationRequestSchedule { NotifyTime = at },
            Android        = AndroidFor(ChannelEngagement),
        };

    private static async Task TrySchedule(NotificationRequest request)
    {
        try { await LocalNotificationCenter.Current.Show(request); }
        catch (Exception ex)
        {
            Diagnostics.Report(ex, NotifyOperation, new Dictionary<string, string>
            {
                ["stage"]          = "schedule",
                ["notificationId"] = request.NotificationId.ToString(),
                ["notifyTime"]     = request.Schedule?.NotifyTime?.ToString("O") ?? "(none)",
            });
        }
    }

    private static DateTime FutureAt(DateTimeOffset from, int days, int hour)
    {
        var candidate = from.LocalDateTime.Date.AddDays(days).AddHours(hour);
        if (candidate <= DateTime.Now)
            candidate = DateTime.Now.Date.AddDays(days).AddHours(hour);
        return candidate;
    }

    private static DateTime NextSundayAt(int hour)
    {
        var today = DateTime.Today;
        int daysUntilSunday = ((int)DayOfWeek.Sunday - (int)today.DayOfWeek + 7) % 7;
        if (daysUntilSunday == 0) daysUntilSunday = 7;
        return today.AddDays(daysUntilSunday).AddHours(hour);
    }

    private static string TruncateTitle(string title, int max = 50) =>
        title.Length <= max ? title : title[..max] + "…";
}
