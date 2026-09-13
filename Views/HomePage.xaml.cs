using MomentaryMomentos.ViewModels;

namespace MomentaryMomentos.Views;

public partial class HomePage : ContentPage
{
    private readonly HomeViewModel _vm;

    public HomePage(HomeViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _vm.RefreshCommand.Execute(null);
        // Update the offline indicator reactively while the page is open.
        Connectivity.Current.ConnectivityChanged += OnConnectivityChanged;
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        Connectivity.Current.ConnectivityChanged -= OnConnectivityChanged;
    }

    private void OnConnectivityChanged(object? sender, ConnectivityChangedEventArgs e)
        => MainThread.BeginInvokeOnMainThread(_vm.RefreshConnectionStatus);

    // ── Auto-fit app name ────────────────────────────────────────────────────
    // The header title wants to be 16pt, but on narrow screens that overflows and
    // truncates ("Momentary Moment…"). Whenever the label's allotted width changes
    // (first layout, rotation, split-screen), re-measure the text at full size and
    // shrink the font only as much as that width requires.

    private const double TitleBaseFontSize = 16;
    private double _lastTitleFitWidth = -1;

    private void OnTitleSizeChanged(object? sender, EventArgs e) => FitTitleToWidth();

    private void FitTitleToWidth()
    {
        double available = TitleLabel.Width;
        // The label fills its column, so its width doesn't depend on its font size —
        // guarding on it prevents re-entry loops from our own FontSize changes.
        if (available <= 0 || Math.Abs(available - _lastTitleFitWidth) < 0.5) return;
        _lastTitleFitWidth = available;

        TitleLabel.FontSize = TitleBaseFontSize;
        double needed = TitleLabel.Measure(double.PositiveInfinity, double.PositiveInfinity).Width;
        if (needed > available)
            TitleLabel.FontSize = Math.Max(11, TitleBaseFontSize * (available / needed) * 0.97);
    }

    private void OnCaptureClicked(object sender, EventArgs e)
        => Shell.Current.GoToAsync("//main/capture?intent=record");

    private void OnReliveClicked(object sender, EventArgs e)
        => Shell.Current.GoToAsync("//main/relive");

    private void OnUploadClicked(object sender, EventArgs e)
        => Shell.Current.GoToAsync("//main/capture?intent=pick");

    // The action rows are tappable across their full width, not just the icon badge.
    private void OnCaptureRowTapped(object sender, TappedEventArgs e)
        => Shell.Current.GoToAsync("//main/capture?intent=record");

    private void OnReliveRowTapped(object sender, TappedEventArgs e)
        => Shell.Current.GoToAsync("//main/relive");

    private void OnUploadRowTapped(object sender, TappedEventArgs e)
        => Shell.Current.GoToAsync("//main/capture?intent=pick");

    private void OnManageTagsClicked(object sender, EventArgs e)
        => Shell.Current.GoToAsync("managetags");

    private void OnHelpClicked(object sender, EventArgs e)
        => Shell.Current.GoToAsync("help");
}
