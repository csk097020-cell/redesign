using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MomentaryMomentos.Services;

namespace MomentaryMomentos.ViewModels;

/// <summary>
/// Backs TrimPage. Receives the raw video path and its duration via Shell
/// query parameters (IQueryAttributable), lets the user drag a start-time
/// slider, then trims to exactly 10 seconds and navigates back to
/// CapturePage with the trimmed path as a query parameter.
/// </summary>
public partial class TrimViewModel : BaseViewModel, IQueryAttributable
{
    private const double ClipSeconds = 10.0;

    private readonly IVideoTrimService _trimService;

    // ── Received from Shell navigation ──────────────────────────────────────
    [ObservableProperty] private string?  videoPath;
    [ObservableProperty] private double   totalDurationSec;

    // ── Slider binding ───────────────────────────────────────────────────────
    [ObservableProperty] private double  startSeconds = 0;
    [ObservableProperty] private double  maxStartSeconds = 0;   // totalDuration - 10

    // ── Display labels ───────────────────────────────────────────────────────
    [ObservableProperty] private string  timeRangeLabel   = "0:00 → 0:10";
    [ObservableProperty] private string  durationInfoLabel = "";

    // ── Trim progress (0–100 for ProgressBar) ───────────────────────────────
    [ObservableProperty] private double  trimProgress;
    [ObservableProperty] private bool    isTrimming;

    // ── Event so the View can seek the MediaElement when slider moves ────────
    public event Action<TimeSpan>? SeekRequested;

    public TrimViewModel(IVideoTrimService trimService)
    {
        _trimService = trimService;
    }

    // ── IQueryAttributable ───────────────────────────────────────────────────
    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("videoPath", out var vp))
            VideoPath = Uri.UnescapeDataString(vp.ToString()!);

        if (query.TryGetValue("durationSec", out var ds) &&
            double.TryParse(ds.ToString(), System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out var sec))
        {
            TotalDurationSec = sec;
        }
    }

    // ── Property-change reactions ────────────────────────────────────────────
    partial void OnTotalDurationSecChanged(double value)
    {
        MaxStartSeconds = Math.Max(0, value - ClipSeconds);
        StartSeconds    = 0;
        UpdateLabels();
    }

    partial void OnStartSecondsChanged(double value)
    {
        UpdateLabels();
        SeekRequested?.Invoke(TimeSpan.FromSeconds(value));
    }

    private void UpdateLabels()
    {
        var start  = TimeSpan.FromSeconds(StartSeconds);
        var end    = TimeSpan.FromSeconds(StartSeconds + Math.Min(ClipSeconds, TotalDurationSec));
        TimeRangeLabel    = $"{FormatTs(start)} → {FormatTs(end)}";
        DurationInfoLabel = $"10-sec clip  •  from a {FormatTs(TimeSpan.FromSeconds(TotalDurationSec))} video";
    }

    private static string FormatTs(TimeSpan ts)
        => ts.TotalMinutes >= 1
            ? $"{(int)ts.TotalMinutes}:{ts.Seconds:D2}"
            : $"0:{ts.Seconds:D2}";

    // ── Commands ─────────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task Trim()
    {
        if (string.IsNullOrEmpty(VideoPath)) return;

        IsTrimming   = true;
        TrimProgress = 0;
        ErrorMessage = null;

        try
        {
            var progressReporter = new Progress<double>(p =>
            {
                TrimProgress = p;  // 0.0 – 1.0 for ProgressBar
            });

            var trimmedPath = await _trimService.TrimVideoAsync(
                VideoPath,
                TimeSpan.FromSeconds(StartSeconds),
                TimeSpan.FromSeconds(ClipSeconds),
                progressReporter);

            if (string.IsNullOrEmpty(trimmedPath))
            {
                ErrorMessage = "Trim failed — please try a different clip.";
                return;
            }

            // The full-length import is superseded now. Nothing referenced it, so leaving it
            // behind just accumulated whole untrimmed videos in local storage.
            DiscardSource();

            // Navigate back to CapturePage and pass the trimmed file path
            await Shell.Current.GoToAsync(
                $"..?trimmedPath={Uri.EscapeDataString(trimmedPath)}");
        }
        catch (Exception ex)
        {
            ErrorMessage = "Trim failed — please try again.";
            System.Diagnostics.Debug.WriteLine($"[TrimViewModel] {ex.Message}");
        }
        finally
        {
            IsTrimming = false;
        }
    }

    [RelayCommand]
    private Task Cancel()
    {
        // Backing out abandons the import, so the copy we made has no owner.
        DiscardSource();
        return Shell.Current.GoToAsync("..");
    }

    /// <summary>
    /// Removes the local copy that was handed to this page. Safe because CaptureViewModel only
    /// routes here with a freshly imported file that no memory row points at yet.
    /// </summary>
    private void DiscardSource()
    {
        if (string.IsNullOrEmpty(VideoPath)) return;

        try
        {
            if (File.Exists(VideoPath)) File.Delete(VideoPath);
        }
        catch (Exception ex)
        {
            Diagnostics.Report(ex, "capture.trim", new Dictionary<string, string>
            {
                ["stage"]     = "discard-source",
                ["videoPath"] = VideoPath,
            });
        }
    }
}
