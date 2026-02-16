using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Library.Models;
using Library.Services;
using System.Collections.ObjectModel;

namespace Library.ViewModels
{
    public partial class MemberLoansViewModel : ObservableObject
    {
        private readonly IDatabaseService _databaseService;
        private readonly IAuthService _authService;

        [ObservableProperty]
        private string memberName;

        [ObservableProperty]
        private string memberEmail;

        [ObservableProperty]
        private string memberInitials;

        [ObservableProperty]
        private string membershipStatus = "Active";

        [ObservableProperty]
        private Color statusColor = Colors.Green;

        [ObservableProperty]
        private ObservableCollection<MemberLoan> loans = new();

        [ObservableProperty]
        private bool isLoading = false;

        public IAsyncRelayCommand LoadLoansCommand { get; }

        public MemberLoansViewModel(IDatabaseService databaseService, IAuthService authService)
        {
            _databaseService = databaseService;
            _authService = authService;

            // ✅ FIXED HERE
            LoadLoansCommand = new AsyncRelayCommand(LoadLoansAsyncCommand);
        }

        // ✅ Wrapper method for command (required)
        private async Task LoadLoansAsyncCommand()
        {
            await LoadLoansAsync(null);
        }

        // ✔ Original method remains unchanged
        public async Task LoadLoansAsync(int? specificMemberId = null)
        {
            try
            {
                IsLoading = true;

                int memberId;

                if (specificMemberId.HasValue)
                {
                    memberId = specificMemberId.Value;
                    await LoadMemberDetails(memberId);
                }
                else
                {
                    var currentUser = await _authService.GetCurrentUserAsync();
                    if (currentUser == null) return;

                    memberId = currentUser.MemberId;
                    MemberName = $"{currentUser.FirstName} {currentUser.LastName}";
                    MemberEmail = currentUser.Email;
                    MemberInitials = GetInitials(currentUser.FirstName, currentUser.LastName);
                }

                var query = @"
                    SELECT l.loan_id AS LoanId,
                           l.copy_id AS CopyId,
                           l.checkout_datetime AS CheckoutDatetime,
                           l.due_datetime AS DueDatetime,
                           l.status AS Status,
                           t.title_name AS BookTitle,
                           bc.barcode AS Barcode,
                           DATEDIFF(l.due_datetime, NOW()) AS DaysRemaining
                    FROM loans l
                    JOIN book_copies bc ON l.copy_id = bc.copy_id
                    JOIN titles t ON bc.title_id = t.title_id
                    WHERE l.member_id = @memberId
                    ORDER BY l.due_datetime";

                var parameters = new Dictionary<string, object>
                {
                    { "@memberId", memberId }
                };

                var loansList = await _databaseService.QueryAsync<MemberLoan>(query, parameters);

                Loans.Clear();

                if (loansList != null)
                {
                    foreach (var loan in loansList)
                    {
                        loan.StatusColor = loan.Status switch
                        {
                            "Returned" => Colors.Gray,
                            "Overdue" => Colors.Red,
                            _ => loan.DaysRemaining < 0 ? Colors.Red :
                                 loan.DaysRemaining < 3 ? Colors.Orange :
                                 Colors.Green
                        };

                        Loans.Add(loan);
                    }
                }
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert(
                    "Error",
                    $"Failed to load loans: {ex.Message}",
                    "OK");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task LoadMemberDetails(int memberId)
        {
            try
            {
                var query = @"
                    SELECT first_name AS FirstName,
                           last_name AS LastName,
                           email AS Email
                    FROM members
                    WHERE member_id = @memberId";

                var parameters = new Dictionary<string, object>
                {
                    { "@memberId", memberId }
                };

                var member = await _databaseService.QuerySingleAsync<Member>(query, parameters);

                if (member != null)
                {
                    MemberName = $"{member.FirstName} {member.LastName}";
                    MemberEmail = member.Email;
                    MemberInitials = GetInitials(member.FirstName, member.LastName);
                }
            }
            catch
            {
            }
        }

        private string GetInitials(string firstName, string lastName)
        {
            if (string.IsNullOrWhiteSpace(firstName) &&
                string.IsNullOrWhiteSpace(lastName))
                return "?";

            string initials = "";

            if (!string.IsNullOrWhiteSpace(firstName))
                initials += firstName[0];

            if (!string.IsNullOrWhiteSpace(lastName))
                initials += lastName[0];

            return initials.ToUpper();
        }
    }

    public partial class MemberLoan : ObservableObject
    {
        public int LoanId { get; set; }
        public int CopyId { get; set; }
        public DateTime CheckoutDatetime { get; set; }
        public DateTime DueDatetime { get; set; }
        public string Status { get; set; }
        public string BookTitle { get; set; }
        public string Barcode { get; set; }
        public int DaysRemaining { get; set; }
        public Color StatusColor { get; set; }
    }
}
