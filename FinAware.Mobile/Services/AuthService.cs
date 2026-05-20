namespace FinAware.Mobile.Services;

public class AuthService
{
    private const string TokenKey = "auth_token";
    private const string UsernameKey = "auth_username";
    private const string EmailKey = "auth_email";

    public async Task SaveTokenAsync(string token, string username, string email)
    {
        await SecureStorage.SetAsync(TokenKey, token);
        await SecureStorage.SetAsync(UsernameKey, username);
        await SecureStorage.SetAsync(EmailKey, email);
    }

    public async Task<string?> GetTokenAsync()
        => await SecureStorage.GetAsync(TokenKey);

    public async Task<string?> GetUsernameAsync()
        => await SecureStorage.GetAsync(UsernameKey);

    public async Task<string?> GetEmailAsync()
        => await SecureStorage.GetAsync(EmailKey);

    public async Task<bool> IsLoggedInAsync()
    {
        var token = await GetTokenAsync();
        return !string.IsNullOrEmpty(token);
    }

    public void Logout()
    {
        SecureStorage.Remove(TokenKey);
        SecureStorage.Remove(UsernameKey);
        SecureStorage.Remove(EmailKey);
    }
}