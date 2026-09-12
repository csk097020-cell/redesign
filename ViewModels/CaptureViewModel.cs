using System.Collections.ObjectModel;
using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MomentaryMomentos.Models;
using MomentaryMomentos.Services;
using MomentaryMomentos.Utils;

namespace MomentaryMomentos.ViewModels;

public partial class CaptureViewModel : BaseViewModel, IQueryAttributable
{
    private const double MaxClipSeconds = 10.0;
    private const long   MaxFileSizeBytes = 50L * 1024 * 1024; // 50 MB
    private readonly SupabaseService _supabase;
    private readonly SyncService _sync;
    private readonly OfflineService _offline;
    private readonly LocalStorageService _localStorage;
    private readonly UserPreferencesService _preferences;
    private readonly ColorThemeService _colorTheme;
    private readonly IVideoRecorderService? _videoRecorder;
    private readonly ISubscriptionService _subscription;
    private readonly IVideoTrimService? _videoTrim;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VideoPreviewSource))]
    private string? lastCapturedPath;

    // Returns a file:// URI string that MediaElement's TypeConverter can consume
    public string? VideoPreviewSource =>
        string.IsNullOrEmpty(LastCapturedPath) ? null : $"file://{LastCapturedPath}";

    [ObservableProperty] private string? status;
    [ObservableProperty] private bool hasVideo;
    [ObservableProperty] private bool isSaving;
    [ObservableProperty] private bool hasSelectedTags;

    // ── Step 1 progress ──────────────────────────────────────────────────────
    // Re-encoding a 4K clip takes the better part of ten seconds. Status alone couldn't report
    // it: the only Status label lives inside a card bound to HasVideo, which is still false at
    // that point, so the screen simply sat there looking broken.
    [ObservableProperty] private bool isProcessing;
    [ObservableProperty] private double processingProgress;

    [ObservableProperty] private bool facingFront;

    // ── Entry intent (FIX 1) — Home's Capture/Upload buttons carry which action the user
    // meant, so Step 1 shows only the relevant option instead of making them choose twice.
    // Null when entering via the plain "Capture" tab, which still offers both.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowBothOptions))]
    [NotifyPropertyChangedFor(nameof(ShowRecordOnly))]
    [NotifyPropertyChangedFor(nameof(ShowPickOnly))]
    private string? intent;

    public bool ShowBothOptions => Intent is null;
    public bool ShowRecordOnly => Intent == "record";
    public bool ShowPickOnly => Intent == "pick";

    private bool _autoLaunchPending;

    /// <summary>Maintained by CapturePage.OnAppearing/OnDisappearing. Lets
    /// ApplyQueryAttributes know the page's own auto-launch check has already run.</summary>
    public bool IsPageVisible { get; set; }

    // ── Memento details (entered after recording, before tagging/caption) ────
    /// <summary>Optional user title; blank falls back to an auto "Tag - Date" label on save.</summary>
    [ObservableProperty] private string? titleInput;
    /// <summary>Optional free-text caption.</summary>
    [ObservableProperty] private string? captionInput;
    /// <summary>Manual capture date. Defaults to today (or the video's metadata date when available).</summary>
    [ObservableProperty] private DateTime dateCaptured = DateTime.Today;

    /// <summary>Upper bound for the capture-date picker — a moment can't be filmed in the future.</summary>
    public DateTime MaxCaptureDate => DateTime.Today;

    public ObservableCollection<Tag> Tags { get; } = new();
    public ObservableCollection<Tag> SelectedTags { get; } = new();

    public CaptureViewModel(
        SupabaseService supabase,
        SyncService sync,
        OfflineService offline,
        LocalStorageService localStorage,
        UserPreferencesService preferences,
        ColorThemeService colorTheme,
        ISubscriptionService subscription,
        IVideoRecorderService? videoRecorder = null,
        IVideoTrimService? videoTrim = null)
    {
        _supabase = supabase;
        _sync = sync;
        _offline = offline;
        _localStorage = localStorage;
        _preferences = preferences;
        _colorTheme = colorTheme;
        _subscription = subscription;
        _videoRecorder = videoRecorder;
        _videoTrim = videoTrim;
    }

    // ── Receive trimmed path back from TrimPage, or an entry intent from Home ────────
    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("trimmedPath", out var tp))
        {
            var trimmedPath = Uri.UnescapeDataString(tp.ToString()!);
            if (!string.IsNullOrEmpty(trimmedPath) && File.Exists(trimmedPath))
                _ = AcceptTrimmedVideoAsync(trimmedPath);
        }

        if (query.TryGetValue("intent", out var intentValue))
        {
            Intent = Uri.UnescapeDataString(intentValue.ToString()!);
            if (!HasVideo)
            {
                _autoLaunchPending = true;

                // On the FIRST navigation to this tab, Shell (notably on Android) can fire
                // OnAppearing before delivering the query — the page's consume attempt has
                // already come up empty and Step 1 is showing. If the page is already
                // visible, consume and launch from here; otherwise OnAppearing will.
                // TryConsumeAutoLaunch clears the flag, so exactly one path ever fires.
                if (IsPageVisible && TryConsumeAutoLaunch(out var autoIntent))
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        if (autoIntent == "record") CaptureVideoCommand.Execute(null);
                        else if (autoIntent == "pick") PickVideoCommand.Execute(null);
                    });
                }
            }
        }
    }

    /// <summary>
    /// Takes the clip TrimPage produced. The size ceiling is enforced here rather than on the
    /// source, because trimming is exactly what brings an oversized video under the limit.
    /// </summary>
    private async Task AcceptTrimmedVideoAsync(string trimmedPath)
    {
        var ready = await PrepareForUploadAsync(trimmedPath, TrimOperation);
        if (ready is null) return;

        LastCapturedPath = ready;
        HasVideo         = true;
        Status           = "Great! Add a title and date";

        await PrefillCaptureDateAsync(ready);
        if (Tags.Count == 0)
            await LoadTagsInternalAsync();
    }

    /// <summary>
    /// Consumed once by CapturePage.OnAppearing to auto-launch the camera/picker for an
    /// intent-driven entry (FIX 1). Returns false on any subsequent appearance of this same
    /// page instance (e.g. returning from TrimPage) so the camera/picker never re-fires.
    /// </summary>
    public bool TryConsumeAutoLaunch(out string? autoLaunchIntent)
    {
        autoLaunchIntent = Intent;
        if (!_autoLaunchPending) return false;
        _autoLaunchPending = false;
        return true;
    }

    [RelayCommand]
    private Task LoadTags() => RunBusyAsync(LoadTagsInternalAsync);

    private async Task LoadTagsInternalAsync()
    {
        List<Tag> tags = new();

        // Try online first, fall back to offline cache, then mock defaults
        try
        {
            if (_supabase.IsAuthenticated)
            {
                tags = await _sync.GetTagsWithCacheAsync(_supabase.Session!.UserId);
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"LoadTags: Online fetch failed, falling back to offline: {ex.Message}");
        }

        // If online returned nothing (empty table, RLS block, or not authenticated), use offline defaults
        if (tags.Count == 0)
        {
            var offlineSession = _offline.GetMockSession();
            if (offlineSession != null)
                tags = await _offline.GetTagsAsync(offlineSession.UserId);
        }

        // Defensive display dedupe (case-insensitive) so nothing shows twice even before the
        // 004 merge migration has run. Active tags win the collapse.
        Tags.Clear();
        foreach (var t in TagText.Dedupe(tags)) Tags.Add(t);
    }

    /// <summary>
    /// Seeds the capture-date picker from the video file's creation metadata when present,
    /// otherwise today. Best-effort: uploads usually strip metadata, so today is the norm.
    /// The user can still adjust it in Step 2.
    /// </summary>
    private async Task PrefillCaptureDateAsync(string videoPath)
    {
        try
        {
            var meta = _videoTrim is not null ? await _videoTrim.GetCreationDateAsync(videoPath) : null;
            DateCaptured = meta?.LocalDateTime.Date ?? DateTime.Today;
        }
        catch
        {
            DateCaptured = DateTime.Today;
        }
    }

    [RelayCommand]
    private async Task CaptureVideo()
    {
        Status = null;
        Diagnostics.Trace(RecordOperation, "starting");

        try
        {
            string? videoPath = null;

            // Use custom recorder with 10-second limit
            if (_videoRecorder != null && _videoRecorder.IsSupported)
            {
                try
                {
                    videoPath = await _videoRecorder.CaptureVideoAsync(maxDurationSeconds: 10, facingFront: FacingFront, muteAudio: false);
                    Diagnostics.Trace(RecordOperation, $"recorder returned {videoPath ?? "null"}");
                }
                catch (PermissionException)
                {
                    throw; // handled below, with a route into system settings
                }
                catch (Exception ex)
                {
                    // Used to become videoPath = null, which the next branch read as
                    // "user cancelled" — so a real recorder failure looked like a no-op.
                    Diagnostics.Report(ex, RecordOperation, new Dictionary<string, string> { ["stage"] = "recorder" });
                    Status = null;
                    await Shell.Current.DisplayAlert("Recording failed", ex.Message, "OK");
                    return;
                }

                if (videoPath == null)
                {
                    Diagnostics.Trace(RecordOperation, "user cancelled");
                    return;
                }

                if (!File.Exists(videoPath))
                {
                    Diagnostics.Report(
                        new FileNotFoundException("Recorder reported success but wrote no file.", videoPath),
                        RecordOperation, new Dictionary<string, string> { ["stage"] = "missing-output" });
                    Status = "Recording failed. Try again.";
                    return;
                }
            }
            else if (MediaPicker.Default.IsCaptureSupported)
            {
                var result = await MediaPicker.Default.CaptureVideoAsync(new MediaPickerOptions
                {
                    Title = "Record your memory"
                });

                if (result is null)
                {
                    Diagnostics.Trace(RecordOperation, "user cancelled");
                    return;
                }

                var fileName = $"memory_{DateTimeOffset.UtcNow:yyyyMMdd_HHmmss}.mp4";
                await using (var src = await result.OpenReadAsync())
                {
                    videoPath = await _localStorage.SaveVideoAsync(fileName, src);
                }

                // Our copy is written — release the picker's staged copy (see PickVideo).
                _localStorage.DiscardPickerTempFile(result.FullPath);
            }
            else
            {
                Status = "Camera not available";
                return;
            }

            if (string.IsNullOrWhiteSpace(videoPath))
            {
                Diagnostics.Trace(RecordOperation, "no path produced");
                return;
            }

            // ── Same gates the pick path enforces, in the same order: trim down to 10 seconds
            //    first, then check what is actually going to be uploaded. The native Android
            //    recorder treats EXTRA_DURATION_LIMIT as a hint that many OEM camera apps
            //    ignore, so a long high-quality clip can land here unchecked. ──
            if (await RouteToTrimIfLongAsync(videoPath, RecordOperation)) return;

            var ready = await PrepareForUploadAsync(videoPath, RecordOperation);
            if (ready is null) return;

            LastCapturedPath = ready;
            HasVideo = true;
            Status = "Great! Add a title and date";
            await PrefillCaptureDateAsync(ready);

            // Auto-load tags
            if (Tags.Count == 0)
                await LoadTagsInternalAsync();
        }
        catch (PermissionException ex)
        {
            Diagnostics.Report(ex, RecordOperation, new Dictionary<string, string> { ["stage"] = "permission" });
            Status = null;
            var openSettings = await Shell.Current.DisplayAlert(
                "Camera access needed",
                "Momentary Momentos needs camera and microphone permission to record a momento.",
                "Open Settings", "Not Now");
            if (openSettings)
                AppInfo.Current.ShowSettingsUI();
        }
        catch (Exception ex)
        {
            Diagnostics.Report(ex, RecordOperation, new Dictionary<string, string> { ["stage"] = "capture" });
            Status = null;
            await Shell.Current.DisplayAlert("Recording failed", ex.Message, "OK");
        }

        Diagnostics.Trace(RecordOperation, $"completed, HasVideo={HasVideo}");
    }

    /// <summary>
    /// Brings an oversized clip under the 50 MB limit by re-encoding it to 1080p, and rejects it
    /// with an alert only if that still isn't enough. Returns the path to use — which may be a
    /// new compressed file — or null when the caller should stop.
    ///
    /// Always run this *after* trimming: a 10-second clip is what gets uploaded, so that is what
    /// the limit should apply to.
    /// </summary>
    private async Task<string?> PrepareForUploadAsync(string videoPath, string operation)
    {
        var fileSize = new FileInfo(videoPath).Length;
        Diagnostics.Trace(operation, $"file is {fileSize:N0} bytes");

        // Trim and compress both write to the cache directory, which Android may purge before a
        // queued offline upload ever runs. Whatever we hand back has to be somewhere permanent.
        if (fileSize <= MaxFileSizeBytes)
            return _localStorage.MoveIntoVideos(videoPath);

        // A 10-second 4K clip is ~85 MB purely because phone cameras record at editing bitrates.
        // Re-encode it rather than turning the user away — trimming cannot help a short clip.
        var compressed = await TryCompressAsync(videoPath, operation);
        var didCompress = false;

        if (compressed is not null)
        {
            var compressedSize = new FileInfo(compressed).Length;
            Diagnostics.Trace(operation, $"compressed to {compressedSize:N0} bytes");

            if (compressedSize <= MaxFileSizeBytes)
            {
                // The source copy is dead weight once we have a smaller re-encode.
                TryDeleteLocal(videoPath);
                return _localStorage.MoveIntoVideos(compressed);
            }

            TryDeleteLocal(compressed);
            fileSize    = compressedSize;
            didCompress = true;
        }

        TryDeleteLocal(videoPath);

        // Don't claim we shrank it when compression never ran — that told the user the clip was
        // "still 79 MB after shrinking" when the number hadn't moved because nothing happened.
        var sizeText = didCompress
            ? $"That clip is still {fileSize / (1024.0 * 1024.0):F0} MB after shrinking it"
            : $"That clip is {fileSize / (1024.0 * 1024.0):F0} MB and couldn't be shrunk on this device";

        Status = null;
        await Shell.Current.DisplayAlert(
            "Video is too large",
            $"{sizeText}, and the limit is 50 MB. Try a shorter clip, or one recorded at a lower resolution.",
            "OK");
        return null;
    }

    /// <summary>
    /// Re-encodes an oversized clip to 1080p. Returns null when compression is unavailable or
    /// fails, in which case the caller falls back to judging the original file.
    /// </summary>
    private async Task<string?> TryCompressAsync(string videoPath, string operation)
    {
        if (_videoTrim is null) return null;

        Status             = "Shrinking video…";
        ProcessingProgress = 0;
        IsProcessing       = true;

        // The encoder reports from a background thread; hop to the UI thread to update bindings.
        var reporter = new Progress<double>(p =>
            MainThread.BeginInvokeOnMainThread(() => ProcessingProgress = p));

        try
        {
            return await _videoTrim.CompressVideoAsync(videoPath, reporter);
        }
        catch (Exception ex)
        {
            Diagnostics.Report(ex, operation, new Dictionary<string, string> { ["stage"] = "compress" });
            return null;
        }
        finally
        {
            IsProcessing = false;
        }
    }

    private static void TryDeleteLocal(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { /* best effort */ }
    }

    /// <summary>
    /// Sends clips longer than 10 s to TrimPage, which returns the trimmed path through
    /// <see cref="ApplyQueryAttributes"/>. Returns true when navigation happened and the
    /// caller should stop.
    /// </summary>
    private async Task<bool> RouteToTrimIfLongAsync(string videoPath, string operation)
    {
        if (_videoTrim is null) return false;

        Status = "Checking length…";
        var duration = await _videoTrim.GetDurationAsync(videoPath);
        Diagnostics.Trace(operation, $"duration = {duration.TotalSeconds:F1}s");

        if (duration.TotalSeconds <= MaxClipSeconds + 0.5) // 0.5s tolerance
            return false;

        Status = null;
        await Shell.Current.GoToAsync(
            $"trim?videoPath={Uri.EscapeDataString(videoPath)}" +
            $"&durationSec={duration.TotalSeconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)}");
        return true;
    }

    [RelayCommand]
    private async Task PickVideo()
    {
        Status = null;
        Diagnostics.Trace(PickOperation, "starting");

        string? localPath = null;
        try
        {
            var result = await MediaPicker.Default.PickVideoAsync();
            if (result is null)
            {
                Diagnostics.Trace(PickOperation, "user cancelled");
                return;
            }

            Diagnostics.Trace(PickOperation, $"selected {result.FileName}");

            // ── Save locally first so we have a real file path ──────────────
            var fileName = $"memory_{DateTimeOffset.UtcNow:yyyyMMdd_HHmmss}.mp4";
            await using (var src = await result.OpenReadAsync())
            {
                localPath = await _localStorage.SaveVideoAsync(fileName, src);
            }

            // The picker staged its own copy in the cache to hand us a real path. We own a copy
            // now, so that one is pure leak — ~80 MB per pick if left behind.
            _localStorage.DiscardPickerTempFile(result.FullPath);

            Diagnostics.Trace(PickOperation, $"saved to {localPath}");

            // Trim first, then size. The other order rejected long videos for being too large
            // when shortening them to 10 seconds is precisely what makes them small — a 79 MB
            // phone clip becomes a few MB once it is 10 seconds long.
            if (await RouteToTrimIfLongAsync(localPath, PickOperation)) return;

            var ready = await PrepareForUploadAsync(localPath, PickOperation);
            if (ready is null) return;

            LastCapturedPath = ready;
            HasVideo         = true;
            Status           = "Great! Add a title and date";
            await PrefillCaptureDateAsync(ready);

            if (Tags.Count == 0)
                await LoadTagsInternalAsync();
        }
        catch (PermissionException ex)
        {
            Diagnostics.Report(ex, PickOperation, new Dictionary<string, string> { ["stage"] = "permission" });
            await ShowMediaPermissionAlertAsync();
        }
        catch (Exception ex)
        {
            Diagnostics.Report(ex, PickOperation, new Dictionary<string, string>
            {
                ["localPath"] = localPath ?? "(none)",
                ["stage"]     = localPath is null ? "picker" : "post-save",
            });
            Status = null;
            await Shell.Current.DisplayAlert(
                "Couldn't load that video",
                $"Something went wrong reading the clip: {ex.Message}",
                "OK");
        }

        Diagnostics.Trace(PickOperation, $"completed, HasVideo={HasVideo}");
    }

    private const string PickOperation    = "capture.pick";
    private const string RecordOperation  = "capture.record";
    private const string TrimOperation    = "capture.trim";

    /// <summary>
    /// Android 14+ lets the user grant partial media access ("Select photos and videos"),
    /// which *denies* READ_MEDIA_VIDEO outright. Nothing the app does at runtime can recover
    /// from that — the user has to change it in system settings, so point them there.
    /// </summary>
    private static async Task ShowMediaPermissionAlertAsync()
    {
        var openSettings = await Shell.Current.DisplayAlert(
            "Photo & video access needed",
            "Momentary Momentos needs permission to see all your photos and videos in order to " +
            "upload one. If you chose \"Select photos and videos\", please switch it to \"Allow all\".",
            "Open Settings", "Not Now");

        if (openSettings)
            AppInfo.Current.ShowSettingsUI();
    }

    /// <summary>
    /// User taps an emotion - immediately add and save if ready
    /// </summary>
    [RelayCommand]
    private async Task SelectEmotion(Tag tag)
    {
        if (!HasVideo || string.IsNullOrEmpty(LastCapturedPath)) return;

        // Toggle selection
        var existing = SelectedTags.FirstOrDefault(t => t.Id == tag.Id);
        if (existing != null)
        {
            SelectedTags.Remove(existing);
            existing.IsSelected = false;
        }
        else
        {
            SelectedTags.Add(tag);
            tag.IsSelected = true;
        }

        // Update HasSelectedTags for button visibility
        HasSelectedTags = SelectedTags.Count > 0;
        OnPropertyChanged(nameof(SelectedTags));

        // Update visual feedback
        if (SelectedTags.Count > 0)
        {
            Status = "Tap Save to preserve this moment!";
        }
        else
        {
            Status = "Tap how you feel";
        }
    }

    /// <summary>Ensure a tag is selected (used when a "create" resolves to an existing tag).</summary>
    private void SelectTag(Tag tag)
    {
        if (SelectedTags.All(t => t.Id != tag.Id))
        {
            SelectedTags.Add(tag);
            tag.IsSelected = true;
        }
        HasSelectedTags = SelectedTags.Count > 0;
        OnPropertyChanged(nameof(SelectedTags));
        if (SelectedTags.Count > 0)
            Status = "Tap Save to preserve this moment!";
    }

    [RelayCommand]
    private async Task CreateCustomTag()
    {
        var name = await Shell.Current.DisplayPromptAsync(
            "New Tag", "Name your tag:", placeholder: "e.g. Grateful", maxLength: 30);
        if (string.IsNullOrWhiteSpace(name)) return;

        var normalized = TagText.Normalize(name);

        // Don't create a second tag that already exists (case-insensitive) — select it instead.
        var existing = Tags.FirstOrDefault(t => TagText.SameName(t.Name, normalized));
        if (existing is not null)
        {
            SelectTag(existing);
            _ = Toast.Make($"\"{existing.Name}\" already exists — selected it for you.").Show();
            return;
        }

        var color = TagPalette.NextColor(Tags.Select(t => t.Color));

        try
        {
            var userId = _supabase.IsAuthenticated ? _supabase.Session!.UserId : null;
            var newTag = await _supabase.CreateTagAsync(new Tag
            {
                Id       = Guid.NewGuid().ToString(),
                Name     = normalized,
                Color    = color,
                // Icon keeps its model default ('✨'); emoji is no longer part of a tag.
                IsActive = true,
                UserId   = userId
            });
            Tags.Add(newTag);
            // Auto-apply the new tag to the memory being created (FIX 1) — the user can tap
            // it again to deselect if they don't want it on this memory.
            SelectTag(newTag);
            _ = Toast.Make($"Tag \"{newTag.Name}\" created & applied.").Show();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"CreateCustomTag: {ex.Message}");
            Status = "Couldn't create tag. Try again.";
        }
    }

    [RelayCommand]
    private async Task SaveMemory()
    {
        if (!HasVideo || SelectedTags.Count == 0) 
        {
            Status = "Select at least one feeling";
            return;
        }

        IsSaving = true;
        Status = "Saving your memory...";

        try
        {
            // User-entered title, or an auto "Emotion - Date" label when left blank.
            var title = string.IsNullOrWhiteSpace(TitleInput)
                ? $"{SelectedTags.First().Name} - {DateTime.Now:MMM d}"
                : TitleInput.Trim();

            var caption = string.IsNullOrWhiteSpace(CaptionInput) ? null : CaptionInput.Trim();

            // Get user ID (from Supabase if authenticated, otherwise offline)
            string userId;
            if (_supabase.IsAuthenticated)
            {
                userId = _supabase.Session!.UserId;
            }
            else
            {
                var offlineSession = _offline.GetMockSession();
                userId = offlineSession?.UserId ?? "local-user";
            }

            // Check free-tier video limit before saving
            if (!await _subscription.CanUploadVideoAsync(userId))
            {
                IsSaving = false;
                var upgrade = await Shell.Current.DisplayAlert(
                    "Storage Limit Reached",
                    "You've used all your free video slots. Upgrade to Premium for 1,000 momentos.",
                    "Upgrade ✨", "Not Now");
                if (upgrade)
                    await Shell.Current.GoToAsync("subscription");
                return;
            }

            // Create memory object.
            // CreatedAt is the automatic upload timestamp ("Date Uploaded"); the user-entered
            // DateCaptured carries when the moment was actually filmed ("Date Captured").
            var memory = new Memory
            {
                Id = Guid.NewGuid().ToString(),
                UserId = userId,
                Title = title,
                Caption = caption,
                VideoUrl = null, // Will be set after upload
                LocalVideoPath = LastCapturedPath,
                Tags = SelectedTags.Select(t => t.Id).ToList(),
                IsFavorite = false,
                CreatedAt = DateTimeOffset.UtcNow,
                DateCaptured = DateOnly.FromDateTime(DateCaptured)
            };

            // Generate the thumbnail now, from the local file (guaranteed present here),
            // so the new memento previews immediately instead of waiting for a later
            // Home/Relive backfill pass. GetThumbnailAsync is non-fatal (returns null on error).
            if (LastCapturedPath is not null && _videoTrim is not null)
            {
                var thumb = await _videoTrim.GetThumbnailAsync(LastCapturedPath);
                if (!string.IsNullOrEmpty(thumb)) memory.LocalThumbnailPath = thumb;
            }

            // Use SyncService to save (handles online/offline automatically).
            // This may rewrite memory.Id to the remote id when online.
            var saveResult = await _sync.SaveMemoryAsync(memory, LastCapturedPath);

            // Persist the local thumbnail under the final id and upload it (background, non-fatal)
            // so the preview propagates cross-device.
            if (!string.IsNullOrEmpty(memory.LocalThumbnailPath))
            {
                await _sync.UpdateLocalThumbnailAsync(memory.Id, memory.LocalThumbnailPath);
                _ = _sync.TryUploadThumbnailAsync(memory, memory.LocalThumbnailPath);
            }

            FeaturedClipService.InvalidateCache();

            // Only promise a later sync when we were genuinely offline. An upload that failed
            // while online used to produce the same reassuring message, hiding 413s and timeouts.
            if (saveResult.UploadedToCloud)
            {
                _ = Toast.Make("Momento saved to cloud! 🎉").Show();
            }
            else if (saveResult.WasOffline)
            {
                _ = Toast.Make("Momento saved! Will sync when online.").Show();
            }
            else
            {
                await Shell.Current.DisplayAlert(
                    "Saved, but the upload didn't finish",
                    "Your momento is safe on this device and we'll keep retrying. " +
                    $"The upload failed with: {saveResult.UploadError?.Message ?? "an unknown error"}",
                    "OK");
            }

            Status = null;

            ClearForm();
            await Shell.Current.GoToAsync("//main/home");
        }
        catch (Exception ex)
        {
            Diagnostics.Report(ex, "capture.save");
            Status = "❌ Couldn't save. Try again.";
        }
        finally
        {
            IsSaving = false;
        }
    }

    [RelayCommand]
    private async Task StartOver()
    {
        // Confirm before discarding a recorded-but-unsaved memory (FIX 3).
        if (HasVideo)
        {
            var ok = await Shell.Current.DisplayAlert(
                "Start over?",
                "This discards your recorded video and any title, caption, and tags for this memory.",
                "Discard", "Keep");
            if (!ok) return;
        }

        ClearForm();
    }

    private void ClearForm()
    {
        foreach (var t in SelectedTags) t.IsSelected = false;
        LastCapturedPath = null;
        HasVideo = false;
        HasSelectedTags = false;
        SelectedTags.Clear();
        TitleInput = null;
        CaptionInput = null;
        DateCaptured = DateTime.Today;
        Status = null;
    }
}
