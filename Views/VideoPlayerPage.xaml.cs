using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using CommunityToolkit.Maui.Views;
using MomentaryMomentos.Services;
using MomentaryMomentos.Utils;

namespace MomentaryMomentos.Views;

[QueryProperty(nameof(VideoUrl),   "url")]
[QueryProperty(nameof(Title),      "title")]
[QueryProperty(nameof(RecordedAt), "recordedAt")]
[QueryProperty(nameof(MemoryId),   "memoryId")]
[QueryProperty(nameof(IsFavorite), "isFavorite")]
public partial class VideoPlayerPage : ContentPage
{
    private readonly SupabaseService _supabase;
    private bool _isClosing;

    private string? _videoUrl;
    public string? VideoUrl
    {
        get => _videoUrl;
        set
        {
            _videoUrl = value;
            PlayerSource = CreateMediaSource(value);
            OnPropertyChanged();
        }
    }

    private MediaSource? _playerSource;
    public MediaSource? PlayerSource
    {
        get => _playerSource;
        private set { _playerSource = value; OnPropertyChanged(); }
    }

    private string? _title;
    public new string? Title
    {
        get => _title;
        set { _title = value; OnPropertyChanged(); }
    }

    private string? _memoryId;
    public string? MemoryId
    {
        get => _memoryId;
        set { _memoryId = value; OnPropertyChanged(); }
    }

    private bool _isFavorite;
    public bool IsFavorite
    {
        get => _isFavorite;
        set
        {
            _isFavorite = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(FavoriteIcon));
        }
    }

    public string FavoriteIcon => IsFavorite ? "⭐" : "☆";

    public VideoPlayerPage(SupabaseService supabase)
    {
        InitializeComponent();
        _supabase = supabase;
        BindingContext = this;
    }

    private string? _recordedAt;
    public string? RecordedAt
    {
        get => _recordedAt;
        set
        {
            _recordedAt = value;
            RelativeRecordedAt = BuildRelativeRecordedAt(value);
            OnPropertyChanged();
        }
    }

    private string? _relativeRecordedAt;
    public string? RelativeRecordedAt
    {
        get => _relativeRecordedAt;
        private set
        {
            _relativeRecordedAt = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasRelativeRecordedAt));
        }
    }

    public bool HasRelativeRecordedAt => !string.IsNullOrWhiteSpace(RelativeRecordedAt);

    private async void OnMediaEnded(object sender, EventArgs e)
        => await CloseAsync();

    private async void OnMediaFailed(object sender, MediaFailedEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine($"Video playback failed: {e.ErrorMessage}");
        await CloseAsync();
    }

    protected override void OnDisappearing()
    {
        StopPlayer();
        base.OnDisappearing();
    }

    public Command GoBackCommand => new(async () => await CloseAsync());

    public Command ToggleFavoriteCommand => new(async () =>
    {
        if (string.IsNullOrEmpty(MemoryId)) return;
        try
        {
            var newState = !IsFavorite;
            await _supabase.UpdateFavoriteAsync(MemoryId, newState);
            IsFavorite = newState;
            _ = Toast.Make(newState ? "Added to favorites ⭐" : "Removed from favorites").Show();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ToggleFavorite failed: {ex.Message}");
            _ = Toast.Make("Couldn't update favorite. Try again.").Show();
        }
    });

    private static MediaSource? CreateMediaSource(string? source)
    {
        if (string.IsNullOrWhiteSpace(source))
            return null;

        if (Uri.TryCreate(source, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            return MediaSource.FromUri(uri);
        }

        return File.Exists(source)
            ? MediaSource.FromFile(source)
            : null;
    }

    private async Task CloseAsync()
    {
        if (_isClosing)
            return;

        _isClosing = true;
        StopPlayer();

        try
        {
            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Video close failed: {ex}");
        }
    }

    private void StopPlayer()
    {
        try
        {
            Player?.Stop();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Video stop failed: {ex.Message}");
        }
    }

    private static string? BuildRelativeRecordedAt(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        if (!DateTimeOffset.TryParse(Uri.UnescapeDataString(raw), out var recorded))
            return null;

        return RelativeTimeFormatter.FormatRecordedAgo(recorded);
    }
}
