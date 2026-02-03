using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Library.Models;
using Library.Services;



namespace Library.ViewModels
{
    public partial class RegisterViewModel : ObservableObject
    {
        private readonly IAuthService _authService;
        private readonly IDatabaseService _databaseService;

        [ObservableProperty]
        private string firstName = string.Empty;

        [ObservableProperty]
        private string lastName = string.Empty;

        [ObservableProperty]
        private string email = string.Empty;

        [ObservableProperty]
        private string phone = string.Empty;

        [ObservableProperty]
        private DateTime dateOfBirth = DateTime.Now.AddYears(-18);

        [ObservableProperty]
        private string password = string.Empty;

        [ObservableProperty]
        private string confirmPassword = string.Empty;

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



        public RegisterViewModel(IAuthService authService, IDatabaseService databaseService)
        {
            _authService = authService;
            _databaseService = databaseService;
        }

        [RelayCommand]

        private async Task Register()
        {
            // Validate inputs
            if (!await ValidateInputs())
            {
                await Application.Current.MainPage.DisplayAlert("Error", ErrorMessage, "OK");
                return;
            }

            IsLoading = true;
            ErrorMessage = string.Empty;

            try
            {
                var memberInfo = new Member
                {
                    FirstName = FirstName,
                    LastName = LastName,
                    Email = Email,
                    Phone = Phone,
                    DateOfBirth = DateOfBirth
                };

                var success = await _authService.RegisterAsync(Email, Password, memberInfo);

                if (success)
                {
                    await Application.Current.MainPage.DisplayAlert(
                        "Success",
                        "Registration successful! You can now login.",
                        "OK");

                    await Shell.Current.GoToAsync("//Login");
                }
                else
                {
                    await Application.Current.MainPage.DisplayAlert(
                        "Error",
                        "Registration failed. Email Or Phone number may already be in use.",
                        "OK");
                }
            }
           
            finally
            {
                IsLoading = false;
            }
        }


        [RelayCommand]
        private async Task GoToLogin()
        {
            await Shell.Current.GoToAsync("//Login");
        }

        // Add these methods to check for duplicates in your database/storage

        private async Task<bool> IsEmailDuplicate(string email)
        {
            try
            {
                // Replace this with your actual database query
                // Example using Entity Framework:
                // return await _context.Users.AnyAsync(u => u.Email.ToLower() == email.ToLower());

                // Example using a list (for demonstration):
                // return existingUsers.Any(u => u.Email.Equals(email, StringComparison.OrdinalIgnoreCase));

                // Placeholder - implement based on your data access method
                return false;
            }
            catch (Exception ex)
            {
                // Log the exception
                Console.WriteLine($"Error checking email duplicate: {ex.Message}");
                return false;
            }
        }

        private async Task<bool> IsPhoneDuplicate(string phone)
        {
            try
            {
                // Remove formatting characters for comparison
                string cleanPhone = System.Text.RegularExpressions.Regex.Replace(phone, @"[^\d]", "");

                // Replace this with your actual database query
                // Example using Entity Framework:
                // return await _context.Users.AnyAsync(u => u.Phone.Replace("-", "").Replace(" ", "").Replace("(", "").Replace(")", "") == cleanPhone);

                // Placeholder - implement based on your data access method
                return false;
            }
            catch (Exception ex)
            {
                // Log the exception
                Console.WriteLine($"Error checking phone duplicate: {ex.Message}");
                return false;
            }
        }

