using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FinAware.Mobile.Models;
using FinAware.Mobile.Services;
using System.Collections.ObjectModel;

namespace FinAware.Mobile.ViewModels;

public partial class BudgetViewModel : BaseViewModel
{
    private readonly ApiService _apiService;

    [ObservableProperty] int selectedMonth = DateTime.Now.Month;
    [ObservableProperty] int selectedYear = DateTime.Now.Year;
    [ObservableProperty] decimal totalExpense;
    [ObservableProperty] decimal limitAmount;
    [ObservableProperty] CategoryModel? selectedCategory;

    public ObservableCollection<BudgetModel> Budgets { get; } = new();
    public ObservableCollection<CategoryModel> Categories { get; } = new();

    public BudgetViewModel(ApiService apiService)
    {
        _apiService = apiService;
        Title = "Bütçe";
    }

    [RelayCommand]
    async Task LoadAsync()
    {
        if (IsBusy) return;
        IsBusy = true;

        try
        {
            var budgetResult = await _apiService.GetBudgetsAsync(SelectedMonth, SelectedYear);
            if (budgetResult.IsSuccess && budgetResult.Data != null)
            {
                Budgets.Clear();
                foreach (var b in budgetResult.Data.Budgets)
                    Budgets.Add(b);
                TotalExpense = budgetResult.Data.TotalExpense;
            }

            var catResult = await _apiService.GetCategoriesAsync();
            if (catResult.IsSuccess && catResult.Data != null)
            {
                Categories.Clear();
                foreach (var c in catResult.Data.Where(c => c.Type == "Expense"))
                    Categories.Add(c);
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    async Task AddBudgetAsync()
    {
        if (LimitAmount <= 0)
        {
            await Shell.Current.DisplayAlert("Hata", "Geçerli bir limit girin.", "Tamam");
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _apiService.CreateBudgetAsync(
                SelectedCategory?.CategoryId,
                LimitAmount,
                SelectedMonth,
                SelectedYear);

            if (result.IsSuccess)
            {
                LimitAmount = 0;
                SelectedCategory = null;
                await LoadAsync();
            }
            else
                await Shell.Current.DisplayAlert("Hata", result.ErrorMessage, "Tamam");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    async Task DeleteBudgetAsync(BudgetModel budget)
    {
        bool confirm = await Shell.Current.DisplayAlert("Sil", "Bu bütçeyi silmek istiyor musunuz?", "Evet", "Hayır");
        if (!confirm) return;

        var result = await _apiService.DeleteBudgetAsync(budget.BudgetId);
        if (result.IsSuccess)
            await LoadAsync();
    }
}