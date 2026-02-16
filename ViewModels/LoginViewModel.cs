using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Library.Services;
using System;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;

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

    public LoginViewModel(IAuthService authService)
    {
        _authService = authService;
    }

    // Show / Hide Password
    [RelayCommand]
    private void TogglePassword()
    {
        IsPasswordHidden = !IsPasswordHidden;
        OnPropertyChanged(nameof(EyeIcon));
    }

    
    // Login
    [RelayCommand]
    private async Task LoginAsync()
    {
        if (string.IsNullOrWhiteSpace(Email) ||
            string.IsNullOrWhiteSpace(Password))
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
                // Load user
                await _authService.GetCurrentUserAsync();

                // Start session
                await UserSession.Instance.StartSessionAsync(
                    Email,
                    _authService.CurrentUserRole,
                    _authService.CurrentUserId);

                // Switch to main shell (NO PARAMETER)
                if (Application.Current is App app)
                {
                    app.SwitchToMainShell();
                }
            }
            else
            {
                ErrorMessage = "Invalid email or password";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = "Login failed: " + ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    // Go to Register Page
    [RelayCommand]
    private async Task GoToRegisterAsync()
    {
        await Shell.Current.GoToAsync("//Register");
    }

    // Clear Fields
    public void ClearCredentials()
    {
        Email = string.Empty;
        Password = string.Empty;
        ErrorMessage = string.Empty;
        IsPasswordHidden = true;

        OnPropertyChanged(nameof(EyeIcon));
    }
}
