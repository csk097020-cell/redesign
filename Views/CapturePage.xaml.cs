using MomentaryMomentos.Models;
using MomentaryMomentos.ViewModels;

namespace MomentaryMomentos.Views;

public partial class CapturePage : ContentPage
{
    private readonly CaptureViewModel _vm;

    public CapturePage(CaptureViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _vm.IsPageVisible = true;
        _vm.LoadTagsCommand.Execute(null);

        if (_vm.TryConsumeAutoLaunch(out var intent))
        {
            if (intent == "record") _vm.CaptureVideoCommand.Execute(null);
            else if (intent == "pick") _vm.PickVideoCommand.Execute(null);
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _vm.IsPageVisible = false;
    }

    private void OnEmotionTagClicked(object sender, EventArgs e)
    {
        if (sender is Button { CommandParameter: Tag tag })
            _vm.SelectEmotionCommand.Execute(tag);
    }
}
