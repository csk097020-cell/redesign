// APPLICATION-SPECIFIC CODE - Property of Client (Corinne Kelley)
using MomentaryMomentos.ViewModels;

namespace MomentaryMomentos.Views;

public partial class MemoryDetailPage : ContentPage
{
    private readonly MemoryDetailViewModel _vm;

    public MemoryDetailPage(MemoryDetailViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    // ── Lifecycle ────────────────────────────────────────────────────────────

    protected override void OnAppearing()
    {
        base.OnAppearing();

        // Returning from the video player: snap back to the exact memory that just played and
        // show its title card. No-op on first appearance (FIX 1).
        _vm.RestorePlayedMemory();
    }

    // ── Navigation ───────────────────────────────────────────────────────────

    private void OnBackClicked(object sender, EventArgs e)
    {
        if (_vm.IsSingleMemoryMode)
        {
            Shell.Current.GoToAsync("//main/home");
            return;
        }

        Shell.Current.GoToAsync("..");
    }

    protected override bool OnBackButtonPressed()
    {
        if (!_vm.IsSingleMemoryMode)
            return base.OnBackButtonPressed();

        MainThread.BeginInvokeOnMainThread(async () => await Shell.Current.GoToAsync("//main/home"));
        return true;
    }

    // ── Swipe gesture (left = next, right = previous) ─────────────────────

    private void OnRetagTagClicked(object sender, EventArgs e)
    {
        if (sender is Button { CommandParameter: TagSelection ts })
            _vm.ToggleRetagSelectionCommand.Execute(ts);
    }

    private void OnSwipedLeft(object sender, SwipedEventArgs e)
    {
        if (_vm.HasNext)
            _vm.NextCommand.Execute(null);
    }

    private void OnSwipedRight(object sender, SwipedEventArgs e)
    {
        if (_vm.HasPrevious)
            _vm.PreviousCommand.Execute(null);
    }
}