        // Update your ValidateInputs method to be async and include duplicate checks
        private async Task<bool> ValidateInputs()
        {
            try
            {
                // Clear previous errors
                ErrorMessage = string.Empty;

                // Check and validate First Name (only letters allowed)
                if (string.IsNullOrWhiteSpace(FirstName))
                {
                    ErrorMessage = "First name is required";
                    return false;
                }
                if (FirstName.Length < 2 || FirstName.Length > 50)
                {
                    ErrorMessage = "First name must be between 2 and 50 characters";
                    return false;
                }
                if (!System.Text.RegularExpressions.Regex.IsMatch(FirstName, @"^[a-zA-Z\s]+$"))
                {
                    ErrorMessage = "First name can only contain letters";
                    return false;
                }

                // Check and validate Last Name (only letters allowed)
                if (string.IsNullOrWhiteSpace(LastName))
                {
                    ErrorMessage = "Last name is required";
                    return false;
                }
                if (LastName.Length < 2 || LastName.Length > 50)
                {
                    ErrorMessage = "Last name must be between 2 and 50 characters";
                    return false;
                }
                if (!System.Text.RegularExpressions.Regex.IsMatch(LastName, @"^[a-zA-Z\s]+$"))
                {
                    ErrorMessage = "Last name can only contain letters";
                    return false;
                }

                // Check and validate Email
                if (string.IsNullOrWhiteSpace(Email))
                {
                    ErrorMessage = "Email is required";
                    return false;
                }
                if (Email.Length > 100)
                {
                    ErrorMessage = "Email must not exceed 100 characters";
                    return false;
                }

                // Improved email validation using regex
                string emailPattern = @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$";
                if (!System.Text.RegularExpressions.Regex.IsMatch(Email, emailPattern))
                {
                    ErrorMessage = "Please enter a valid email address";
                    return false;
                }

                // Check for duplicate email
                if (await IsEmailDuplicate(Email))
                {
                    ErrorMessage = "This email address is already registered";
                    return false;
                }

                // Check and validate Phone Number (only digits allowed)
                if (!string.IsNullOrWhiteSpace(Phone))
                {
                    if (Phone.Length < 10 || Phone.Length > 15)
                    {
                        ErrorMessage = "Phone number must be between 10 and 15 characters";
                        return false;
                    }
                    if (!System.Text.RegularExpressions.Regex.IsMatch(Phone, @"^[0-9+\-\s()]+$"))
                    {
                        ErrorMessage = "Phone number can only contain numbers and basic formatting characters";
                        return false;
                    }

                    // Check for duplicate phone number
                    if (await IsPhoneDuplicate(Phone))
                    {
                        ErrorMessage = "This phone number is already registered";
                        return false;
                    }
                }

                // Check and validate Password
                if (string.IsNullOrWhiteSpace(Password))
                {
                    ErrorMessage = "Password is required";
                    return false;
                }
                if (Password.Length < 8 || Password.Length > 20)
                {
                    ErrorMessage = "Password must be between 8 and 20 characters";
                    return false;
                }

                // Password strength validation (must contain uppercase, lowercase, and special character)
                bool hasUpperCase = System.Text.RegularExpressions.Regex.IsMatch(Password, @"[A-Z]");
                bool hasLowerCase = System.Text.RegularExpressions.Regex.IsMatch(Password, @"[a-z]");
                bool hasSpecialChar = System.Text.RegularExpressions.Regex.IsMatch(Password, @"[!@#$%^&*()_+\-=\[\]{};':""\\|,.<>/?]");

                if (!hasUpperCase || !hasLowerCase || !hasSpecialChar)
                {
                    ErrorMessage = "Password must contain at least one uppercase letter, one lowercase letter, and one special character";
                    return false;
                }

                // Check password confirmation
                if (Password != ConfirmPassword)
                {
                    ErrorMessage = "Passwords do not match";
                    return false;
                }

                // Check age (must be at least 13 years old)
                var age = DateTime.Now.Year - DateOfBirth.Year;
                if (DateOfBirth > DateTime.Now.AddYears(-age)) age--;
                if (age < 13)
                {
                    ErrorMessage = "You must be at least 13 years old to register";
                    return false;
                }

                return true;
            }
            catch (FormatException)
            {
                ErrorMessage = "Please enter a valid email address";
                return false;
            }
            catch (Exception ex)
            {
                // Log the exception for debugging
                // Console.WriteLine($"Validation error: {ex.Message}");
                ErrorMessage = "An unexpected error occurred during validation";
                return false;
            }
            
        }

    }
}