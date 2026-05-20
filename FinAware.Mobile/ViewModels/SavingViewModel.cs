using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FinAware.Mobile.Models;
using FinAware.Mobile.Services;
using System.Collections.ObjectModel;

namespace FinAware.Mobile.ViewModels;

public partial class SavingViewModel : BaseViewModel
{
    private readonly ApiService _apiService;

    [ObservableProperty] string goalName = "";
    [ObservableProperty] string targetAmount = "";
    [ObservableProperty] string addAmount = "";
    [ObservableProperty] DateTime? targetDate;
    [ObservableProperty] string selectedIcon = "💰";
    [ObservableProperty] string selectedColor = "#4DB6AC";

    public ObservableCollection<SavingModel> Savings { get; } = new();
    public List<string> Icons { get; } = new() { "💰", "🏠", "✈️", "🚗", "📱", "🎓", "💍", "🏖️" };

    public SavingViewModel(ApiService apiService)
    {
        _apiService = apiService;
        Title = "Birikimler";
    }

    [RelayCommand]
    async Task LoadAsync()
    {
        if (IsBusy) return;
        IsBusy = true;

        try
        {
            var result = await _apiService.GetSavingsAsync();
            if (result.IsSuccess && result.Data != null)
            {
                Savings.Clear();
                foreach (var s in result.Data)
                    Savings.Add(s);
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    async Task AddSavingAsync()
    {
        if (string.IsNullOrWhiteSpace(GoalName) ||
            !decimal.TryParse(TargetAmount.Replace(",", "."), out decimal amount) || amount <= 0)
        {
            await Shell.Current.DisplayAlert("Hata", "Hedef adı ve tutar zorunludur.", "Tamam");
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _apiService.CreateSavingAsync(
                GoalName, amount, TargetDate, SelectedIcon, SelectedColor);

            if (result.IsSuccess)
            {
                GoalName = "";
                TargetAmount = "";
                TargetDate = null;
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
    async Task AddAmountAsync(SavingModel saving)
    {
        var input = await Shell.Current.DisplayPromptAsync(
            "Tutar Ekle", $"'{saving.GoalName}' hedefine eklenecek tutar:", "Ekle", "İptal",
            placeholder: "0.00", keyboard: Keyboard.Numeric);

        if (string.IsNullOrEmpty(input)) return;

        if (!decimal.TryParse(input.Replace(",", "."), out decimal amount) || amount <= 0)
        {
            await Shell.Current.DisplayAlert("Hata", "Geçerli bir tutar girin.", "Tamam");
            return;
        }

        var result = await _apiService.AddSavingAmountAsync(saving.SavingId, amount);
        if (result.IsSuccess)
            await LoadAsync();
        else
            await Shell.Current.DisplayAlert("Hata", result.ErrorMessage, "Tamam");
    }

    [RelayCommand]
    async Task DeleteSavingAsync(SavingModel saving)
    {
        bool confirm = await Shell.Current.DisplayAlert(
            "Sil", $"'{saving.GoalName}' hedefini silmek istiyor musunuz?", "Evet", "Hayır");

        if (!confirm) return;

        var result = await _apiService.DeleteSavingAsync(saving.SavingId);
        if (result.IsSuccess)
            await LoadAsync();
    }
}