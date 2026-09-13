using System.Collections.ObjectModel;
using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MomentaryMomentos.Models;
using MomentaryMomentos.Services;
using MomentaryMomentos.Utils;

namespace MomentaryMomentos.ViewModels;

public partial class HomeViewModel : BaseViewModel
{
    private readonly SupabaseService _supabase;
    private readonly SecureTokenStore _tokens;
    private readonly SyncService _sync;
    private readonly OfflineService _offline;
    private readonly UserPreferencesService _preferences;
    private readonly ConnectivityService _connectivity;
    private readonly FeaturedClipService _featuredClip;
    private readonly IAppNotifications _notifications;
    private readonly IVideoTrimService? _videoTrim;

    [ObservableProperty] private int totalMemories;
    [ObservableProperty] private int favorites;
    [ObservableProperty] private int thisWeek;
    [ObservableProperty] private string storageEstimate = "0 MB";
    [ObservableProperty] private string trend = "+0%";
    [ObservableProperty] private int categories;
    [ObservableProperty] private string connectionStatus = "Offline";
    [ObservableProperty] private bool isOfflineMode = true;
    // Drives the header indicator's visibility: shown only when offline, hidden when connected.
    [ObservableProperty] private bool isOffline = true;
    [ObservableProperty] private bool hasMemories;
    [ObservableProperty] private bool hasNoMemories = true;

    // ── Featured clip of the day ───────────────────────────────────────────────
    [ObservableProperty] private Memory? featuredMemory;
    [ObservableProperty] private string  featuredSubtitle = "";
    [ObservableProperty] private bool    hasFeaturedMemory;

    // ── Tag distribution (#11) ────────────────────────────────────────────────
    // Populated during Refresh so ShowCategoriesCommand can display breakdown
    private List<(string Name, string Color, int Count)> _tagCounts = new();

    public ObservableCollection<Memory> RecentMemories { get; } = new();

    public HomeViewModel(
        SupabaseService supabase,
        SecureTokenStore tokens,
        SyncService sync,
        OfflineService offline,
        UserPreferencesService preferences,
        ConnectivityService connectivity,
        FeaturedClipService featuredClip,
        IAppNotifications notifications,
        IVideoTrimService? videoTrim = null)
    {
        _supabase      = supabase;
        _tokens        = tokens;
        _sync          = sync;
        _offline       = offline;
        _preferences   = preferences;
        _connectivity  = connectivity;
        _featuredClip  = featuredClip;
        _notifications = notifications;
        _videoTrim     = videoTrim;

        IsOfflineMode = _preferences.OfflineModeEnabled;
        UpdateConnectionStatus();
    }

    [RelayCommand]
    private async Task Refresh()
    {
        await RunBusyAsync(async () =>
        {
            var session = _supabase.Session ?? _offline.GetMockSession();
            if (session == null)
                throw new InvalidOperationException("Not logged in.");

            List<Memory> memories;
            List<Models.Tag> tags = new();
            Dictionary<string, int> weights = new();

            if (IsOfflineMode)
            {
                memories = await _offline.GetMemoriesAsync(session.UserId);
                tags     = await _offline.GetTagsAsync(session.UserId);
            }
            else
            {
                var memoriesTask = _sync.GetMemoriesWithCacheAsync(session.UserId);
                // Include inactive tags so chips for deactivated (legacy) tags still resolve on existing memories.
                var tagsTask     = _sync.GetTagsWithCacheAsync(session.UserId, includeInactive: true);
                var weightsTask  = _supabase.GetMemoryWeightsAsync(session.UserId);
                memories = await memoriesTask;
                tags     = await tagsTask;
                weights  = await weightsTask;
            }

            // Resolve tag IDs → display info for thumbnail cards
            var tagLookup = tags.ToDictionary(t => t.Id, t => t);
            foreach (var m in memories)
            {
                m.DisplayTags = m.Tags
                    .Select(id => tagLookup.TryGetValue(id, out var t)
                        ? new Models.TagInfo(t.Name, t.Color, t.Icon)
                        : null)
                    .OfType<Models.TagInfo>()
                    .ToList();
            }

            // Backfill thumbnails for any memory that has a local video but no thumbnail yet
            await BackfillMissingThumbnailsAsync(memories);

            UpdateStats(memories, tags);

            // Momentos paused by a hidden tag (#8) stay in the stats but never surface
            // on the featured card or the recent list until the tag is restored.
            var visibleMemories = MemoryVisibility.ExcludePaused(memories, tags);

            // ── Featured clip of the day ──────────────────────────────────────
            var featured = _featuredClip.GetFeaturedClip(visibleMemories, weights);
            FeaturedMemory    = featured;
            FeaturedSubtitle  = featured is not null ? FeaturedClipService.GetSubtitle(featured) : "";
            HasFeaturedMemory = featured is not null;

            // Recent memories excludes the featured clip (it already has its own card)
            RecentMemories.Clear();
            foreach (var m in visibleMemories.Where(m => m.Id != featured?.Id).Take(3))
                RecentMemories.Add(m);

            HasMemories    = memories.Count > 0;
            HasNoMemories  = memories.Count == 0;

            UpdateConnectionStatus();

            // ── Schedule notifications ────────────────────────────────────────
            await ScheduleNotificationsAsync(memories, tags, featured);
        });
    }

