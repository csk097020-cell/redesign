using CommunityToolkit.Mvvm.Input;
using MomentaryMomentos.Models;
using MomentaryMomentos.ViewModels;

namespace MomentaryMomentos.Views;

public partial class RelivePage : ContentPage
{
    private readonly ReliveViewModel _vm;

    public RelivePage(ReliveViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        try
        {
            // Relive now waits for the person to roll the dice. The old implementation
            // immediately chose a memory on tab entry, so the dice was never actually part
            // of the experience.
            _vm.IsChoosingMemory = false;
            if (_vm.RefreshCommand is IAsyncRelayCommand asyncRefresh)
                await asyncRefresh.ExecuteAsync(null);
            else
                _vm.RefreshCommand.Execute(null);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"RelivePage.OnAppearing refresh failed: {ex}");
        }
    }

    private async void OnDiceClicked(object sender, EventArgs e)
    {
        try
        {
            _vm.IsChoosingMemory = true;
            if (_vm.SurpriseMeCommand is IAsyncRelayCommand asyncSurprise)
                await asyncSurprise.ExecuteAsync(null);
            else
                _vm.SurpriseMeCommand.Execute(null);
        }
        finally
        {
            _vm.IsChoosingMemory = false;
        }
    }

    protected override bool OnBackButtonPressed()
    {
        MainThread.BeginInvokeOnMainThread(async () => await Shell.Current.GoToAsync("//main/home"));
        return true;
    }

    private void OnCardTapped(object sender, TappedEventArgs e)
    {
        if (e.Parameter is Memory memory)
            _vm.PlayCommand.Execute(memory);
    }

    private void OnToggleFavoriteTapped(object sender, TappedEventArgs e)
    {
        if (e.Parameter is Memory memory)
            _vm.ToggleFavoriteCommand.Execute(memory);
    }
}
