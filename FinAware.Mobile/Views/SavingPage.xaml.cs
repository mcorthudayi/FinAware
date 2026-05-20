using FinAware.Mobile.ViewModels;

namespace FinAware.Mobile.Views;

public partial class SavingPage : ContentPage
{
    private readonly SavingViewModel _vm;

    public SavingPage(SavingViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.LoadCommand.ExecuteAsync(null);
    }
}