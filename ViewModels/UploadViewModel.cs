using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MomentaryMomentos.Models;
using MomentaryMomentos.Services;

namespace MomentaryMomentos.ViewModels;

public partial class UploadViewModel : BaseViewModel
{
    private readonly SupabaseService _supabase;
    private readonly SyncService _sync;
    private readonly ISubscriptionService _subscription;
    private readonly IVideoTrimService? _videoTrim;

    [ObservableProperty] private string? pickedPath;
    [ObservableProperty] private string title = "";
    [ObservableProperty] private string? status;

    public ObservableCollection<Tag> Tags { get; } = new();
    public ObservableCollection<Tag> SelectedTags { get; } = new();

    public UploadViewModel(SupabaseService supabase, SyncService sync, ISubscriptionService subscription,
        IVideoTrimService? videoTrim = null)
    {
        _supabase = supabase;
        _sync = sync;
        _subscription = subscription;
        _videoTrim = videoTrim;
    }

    [RelayCommand]
    private async Task LoadTags()
    {
        await RunBusyAsync(async () =>
        {
            var session = _supabase.Session ?? throw new InvalidOperationException("Not logged in.");
            var tags = await _sync.GetTagsWithCacheAsync(session.UserId);
            Tags.Clear();
            foreach (var t in tags) Tags.Add(t);
        });
    }

    [RelayCommand]
    private void ToggleTag(Tag tag)
    {
        if (SelectedTags.Any(t => t.Id == tag.Id))
            SelectedTags.Remove(SelectedTags.First(t => t.Id == tag.Id));
        else
            SelectedTags.Add(tag);
    }

    [RelayCommand]
    private async Task PickVideo()
    {
        await RunBusyAsync(async () =>
        {
            Status = null;

            var result = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Select a video",
                FileTypes = FilePickerFileType.Videos
            });

            if (result is null) return;

            var localPath = Path.Combine(FileSystem.CacheDirectory, result.FileName);
            await using (var src = await result.OpenReadAsync())
            await using (var dst = File.OpenWrite(localPath))
                await src.CopyToAsync(dst);

            PickedPath = localPath;
            if (string.IsNullOrWhiteSpace(Title)) Title = Path.GetFileNameWithoutExtension(localPath);
            Status = "Video selected. Choose tags and upload.";
        });
    }

    [RelayCommand]
    private async Task Upload()
    {
        await RunBusyAsync(async () =>
        {
            var session = _supabase.Session ?? throw new InvalidOperationException("Not logged in.");

            if (string.IsNullOrWhiteSpace(PickedPath) || !File.Exists(PickedPath))
                throw new InvalidOperationException("Pick a video first.");

            if (SelectedTags.Count == 0)
                throw new InvalidOperationException("Select at least one tag.");

            // CONTRACT REQUIREMENT: Check storage limits (Section 2.6)
            // APPLICATION-SPECIFIC CODE - Property of Client (Corinne Kelley)
            var canUpload = await _subscription.CanUploadVideoAsync(session.UserId);

            if (!canUpload)
            {
                var remaining = await _subscription.GetRemainingVideoSlotsAsync(session.UserId);
                if (remaining <= 0)
                {
                    var upgrade = await Shell.Current.DisplayAlert(
                        "Storage Limit Reached",
                        "You've reached your free storage limit. Upgrade to Premium for 1,000 momentos!",
                        "Upgrade Now",
                        "Cancel");

                    if (upgrade)
                    {
                        await Shell.Current.GoToAsync("subscription");
                    }

                    return;
                }
            }

            var contentType = "video/mp4";
            Status = "Uploading…";
            var publicUrl = await _supabase.UploadVideoAsync(session.UserId, PickedPath, contentType);

            var videoCreatedAt = PickedPath is not null && _videoTrim is not null
                ? await _videoTrim.GetCreationDateAsync(PickedPath) ?? DateTimeOffset.UtcNow
                : DateTimeOffset.UtcNow;

            var memory = new Memory
            {
                UserId = session.UserId,
                Title = string.IsNullOrWhiteSpace(Title) ? $"{SelectedTags[0].Name} Memory" : Title.Trim(),
                VideoUrl = publicUrl,
                Tags = SelectedTags.Select(t => t.Id).ToList(),
                CreatedAt = videoCreatedAt
            };

            await _supabase.InsertMemoryAsync(memory);
            Status = "Uploaded ✔";
            PickedPath = null;
            SelectedTags.Clear();
            Title = "";
        });
    }
}
