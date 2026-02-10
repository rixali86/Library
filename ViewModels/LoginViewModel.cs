using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Library.Services;
using System.Threading.Tasks;

namespace Library.ViewModels;

public partial class LoginViewModel : ObservableObject
{
    private readonly IAuthService _authService;

    [ObservableProperty]
    private string email = string.Empty;

    [ObservableProperty]
    private string password = string.Empty;

    [ObservableProperty]
    private bool isLoading = false;

    [ObservableProperty]
    private string errorMessage = string.Empty;

    [ObservableProperty]
    private bool isPasswordHidden = true;

    public string EyeIcon => IsPasswordHidden ? "eye_closed.png" : "eye_open.png";

    [RelayCommand]
    private void TogglePassword()
    {
        IsPasswordHidden = !IsPasswordHidden;
        OnPropertyChanged(nameof(EyeIcon));
    }

    public LoginViewModel(IAuthService authService)
    {
        _authService = authService;
    }

    [RelayCommand]
    private async Task LoginAsync()
    {
        if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "Please enter both email and password";
            return;
        }

        IsLoading = true;
        ErrorMessage = string.Empty;

        try
        {
            var success = await _authService.LoginAsync(Email, Password);

            if (success)
            {
                // Start user session
                await UserSession.Instance.StartSessionAsync(Email);

                // Switch to MainShell
                var mainShell = Application.Current.Handler.MauiContext.Services.GetService<AppShell>();
                Application.Current.MainPage = mainShell;
            }
            else
            {
                ErrorMessage = "Invalid email or password";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Login failed: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task GoToRegisterAsync()
    {
        await Shell.Current.GoToAsync("//Register");
    }

    // Added: clear credentials and UI state (called when login page appears or on logout)
    public void ClearCredentials()
    {
        Email = string.Empty;
        Password = string.Empty;
        ErrorMessage = string.Empty;
        IsPasswordHidden = true;
        OnPropertyChanged(nameof(EyeIcon));
    }
}