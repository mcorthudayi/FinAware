using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FinAware.Mobile.Models;
using FinAware.Mobile.Services;
using System.Collections.ObjectModel;

namespace FinAware.Mobile.ViewModels;

public partial class CategoryViewModel : BaseViewModel
{
    private readonly ApiService _apiService;

    [ObservableProperty] string name = "";
    [ObservableProperty] string selectedType = "Expense";
    [ObservableProperty] string selectedIcon = "📁";
    [ObservableProperty] string selectedColor = "#4DB6AC";

    public ObservableCollection<CategoryModel> Categories { get; } = new();
    public List<string> Types { get; } = new() { "Expense", "Income" };
    public List<string> Icons { get; } = new()
    {
        "🛒", "🍔", "🚗", "🧾", "💊", "🎬", "👔", "💻",
        "📚", "⚽", "💰", "💵", "📦", "🏠", "✈️", "🎓"
    };

    public CategoryViewModel(ApiService apiService)
    {
        _apiService = apiService;
        Title = "Kategoriler";
    }

    [RelayCommand]
    async Task LoadAsync()
    {
        if (IsBusy) return;
        IsBusy = true;

        try
        {
            var result = await _apiService.GetCategoriesAsync();
            if (result.IsSuccess && result.Data != null)
            {
                Categories.Clear();
                foreach (var c in result.Data)
                    Categories.Add(c);
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    async Task AddCategoryAsync()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            await Shell.Current.DisplayAlert("Hata", "Kategori adı zorunludur.", "Tamam");
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _apiService.CreateCategoryAsync(
                Name, SelectedType, SelectedIcon, SelectedColor);

            if (result.IsSuccess)
            {
                Name = "";
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
    async Task DeleteCategoryAsync(CategoryModel category)
    {
        bool confirm = await Shell.Current.DisplayAlert(
            "Sil", $"'{category.Name}' kategorisini silmek istiyor musunuz?", "Evet", "Hayır");

        if (!confirm) return;

        var result = await _apiService.DeleteCategoryAsync(category.CategoryId);
        if (result.IsSuccess)
            await LoadAsync();
        else
            await Shell.Current.DisplayAlert("Hata", result.ErrorMessage, "Tamam");
    }
}