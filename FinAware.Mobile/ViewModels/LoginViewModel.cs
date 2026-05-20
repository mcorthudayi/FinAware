using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FinAware.Mobile.Services;
using FinAware.Mobile.Views;

namespace FinAware.Mobile.ViewModels;

public partial class LoginViewModel : BaseViewModel
{
    private readonly ApiService _apiService;
    private readonly AuthService _authService;

    [ObservableProperty] string email = "";
    [ObservableProperty] string password = "";
    [ObservableProperty] bool rememberMe = false;
    [ObservableProperty] string errorMessage = "";
    [ObservableProperty] bool hasError = false;

    public LoginViewModel(ApiService apiService, AuthService authService)
    {
        _apiService = apiService;
        _authService = authService;
        Title = "Giriş Yap";
    }

    [RelayCommand]
    async Task LoginAsync()
    {
        if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "E-posta ve şifre gereklidir.";
            HasError = true;
            return;
        }

        IsBusy = true;
        HasError = false;

        try
        {
            var result = await _apiService.LoginAsync(Email, Password);

            if (result.IsSuccess && result.Data != null)
            {
                await _authService.SaveTokenAsync(
                    result.Data.Token,
                    result.Data.Username,
                    result.Data.Email);

                Application.Current!.MainPage = new AppShell();
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
    async Task GoToRegisterAsync()
    {
        await Shell.Current.GoToAsync(nameof(RegisterPage));
    }
}