    [RelayCommand]
    private async Task ShowCategories()
    {
        if (_tagCounts.Count == 0)
        {
            _ = Toast.Make("No memories yet to show categories.").Show();
            return;
        }

        var lines = _tagCounts
            .OrderByDescending(t => t.Count)
            .Select(t => $"{t.Name}  ×{t.Count}");
        await Shell.Current.DisplayAlert(
            $"Tag Breakdown  ({_tagCounts.Count} categories)",
            string.Join("\n", lines),
            "Close");
    }

    [RelayCommand]
    private async Task PlayFeatured()
    {
        if (FeaturedMemory is null) return;
        var userId = _supabase.Session?.UserId ?? "";
        await Shell.Current.GoToAsync(
            $"memorydetail?memoryId={Uri.EscapeDataString(FeaturedMemory.Id)}" +
            $"&userId={Uri.EscapeDataString(userId)}");
    }

    [RelayCommand]
    private async Task SignOut()
    {
        if (!IsOfflineMode)
            _supabase.SignOut();

        _notifications.CancelAll();
        _tokens.Clear();
        await Shell.Current.GoToAsync("//login");
    }

    private async Task ScheduleNotificationsAsync(List<Memory> memories, List<Models.Tag> tags, Memory? featured)
    {
        try
        {
            // Every scheduler below cancels and restarts its countdown from now. Running that on
            // each Home load meant a user who opens the app daily reset the 3-day engagement
            // reminder every day, so it could never fire. Once per day keeps the content fresh
            // without ever rewinding a pending reminder.
            if (_preferences.NotificationsScheduledOn == DateTime.Today)
                return;


            // Determine last capture time (most recent memory)
            var lastCapture = memories.Count > 0
                ? memories.Max(m => m.CreatedAt)
                : DateTimeOffset.Now.AddYears(-1); // force engagement schedule if no memories

            // Top tag by frequency
            var topTagId = memories
                .SelectMany(m => m.Tags)
                .GroupBy(id => id)
                .OrderByDescending(g => g.Count())
                .FirstOrDefault()?.Key ?? "";
            var topTagName = tags.FirstOrDefault(t => t.Id == topTagId)?.Name ?? "";

            await _notifications.ScheduleEngagementRemindersAsync(lastCapture);
            await _notifications.ScheduleWeeklyInsightAsync(memories.Count, ThisWeek, topTagName);
            await _notifications.ScheduleDailyChallengesAsync();

            if (featured is not null)
                await _notifications.ScheduleMemoryFlashbackAsync(featured.Title, FeaturedSubtitle);

            _preferences.NotificationsScheduledOn = DateTime.Today;
        }
        catch (Exception ex)
        {
            Diagnostics.Report(ex, "home.schedule-notifications");
        }
    }

