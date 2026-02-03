using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Library.Services;
using Library.Views;
using System.Windows.Input;

namespace Library.ViewModels
{
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
                    // Navigate to dashboard
                    await Shell.Current.GoToAsync("//Dashboard");
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

        
        




     

    }
}

