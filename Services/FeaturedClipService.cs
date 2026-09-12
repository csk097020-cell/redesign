using System.Text.Json;
using MomentaryMomentos.Models;

namespace MomentaryMomentos.Services;

/// <summary>
/// Time-Aware Weighted Recall: selects the best clip to surface when the user opens the app.
///
/// Score(memory) =
///   (baseWeight + favoriteBonus) × (1 + anniversaryBonus) − recentPenalty
///
/// Anniversary bonus (strongest signal — "On This Day"):
///   +3.0  exact same month+day as today, from a prior year
///   +1.5  within 3 days of today's month+day, from a prior year
///   +0.5  same month, from a prior year
///
/// Favorite bonus:   +1.0 if IsFavorite
/// Recent penalty:   −2.0 if this memory was featured in the last 7 days
/// Base weight:      from momo_memory_metrics (0 = suppressed by "See Less" → never feature)
///
/// The selection is locked in for the full day so the user always sees the same
/// "Today's Clip" regardless of how many times they open the app.
/// </summary>
public class FeaturedClipService
{
    private const string PrefsFeaturedDate = "momo_featured_date";
    private const string PrefsFeaturedId   = "momo_featured_id";
    private const string PrefsHistory      = "momo_featured_history";

    private record ShownEntry(string Id, string Date);

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Returns today's featured clip, computing and caching a new one if needed.</summary>
    public Memory? GetFeaturedClip(List<Memory> memories, Dictionary<string, int> weights)
    {
        if (memories.Count == 0) return null;

        // Show only favorited memories; fall back to all if user has no favorites yet
        var favorites = memories.Where(m => m.IsFavorite).ToList();
        memories = favorites.Count > 0 ? favorites : memories;

        var today = DateOnly.FromDateTime(DateTime.Today);

        // Return today's cached pick without re-computing
        var cachedDate = Preferences.Get(PrefsFeaturedDate, "");
        var cachedId   = Preferences.Get(PrefsFeaturedId,   "");
        if (cachedDate == today.ToString("o") && !string.IsNullOrEmpty(cachedId))
        {
            var cached = memories.FirstOrDefault(m => m.Id == cachedId);
            if (cached is not null) return cached;
        }

        // Build the recently-shown exclusion window (last 7 days)
        var history    = LoadHistory();
        var cutoff     = today.AddDays(-7).ToString("o");
        var recentIds  = history
            .Where(h => string.Compare(h.Date, cutoff, StringComparison.Ordinal) >= 0)
            .Select(h => h.Id)
            .ToHashSet();

        // Score every memory
        var scored = memories
            .Select(m => (Memory: m, Score: Score(m, weights, today, recentIds)))
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .ToList();

        // Fallback: if everything is suppressed, use the full list with uniform weight
        if (scored.Count == 0)
            scored = memories.Select(m => (Memory: m, Score: 1.0)).ToList();

        // Weighted-random from the top 20% (minimum 5 candidates)
        int take  = Math.Max(5, scored.Count / 5);
        var pool  = scored.Take(take).ToList();
        double total = pool.Sum(x => x.Score);
        double r     = Random.Shared.NextDouble() * total;
        double cum   = 0;
        Memory? pick = null;
        foreach (var (mem, score) in pool)
        {
            cum += score;
            if (r <= cum) { pick = mem; break; }
        }
        pick ??= pool[0].Memory;

        // Persist today's choice and prune old history
        Preferences.Set(PrefsFeaturedDate, today.ToString("o"));
        Preferences.Set(PrefsFeaturedId,   pick.Id);

        var thirtyDaysCutoff = today.AddDays(-30).ToString("o");
        var updated = history
            .Where(h => string.Compare(h.Date, thirtyDaysCutoff, StringComparison.Ordinal) >= 0)
            .Append(new ShownEntry(pick.Id, today.ToString("o")))
            .ToList();
        Preferences.Set(PrefsHistory, JsonSerializer.Serialize(updated));

        return pick;
    }

    /// <summary>Returns a human-readable subtitle explaining why this clip was selected.</summary>
    public static string GetSubtitle(Memory memory)
    {
        var today   = DateOnly.FromDateTime(DateTime.Today);
        var memDate = DateOnly.FromDateTime(memory.CreatedAt.LocalDateTime.Date);

        if (memDate.Year < today.Year)
        {
            int yearsAgo = today.Year - memDate.Year;
            string ago   = yearsAgo == 1 ? "1 Year Ago" : $"{yearsAgo} Years Ago";

            if (memDate.Month == today.Month && memDate.Day == today.Day)
                return $"📅 On This Day — {ago}";

            if (memDate.Month == today.Month)
                return $"📆 This Month, {ago}";
        }

        return "⭐ Today's Favorite Memory";
    }

    // ── Scoring ───────────────────────────────────────────────────────────────

    private static double Score(
        Memory memory,
        Dictionary<string, int> weights,
        DateOnly today,
        HashSet<string> recentIds)
    {
        double baseWeight = Math.Max(0, weights.GetValueOrDefault(memory.Id, 1));
        if (baseWeight == 0) return 0; // "See Less" suppressed — never feature

        var memDate = DateOnly.FromDateTime(memory.CreatedAt.LocalDateTime.Date);

        double anniversaryBonus = 0.0;
        if (memDate.Year < today.Year)
        {
            if (memDate.Month == today.Month && memDate.Day == today.Day)
                anniversaryBonus = 3.0;
            else if (NearSameDayOfYear(memDate, today, toleranceDays: 3))
                anniversaryBonus = 1.5;
            else if (memDate.Month == today.Month)
                anniversaryBonus = 0.5;
        }

        double favoriteBonus  = memory.IsFavorite ? 1.0 : 0.0;
        double recentPenalty  = recentIds.Contains(memory.Id) ? 2.0 : 0.0;

        return Math.Max(0, (baseWeight + favoriteBonus) * (1.0 + anniversaryBonus) - recentPenalty);
    }

    private static bool NearSameDayOfYear(DateOnly date, DateOnly today, int toleranceDays)
    {
        int diff = Math.Abs(date.DayOfYear - today.DayOfYear);
        return diff <= toleranceDays || diff >= 365 - toleranceDays;
    }

    // ── History persistence ───────────────────────────────────────────────────

    private static List<ShownEntry> LoadHistory()
    {
        try
        {
            var json = Preferences.Get(PrefsHistory, "[]");
            return JsonSerializer.Deserialize<List<ShownEntry>>(json) ?? new();
        }
        catch { return new(); }
    }

    /// <summary>Clears the featured clip cache so a new one is picked on next call.</summary>
    public static void InvalidateCache()
    {
        Preferences.Remove(PrefsFeaturedDate);
        Preferences.Remove(PrefsFeaturedId);
    }
}
