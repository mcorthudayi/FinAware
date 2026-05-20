using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FinAware.Mobile.Services;
using FinAware.Mobile.Views;

namespace FinAware.Mobile.ViewModels;

public partial class ProfileViewModel : BaseViewModel
{
    private readonly ApiService _apiService;
    private readonly AuthService _authService;

    [ObservableProperty] string username = "";
    [ObservableProperty] string email = "";
    [ObservableProperty] bool emailNotificationsEnabled;
    [ObservableProperty] bool telegramLinked;
    [ObservableProperty] string currentPassword = "";
    [ObservableProperty] string newPassword = "";
    [ObservableProperty] string confirmNewPassword = "";

    public ProfileViewModel(ApiService apiService, AuthService authService)
    {
        _apiService = apiService;
        _authService = authService;
        Title = "Profil";
    }

    [RelayCommand]
    async Task LoadAsync()
    {
        if (IsBusy) return;
        IsBusy = true;

        try
        {
            var result = await _apiService.GetProfileAsync();
            if (result.IsSuccess && result.Data != null)
            {
                Username = result.Data.Username;
                Email = result.Data.Email;
                EmailNotificationsEnabled = result.Data.EmailNotificationsEnabled;
                TelegramLinked = result.Data.TelegramLinked;
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    async Task ChangePasswordAsync()
    {
        if (string.IsNullOrWhiteSpace(CurrentPassword) ||
            string.IsNullOrWhiteSpace(NewPassword))
        {
            await Shell.Current.DisplayAlert("Hata", "Tüm alanları doldurun.", "Tamam");
            return;
        }

        if (NewPassword != ConfirmNewPassword)
        {
            await Shell.Current.DisplayAlert("Hata", "Yeni şifreler eşleşmiyor.", "Tamam");
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _apiService.ChangePasswordAsync(CurrentPassword, NewPassword);
            if (result.IsSuccess)
            {
                CurrentPassword = "";
                NewPassword = "";
                ConfirmNewPassword = "";
                await Shell.Current.DisplayAlert("Başarılı", "Şifre değiştirildi!", "Tamam");
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
    async Task LogoutAsync()
    {
        bool confirm = await Shell.Current.DisplayAlert(
            "Çıkış", "Çıkış yapmak istiyor musunuz?", "Evet", "Hayır");

        if (!confirm) return;

        _authService.Logout();
        Application.Current!.MainPage = new NavigationPage(
            App.Current!.Handler!.MauiContext!.Services.GetRequiredService<LoginPage>());
    }
}