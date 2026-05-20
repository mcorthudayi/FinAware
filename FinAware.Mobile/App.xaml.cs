using FinAware.Mobile.Views;

namespace FinAware.Mobile;

public partial class App : Application
{
    public App(IServiceProvider serviceProvider)
    {
        InitializeComponent();

        var token = SecureStorage.GetAsync("auth_token").Result;
        if (!string.IsNullOrEmpty(token))
            MainPage = new AppShell();
        else
            MainPage = new NavigationPage(serviceProvider.GetRequiredService<LoginPage>());
    }
}