using MomentaryMomentos.ViewModels;

namespace MomentaryMomentos.Views;

public partial class TagManagementPage : ContentPage
{
    private readonly TagManagementViewModel _vm;

    public TagManagementPage(TagManagementViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _vm.LoadCommand.Execute(null);
    }

    private void OnBackClicked(object sender, EventArgs e)
        => Shell.Current.GoToAsync("//main/home");

    private void OnEditTagClicked(object sender, EventArgs e)
    {
        if (sender is Button { CommandParameter: TagUsageItem item })
            _vm.EditCommand.Execute(item);
    }

    private void OnToggleTagClicked(object sender, EventArgs e)
    {
        if (sender is Button { CommandParameter: TagUsageItem item })
            _vm.ToggleActiveCommand.Execute(item);
    }

    private void OnDeleteTagClicked(object sender, EventArgs e)
    {
        if (sender is Button { CommandParameter: TagUsageItem item })
            _vm.DeleteCommand.Execute(item);
    }

    protected override bool OnBackButtonPressed()
    {
        MainThread.BeginInvokeOnMainThread(async () =>
            await Shell.Current.GoToAsync("//main/home"));

        return true;
    }
}
