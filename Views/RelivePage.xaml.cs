using CommunityToolkit.Mvvm.Input;
using MomentaryMomentos.Models;
using MomentaryMomentos.ViewModels;

namespace MomentaryMomentos.Views;

public partial class RelivePage : ContentPage
{
    private readonly ReliveViewModel _vm;
    private bool _suppressNextAutoPlay;

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
            _vm.IsChoosingMemory = true;

            if (_vm.RefreshCommand is IAsyncRelayCommand asyncRefresh)
                await asyncRefresh.ExecuteAsync(null);
            else
                _vm.RefreshCommand.Execute(null);

            if (_suppressNextAutoPlay)
            {
                _suppressNextAutoPlay = false;
                await Shell.Current.GoToAsync("//main/home");
                return;
            }

            _suppressNextAutoPlay = true;
            if (_vm.SurpriseMeCommand is IAsyncRelayCommand asyncSurprise)
                await asyncSurprise.ExecuteAsync(null);
            else
                _vm.SurpriseMeCommand.Execute(null);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"RelivePage.OnAppearing refresh failed: {ex}");
            await Shell.Current.GoToAsync("//main/home");
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
        {
            _suppressNextAutoPlay = true;
            _vm.PlayCommand.Execute(memory);
        }
    }

    private void OnToggleFavoriteTapped(object sender, TappedEventArgs e)
    {
        if (e.Parameter is Memory memory)
            _vm.ToggleFavoriteCommand.Execute(memory);
    }

    private void OnDeleteClicked(object sender, EventArgs e)
    {
        if (sender is Button { CommandParameter: Memory memory })
            _vm.DeleteCommand.Execute(memory);
    }
}
