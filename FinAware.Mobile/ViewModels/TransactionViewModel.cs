using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FinAware.Mobile.Models;
using FinAware.Mobile.Services;
using System.Collections.ObjectModel;

namespace FinAware.Mobile.ViewModels;

public partial class TransactionViewModel : BaseViewModel
{
    private readonly ApiService _apiService;

    [ObservableProperty] string amount = "";
    [ObservableProperty] string description = "";
    [ObservableProperty] string selectedType = "Expense";
    [ObservableProperty] DateTime selectedDate = DateTime.Now;
    [ObservableProperty] CategoryModel? selectedCategory;
    [ObservableProperty] string errorMessage = "";
    [ObservableProperty] bool hasError = false;
    [ObservableProperty] bool isRefreshing = false;

    public ObservableCollection<TransactionModel> Transactions { get; } = new();
    public ObservableCollection<CategoryModel> Categories { get; } = new();
    public List<string> Types { get; } = new() { "Expense", "Income" };

    public TransactionViewModel(ApiService apiService)
    {
        _apiService = apiService;
        Title = "İşlemler";
    }

    [RelayCommand]
    async Task LoadAsync()
    {
        if (IsBusy) return;
        IsBusy = true;

        try
        {
            var txResult = await _apiService.GetTransactionsAsync();
            if (txResult.IsSuccess && txResult.Data != null)
            {
                Transactions.Clear();
                foreach (var t in txResult.Data)
                    Transactions.Add(t);
            }

            var catResult = await _apiService.GetCategoriesAsync();
            if (catResult.IsSuccess && catResult.Data != null)
            {
                Categories.Clear();
                foreach (var c in catResult.Data)
                    Categories.Add(c);
            }
        }
        finally
        {
            IsBusy = false;
            IsRefreshing = false;
        }
    }

    [RelayCommand]
    async Task AddTransactionAsync()
    {
        if (!decimal.TryParse(Amount.Replace(",", "."), out decimal parsedAmount) || parsedAmount <= 0)
        {
            ErrorMessage = "Geçerli bir tutar girin.";
            HasError = true;
            return;
        }

        if (SelectedCategory == null)
        {
            ErrorMessage = "Kategori seçin.";
            HasError = true;
            return;
        }

        IsBusy = true;
        HasError = false;

        try
        {
            var req = new CreateTransactionRequest
            {
                Amount = parsedAmount,
                Type = SelectedType,
                Description = Description,
                Date = SelectedDate,
                CategoryId = SelectedCategory.CategoryId,
                Currency = "TRY"
            };

            var result = await _apiService.CreateTransactionAsync(req);
            if (result.IsSuccess)
            {
                Amount = "";
                Description = "";
                SelectedDate = DateTime.Now;
                await LoadAsync();
                await Shell.Current.DisplayAlert("Başarılı", "İşlem eklendi!", "Tamam");
            }
            else
            {
                ErrorMessage = result.ErrorMessage;
                HasError = true;
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    async Task DeleteTransactionAsync(TransactionModel transaction)
    {
        bool confirm = await Shell.Current.DisplayAlert(
            "Sil", $"Bu işlemi silmek istiyor musunuz?", "Evet", "Hayır");

        if (!confirm) return;

        var result = await _apiService.DeleteTransactionAsync(transaction.TransactionId);
        if (result.IsSuccess)
            await LoadAsync();
        else
            await Shell.Current.DisplayAlert("Hata", result.ErrorMessage, "Tamam");
    }

    [RelayCommand]
    async Task RefreshAsync()
    {
        IsRefreshing = true;
        await LoadAsync();
    }
}