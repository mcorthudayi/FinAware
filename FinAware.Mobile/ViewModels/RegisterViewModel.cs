using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FinAware.Mobile.Services;

namespace FinAware.Mobile.ViewModels;

public partial class RegisterViewModel : BaseViewModel
{
    private readonly ApiService _apiService;

    [ObservableProperty] string username = "";
    [ObservableProperty] string email = "";
    [ObservableProperty] string password = "";
    [ObservableProperty] string confirmPassword = "";
    [ObservableProperty] string errorMessage = "";
    [ObservableProperty] bool hasError = false;
    [ObservableProperty] string successMessage = "";
    [ObservableProperty] bool hasSuccess = false;

    public RegisterViewModel(ApiService apiService)
    {
        _apiService = apiService;
        Title = "Kayıt Ol";
    }

    [RelayCommand]
    async Task RegisterAsync()
    {
        if (string.IsNullOrWhiteSpace(Username) ||
            string.IsNullOrWhiteSpace(Email) ||
            string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "Tüm alanlar zorunludur.";
            HasError = true;
            return;
        }

        if (Password != ConfirmPassword)
        {
            ErrorMessage = "Şifreler eşleşmiyor.";
            HasError = true;
            return;
        }

        IsBusy = true;
        HasError = false;
        HasSuccess = false;

        try
        {
            var result = await _apiService.RegisterAsync(Username, Email, Password);

            if (result.IsSuccess)
            {
                SuccessMessage = "Kayıt başarılı! Giriş yapabilirsiniz.";
                HasSuccess = true;
                await Task.Delay(1500);
                await Shell.Current.GoToAsync("..");
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
    async Task GoBackAsync()
        => await Shell.Current.GoToAsync("..");
}