    private void UpdateStats(List<Memory> memories, List<Models.Tag> tags)
    {
        TotalMemories = memories.Count;
        Favorites = memories.Count(m => m.IsFavorite);

        var oneWeekAgo = DateTimeOffset.UtcNow.AddDays(-7);
        var twoWeeksAgo = DateTimeOffset.UtcNow.AddDays(-14);
        ThisWeek = memories.Count(m => m.CreatedAt > oneWeekAgo);
        var lastWeek = memories.Count(m => m.CreatedAt > twoWeeksAgo && m.CreatedAt <= oneWeekAgo);

        var t = lastWeek > 0 ? (int)Math.Round(((double)(ThisWeek - lastWeek) / lastWeek) * 100.0) : (ThisWeek > 0 ? 100 : 0);
        Trend = t >= 0 ? $"+{t}%" : $"{t}%";

        // Tag distribution for #11 Categories modal
        var tagLookup = tags.ToDictionary(t2 => t2.Id, t2 => t2);
        var countMap = new Dictionary<string, int>();
        foreach (var m in memories)
            foreach (var id in m.Tags ?? new())
                countMap[id] = countMap.GetValueOrDefault(id, 0) + 1;

        _tagCounts = countMap
            .Select(kv => (
                Name:  tagLookup.TryGetValue(kv.Key, out var tg) ? tg.Name : kv.Key,
                Color: tagLookup.TryGetValue(kv.Key, out var tg2) ? tg2.Color : "#3B82F6",
                Count: kv.Value))
            .ToList();

        Categories = _tagCounts.Count;

        var storageInMb = TotalMemories * 2.5;
        StorageEstimate = storageInMb > 1000 ? $"{storageInMb / 1000:0.0} GB" : $"{storageInMb:0} MB";
    }
    
    /// <summary>
    /// Re-evaluates connectivity for the header indicator. Called by the page on
    /// ConnectivityChanged so the indicator appears/disappears while the page is open.
    /// </summary>
    public void RefreshConnectionStatus() => UpdateConnectionStatus();

    private void UpdateConnectionStatus()
    {
        if (IsOfflineMode)
        {
            ConnectionStatus = "[Offline Mode] Data saved locally";
            IsOffline = true;
        }
        else if (_connectivity.IsConnected)
        {
            // Connected is the normal state \u2014 don't announce it (indicator stays hidden).
            ConnectionStatus = $"Connected via {_connectivity.ConnectionDescription}";
            IsOffline = false;
        }
        else
        {
            ConnectionStatus = "Offline \u2014 memories will sync when reconnected";
            IsOffline = true;
        }
    }

    private async Task BackfillMissingThumbnailsAsync(List<Memory> memories)
    {
        if (_videoTrim is null) return;

        // Thumbnails needing a cross-device upload this pass, run off the critical path after render.
        var toUpload = new List<(Memory Memory, string ThumbPath)>();

        foreach (var m in memories)
        {
            if (!string.IsNullOrEmpty(m.ThumbnailUrl)) continue;   // already propagated remotely

            var thumbPath = m.LocalThumbnailPath;

            // Generate locally only if we don't already have a usable thumbnail file on disk.
            if (string.IsNullOrEmpty(thumbPath) || !File.Exists(thumbPath))
            {
                if (string.IsNullOrEmpty(m.LocalVideoPath) || !File.Exists(m.LocalVideoPath)) continue;
                try
                {
                    thumbPath = await _videoTrim.GetThumbnailAsync(m.LocalVideoPath);
                    if (string.IsNullOrEmpty(thumbPath)) continue;
                    m.LocalThumbnailPath = thumbPath;
                    await _sync.UpdateLocalThumbnailAsync(m.Id, thumbPath);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[HomeViewModel] BackfillThumbnail: {ex.Message}");
                    continue;
                }
            }

            // Local thumbnail is generated for display regardless of sync state; only queue the
            // cross-device upload for synced rows (local-only/pending rows have no remote row to PATCH).
            if (m.IsSynced)
                toUpload.Add((m, thumbPath));
        }

        // Upload sequentially in the background so the page render isn't blocked by network.
        if (toUpload.Count > 0)
            _ = Task.Run(async () =>
            {
                foreach (var (m, p) in toUpload)
                    await _sync.TryUploadThumbnailAsync(m, p);
            });
    }
}
