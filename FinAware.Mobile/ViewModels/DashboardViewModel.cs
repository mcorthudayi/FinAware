using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FinAware.Mobile.Models;
using FinAware.Mobile.Services;
using System.Collections.ObjectModel;

namespace FinAware.Mobile.ViewModels;

public partial class DashboardViewModel : BaseViewModel
{
    private readonly ApiService _apiService;
    private readonly AuthService _authService;

    [ObservableProperty] decimal totalIncome;
    [ObservableProperty] decimal totalExpense;
    [ObservableProperty] decimal totalBalance;
    [ObservableProperty] string username = "";
    [ObservableProperty] int unreadCount;
    [ObservableProperty] string balanceColor = "#1AAFA3";

    public ObservableCollection<TransactionModel> RecentTransactions { get; } = new();

    public DashboardViewModel(ApiService apiService, AuthService authService)
    {
        _apiService = apiService;
        _authService = authService;
        Title = "Ana Sayfa";
    }

    [RelayCommand]
    async Task LoadAsync()
    {
        if (IsBusy) return;
        IsBusy = true;

        try
        {
            Username = await _authService.GetUsernameAsync() ?? "";

            var txResult = await _apiService.GetTransactionsAsync();
            if (txResult.IsSuccess && txResult.Data != null)
            {
                var transactions = txResult.Data;
                TotalIncome = transactions.Where(t => t.Type == "Income").Sum(t => t.Amount);
                TotalExpense = transactions.Where(t => t.Type == "Expense").Sum(t => t.Amount);
                TotalBalance = TotalIncome - TotalExpense;
                BalanceColor = TotalBalance >= 0 ? "#4CAF50" : "#F44336";

                RecentTransactions.Clear();
                foreach (var t in transactions.Take(10))
                    RecentTransactions.Add(t);
            }

            var notifResult = await _apiService.GetUnreadCountAsync();
            if (notifResult.IsSuccess)
                UnreadCount = notifResult.Data;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    async Task RefreshAsync() => await LoadAsync();
}