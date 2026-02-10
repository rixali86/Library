using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Library.Models;
using Library.Services;

namespace Library.ViewModels
{
    public partial class DashboardViewModel : ObservableObject
    {
        private readonly IAuthService _authService;
        private readonly ILibraryService _libraryService;
        private readonly IDatabaseService _databaseService;

        [ObservableProperty]
        private List<Loan> currentLoans = new();

        [ObservableProperty]
        private List<BranchEvent> upcomingEvents = new();

        [ObservableProperty]
        private bool isLoading = false;

        [ObservableProperty]
        private string userName = string.Empty;

        public DashboardViewModel(IAuthService authService, ILibraryService libraryService, IDatabaseService databaseService)
        {
            _authService = authService;
            _libraryService = libraryService;
            _databaseService = databaseService;
            LoadDataCommand.ExecuteAsync(null);
        }

        [RelayCommand]
        private async Task LoadDataAsync()
        {
            IsLoading = true;
            try
            {
                // Get member ID from email (in real app, you'd store member ID in auth)
                var email = _authService.CurrentUserEmail;
                var memberQuery = "SELECT member_id, first_name FROM members WHERE email = @email";
                var memberParams = new Dictionary<string, object> { { "@email", email } };
                var member = await _databaseService.QuerySingleAsync<Member>(memberQuery, memberParams);

                if (member != null)
                {
                    UserName = member.FirstName;

                    // Load loans
                    CurrentLoans = await _libraryService.GetMemberLoansAsync(member.MemberId);

                    // Load events
                    UpcomingEvents = await _libraryService.GetUpcomingEventsAsync();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Load data error: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task SearchBooksAsync()
        {
            await Shell.Current.GoToAsync("//Search");
        }

        [RelayCommand]
        private async Task ViewBranchesAsync()
        {
            await Shell.Current.GoToAsync("//Branches");
        }

        [RelayCommand]
        private async Task ViewEventsAsync()
        {
            await Shell.Current.GoToAsync("//Events");
        }
        [RelayCommand]
        private async Task ViewAppSettingAsync()
        {
            await Shell.Current.GoToAsync("//Setting");
        }

        [RelayCommand]
        private async Task ViewMemberLoansAsync()
        {
            await Shell.Current.GoToAsync("//Loan");
        }


        [RelayCommand]
        private async Task LogoutAsync()
        {
            await _authService.LogoutAsync();

            // Switch application to the authentication shell (AuthShell),
            // because the Login route is registered in AuthShell.
            // This avoids attempting to navigate to a route that AppShell doesn't know about.
            ((App)Application.Current).SwitchToAuthShell();
        }

        [RelayCommand]
        private async Task ReturnBookAsync(Loan loan)
        {
            if (loan == null) return;

            bool success = await _libraryService.ReturnBookAsync(loan.LoanId);
            if (success)
            {
                await LoadDataAsync();
                await Application.Current.MainPage.DisplayAlert("Success", "Book returned successfully", "OK");
            }
            else
            {
                await Application.Current.MainPage.DisplayAlert("Error", "Failed to return book", "OK");
            }
        }
    }
}