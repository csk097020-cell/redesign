namespace MomentaryMomentos.Utils;

public static class RelativeTimeFormatter
{
    public static string FormatAgo(DateTimeOffset date)
    {
        var elapsed = DateTimeOffset.UtcNow - date.ToUniversalTime();
        if (elapsed < TimeSpan.Zero)
            elapsed = TimeSpan.Zero;

        if (elapsed.TotalMinutes < 1)
            return "just now";

        if (elapsed.TotalHours < 1)
        {
            var minutes = Math.Max(1, (int)Math.Round(elapsed.TotalMinutes));
            return $"{minutes} min{(minutes == 1 ? "" : "s")} ago";
        }

        if (elapsed.TotalDays < 1)
        {
            var hours = Math.Max(1, (int)Math.Round(elapsed.TotalHours));
            return $"{hours} hr{(hours == 1 ? "" : "s")} ago";
        }

        if (elapsed.TotalDays < 30)
        {
            var days = Math.Max(1, (int)Math.Floor(elapsed.TotalDays));
            return $"{days} day{(days == 1 ? "" : "s")} ago";
        }

        if (elapsed.TotalDays < 60)
        {
            var weeks = Math.Max(1, (int)Math.Round(elapsed.TotalDays / 7));
            return $"{weeks} week{(weeks == 1 ? "" : "s")} ago";
        }

        if (elapsed.TotalDays < 730)
        {
            var months = Math.Max(1, (int)Math.Round(elapsed.TotalDays / 30.4375));
            return $"{months} month{(months == 1 ? "" : "s")} ago";
        }

        var years = elapsed.TotalDays / 365.2425;
        var roundedYears = Math.Round(years, 1);
        var yearText = roundedYears % 1 == 0
            ? ((int)roundedYears).ToString()
            : roundedYears.ToString("0.0");

        return $"{yearText} year{(roundedYears == 1 ? "" : "s")} ago";
    }

    public static string FormatRecordedAgo(DateTimeOffset date) =>
        $"Recorded {FormatAgo(date)}";
}
