using MomentaryMomentos.ViewModels;

namespace MomentaryMomentos.Views;

public partial class AdminPage : ContentPage
{
    private readonly AdminViewModel _vm;

    public AdminPage(AdminViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _vm.LoadCommand.Execute(null);
    }
}
