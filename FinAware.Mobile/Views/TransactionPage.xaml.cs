using FinAware.Mobile.ViewModels;

namespace FinAware.Mobile.Views;

public partial class TransactionPage : ContentPage
{
    private readonly TransactionViewModel _vm;

    public TransactionPage(TransactionViewModel vm)
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