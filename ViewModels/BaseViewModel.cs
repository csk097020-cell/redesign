// GENERAL-PURPOSE COMPONENT - Licensed to Client per contract Section 6.2
using CommunityToolkit.Mvvm.ComponentModel;

namespace MomentaryMomentos.ViewModels;

/// <summary>
/// Base ViewModel with busy-state and error handling.
/// Pattern: MVVM with CommunityToolkit source generators.
/// </summary>
public abstract partial class BaseViewModel : ObservableObject
{
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string? errorMessage;
    [ObservableProperty] private string? successMessage;

    /// <summary>
    /// Drives RefreshView.IsRefreshing only. Bind pull-to-refresh to this, never to
    /// <see cref="IsBusy"/>: Android's SwipeRefreshLayout can be left spinning forever when
    /// IsRefreshing is raised programmatically rather than by a user pull, and only an app
    /// restart clears it. Nothing here ever sets it true — the RefreshView does that on a real
    /// pull, and RunBusyAsync always puts it back down.
    /// </summary>
    [ObservableProperty] private bool isRefreshing;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);
    public bool HasSuccess => !string.IsNullOrEmpty(SuccessMessage);

    protected async Task RunBusyAsync(Func<Task> action)
    {
        // A pull that lands mid-operation still has to lower its own spinner.
        if (IsBusy) { IsRefreshing = false; return; }
        try
        {
            IsBusy = true;
            ErrorMessage = null;
            SuccessMessage = null;
            await action();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            OnPropertyChanged(nameof(HasError));
        }
        finally
        {
            IsBusy = false;
            IsRefreshing = false;
        }
    }

    partial void OnErrorMessageChanged(string? value) => OnPropertyChanged(nameof(HasError));
    partial void OnSuccessMessageChanged(string? value) => OnPropertyChanged(nameof(HasSuccess));
}
