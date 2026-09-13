using MomentaryMomentos.ViewModels;

namespace MomentaryMomentos.Views;

public partial class TrimPage : ContentPage
{
    private readonly TrimViewModel _vm;
    private bool _isPlaying;

    public TrimPage(TrimViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;

        // When the ViewModel signals a seek (slider moved), update the player
        _vm.SeekRequested += OnSeekRequested;
    }

    // ── Lifecycle ────────────────────────────────────────────────────────────

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        // Stop playback and free MediaElement resources when leaving the page
        VideoPreview.Stop();
        VideoPreview.Handler?.DisconnectHandler();
        _vm.SeekRequested -= OnSeekRequested;
    }

    // ── MediaElement seek ────────────────────────────────────────────────────

    private void OnSeekRequested(TimeSpan position)
    {
        // Always pause and show the frame at the selected start time
        if (_isPlaying)
        {
            VideoPreview.Pause();
            _isPlaying = false;
            PlayIcon.Text = "▶";
        }
        _ = VideoPreview.SeekTo(position, CancellationToken.None);
    }

    // ── Slider drag completed — seek without hammering on every value change ─

    private void OnSliderDragCompleted(object sender, EventArgs e)
    {
        var startPos = TimeSpan.FromSeconds(StartSlider.Value);
        if (_isPlaying)
        {
            VideoPreview.Pause();
            _isPlaying = false;
            PlayIcon.Text = "▶";
        }
        _ = VideoPreview.SeekTo(startPos, CancellationToken.None);
    }

    // ── Play / Pause toggle ──────────────────────────────────────────────────

    private void OnPlayToggleTapped(object sender, TappedEventArgs e)
    {
        if (_isPlaying)
        {
            VideoPreview.Pause();
            _isPlaying = false;
            PlayIcon.Text = "▶";
        }
        else
        {
            // Seek to start of selected window before playing
            _ = VideoPreview.SeekTo(TimeSpan.FromSeconds(_vm.StartSeconds), CancellationToken.None);
            VideoPreview.Play();
            _isPlaying = true;
            PlayIcon.Text = "⏸";
        }
    }
}
