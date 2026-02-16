using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Library.Models;
using Library.Services;
using System.Collections.ObjectModel;

namespace Library.ViewModels
{
    public partial class ReturnBookViewModel : ObservableObject
    {
        private readonly IDatabaseService _databaseService;
        private readonly IAuthService _authService;

        [ObservableProperty]
        private string barcodeInput = string.Empty;

        [ObservableProperty]
        private ObservableCollection<Member> members = new();

        [ObservableProperty]
        private Member selectedMember;

        [ObservableProperty]
        private ObservableCollection<Loan> memberLoans = new();

        [ObservableProperty]
        private bool hasFines = false;

        [ObservableProperty]
        private decimal totalFine = 0;

        [ObservableProperty]
        private bool isProcessing = false;

        [ObservableProperty]
        private string memberSearchText = string.Empty;

        [ObservableProperty]
        private ObservableCollection<Member> filteredMembers = new();

        public IAsyncRelayCommand SearchByBarcodeCommand { get; }
        public IAsyncRelayCommand LoadMembersCommand { get; }
        public IAsyncRelayCommand<Member> LoadMemberLoansCommand { get; }
        public IAsyncRelayCommand<Loan> ReturnBookCommand { get; }
        public IAsyncRelayCommand PayFinesCommand { get; }
        public IAsyncRelayCommand ReturnWithoutPaymentCommand { get; }
        public IRelayCommand FilterMembersCommand { get; }

        public ReturnBookViewModel(IDatabaseService databaseService, IAuthService authService)
        {
            _databaseService = databaseService;
            _authService = authService;

            SearchByBarcodeCommand = new AsyncRelayCommand(SearchByBarcodeAsync);
            LoadMembersCommand = new AsyncRelayCommand(LoadMembersAsync);
            LoadMemberLoansCommand = new AsyncRelayCommand<Member>(LoadMemberLoansAsync);
            ReturnBookCommand = new AsyncRelayCommand<Loan>(ReturnBookAsync);
            PayFinesCommand = new AsyncRelayCommand(PayFinesAsync);
            ReturnWithoutPaymentCommand = new AsyncRelayCommand(ReturnWithoutPaymentAsync);
            FilterMembersCommand = new RelayCommand(FilterMembers);

            // Load members immediately when ViewModel is created
            MainThread.BeginInvokeOnMainThread(async () => await LoadMembersAsync());
        }

        partial void OnMemberSearchTextChanged(string value)
        {
            FilterMembers();
        }

        private void FilterMembers()
        {
            if (string.IsNullOrWhiteSpace(MemberSearchText))
            {
                FilteredMembers.Clear();
                foreach (var member in Members)
                {
                    FilteredMembers.Add(member);
                }
            }
            else
            {
                var filtered = Members.Where(m =>
                    m.FirstName.Contains(MemberSearchText, StringComparison.OrdinalIgnoreCase) ||
                    m.LastName.Contains(MemberSearchText, StringComparison.OrdinalIgnoreCase) ||
                    m.Email.Contains(MemberSearchText, StringComparison.OrdinalIgnoreCase) ||
                    $"{m.FirstName} {m.LastName}".Contains(MemberSearchText, StringComparison.OrdinalIgnoreCase)
                ).ToList();

                FilteredMembers.Clear();
                foreach (var member in filtered)
                {
                    FilteredMembers.Add(member);
                }
            }
        }

        public async Task LoadMembersAsync()
        {
            try
            {
                IsProcessing = true;

                // Remove the LIMIT 50 to get ALL members
                // Also ensure we're getting all members regardless of role
                var query = @"
                    SELECT member_id AS MemberId,
                           first_name AS FirstName,
                           last_name AS LastName,
                           email AS Email
                    FROM members
                    ORDER BY first_name, last_name";

                var membersList = await _databaseService.QueryAsync<Member>(query);

                Members.Clear();
                if (membersList != null && membersList.Any())
                {
                    foreach (var member in membersList)
                    {
                        Members.Add(member);
                    }

                    // Also update filtered members
                    FilterMembers();
                }
                else
                {
                    await Application.Current.MainPage.DisplayAlert("Info", "No members found in database.", "OK");
                }
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert("Error", $"Failed to load members: {ex.Message}", "OK");
            }
            finally
            {
                IsProcessing = false;
            }
        }

        // Alternative method to load members with pagination if you have too many members
        public async Task LoadMembersPaginatedAsync(int pageNumber = 1, int pageSize = 100)
        {
            try
            {
                IsProcessing = true;

                int offset = (pageNumber - 1) * pageSize;

                var query = @"
                    SELECT member_id AS MemberId,
                           first_name AS FirstName,
                           last_name AS LastName,
                           email AS Email
                    FROM members
                    ORDER BY first_name, last_name
                    LIMIT @limit OFFSET @offset";

                var parameters = new Dictionary<string, object>
                {
                    { "@limit", pageSize },
                    { "@offset", offset }
                };

                var membersList = await _databaseService.QueryAsync<Member>(query, parameters);

                if (pageNumber == 1)
                {
                    Members.Clear();
                }

                if (membersList != null)
                {
                    foreach (var member in membersList)
                    {
                        Members.Add(member);
                    }

                    FilterMembers();
                }
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert("Error", $"Failed to load members: {ex.Message}", "OK");
            }
            finally
            {
                IsProcessing = false;
            }
        }

        private async Task SearchByBarcodeAsync()
        {
            if (string.IsNullOrWhiteSpace(BarcodeInput)) return;

            try
            {
                IsProcessing = true;

                var query = @"
                    SELECT l.loan_id AS LoanId,
                           l.copy_id AS CopyId,
                           l.member_id AS MemberId,
                           l.checkout_datetime AS CheckoutDatetime,
                           l.due_datetime AS DueDatetime,
                           l.status AS Status,
                           t.title_name AS BookTitle,
                           bc.barcode AS Barcode,
                           m.first_name AS MemberFirstName,
                           m.last_name AS MemberLastName
                    FROM loans l
                    JOIN book_copies bc ON l.copy_id = bc.copy_id
                    JOIN titles t ON bc.title_id = t.title_id
                    JOIN members m ON l.member_id = m.member_id
                    WHERE bc.barcode = @barcode AND l.status = 'Checked Out'
                    LIMIT 1";

                var parameters = new Dictionary<string, object> { { "@barcode", BarcodeInput } };
                var loan = await _databaseService.QuerySingleAsync<Loan>(query, parameters);

                if (loan != null)
                {
                    // Find and select the member in the list
                    SelectedMember = Members.FirstOrDefault(m => m.MemberId == loan.MemberId);

                    if (SelectedMember == null)
                    {
                        // If member not in list, create temporary member object
                        SelectedMember = new Member
                        {
                            MemberId = loan.MemberId,
                            FirstName = loan.MemberFirstName,
                            LastName = loan.MemberLastName,
                            Email = "" // You might want to fetch this separately
                        };
                    }

                    await LoadMemberLoansAsync(SelectedMember);
                    await ReturnBookAsync(loan);
                }
                else
                {
                    await Application.Current.MainPage.DisplayAlert("Not Found", "No active loan found for this barcode.", "OK");
                }
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert("Error", $"Search failed: {ex.Message}", "OK");
            }
            finally
            {
                BarcodeInput = string.Empty;
                IsProcessing = false;
            }
        }

        private async Task LoadMemberLoansAsync(Member member)
        {
            if (member == null) return;

            try
            {
                IsProcessing = true;

                var query = @"
                    SELECT l.loan_id AS LoanId,
                           l.copy_id AS CopyId,
                           l.member_id AS MemberId,
                           l.checkout_datetime AS CheckoutDatetime,
                           l.due_datetime AS DueDatetime,
                           l.status AS Status,
                           t.title_name AS BookTitle,
                           bc.barcode AS Barcode,
                           CASE 
                               WHEN l.due_datetime < datetime('now') 
                               THEN julianday('now') - julianday(l.due_datetime)
                               ELSE 0 
                           END as DaysOverdue
                    FROM loans l
                    JOIN book_copies bc ON l.copy_id = bc.copy_id
                    JOIN titles t ON bc.title_id = t.title_id
                    WHERE l.member_id = @memberId AND l.status = 'Checked Out'
                    ORDER BY l.due_datetime";

                var parameters = new Dictionary<string, object> { { "@memberId", member.MemberId } };
                var loans = await _databaseService.QueryAsync<Loan>(query, parameters);

                MemberLoans.Clear();
                if (loans != null)
                {
                    foreach (var loan in loans)
                    {
                        MemberLoans.Add(loan);
                    }
                }

                await CalculateTotalFine(member.MemberId);
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert("Error", $"Failed to load loans: {ex.Message}", "OK");
            }
            finally
            {
                IsProcessing = false;
            }
        }

        private async Task CalculateTotalFine(int memberId)
        {
            try
            {
                var query = @"
                    SELECT COALESCE(SUM(penalty_amount), 0)
                    FROM penalties
                    WHERE member_id = @memberId AND resolved = 0";

                var parameters = new Dictionary<string, object> { { "@memberId", memberId } };
                var result = await _databaseService.ExecuteScalarAsync(query, parameters);

                TotalFine = Convert.ToDecimal(result);
                HasFines = TotalFine > 0;
            }
            catch
            {
                TotalFine = 0;
                HasFines = false;
            }
        }

        private async Task ReturnBookAsync(Loan loan)
        {
            if (loan == null) return;

            try
            {
                IsProcessing = true;

                // Update loan
                var updateQuery = @"
                    UPDATE loans 
                    SET return_datetime = @returnDate, status = 'Returned' 
                    WHERE loan_id = @loanId";

                var updateParams = new Dictionary<string, object>
                {
                    { "@loanId", loan.LoanId },
                    { "@returnDate", DateTime.Now }
                };
                await _databaseService.ExecuteNonQueryAsync(updateQuery, updateParams);

                // Check if overdue and create penalty
                if (loan.DueDatetime < DateTime.Now)
                {
                    var daysOverdue = (DateTime.Now - loan.DueDatetime).Days;
                    var fineAmount = daysOverdue * 0.50m; // $0.50 per day

                    var penaltyQuery = @"
                        INSERT INTO penalties (loan_id, member_id, penalty_amount, reason, resolved)
                        VALUES (@loanId, @memberId, @amount, 'Overdue return', 0)";

                    var penaltyParams = new Dictionary<string, object>
                    {
                        { "@loanId", loan.LoanId },
                        { "@memberId", loan.MemberId },
                        { "@amount", fineAmount }
                    };
                    await _databaseService.ExecuteNonQueryAsync(penaltyQuery, penaltyParams);
                }

                await Application.Current.MainPage.DisplayAlert("Success", "Book returned successfully", "OK");

                // Refresh loans
                if (SelectedMember != null)
                {
                    await LoadMemberLoansAsync(SelectedMember);
                }
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert("Error", $"Failed to return book: {ex.Message}", "OK");
            }
            finally
            {
                IsProcessing = false;
            }
        }

        private async Task PayFinesAsync()
        {
            if (SelectedMember == null || !HasFines) return;

            try
            {
                IsProcessing = true;

                var query = @"
                    UPDATE penalties 
                    SET resolved = 1 
                    WHERE member_id = @memberId AND resolved = 0";

                var parameters = new Dictionary<string, object> { { "@memberId", SelectedMember.MemberId } };
                await _databaseService.ExecuteNonQueryAsync(query, parameters);

                await Application.Current.MainPage.DisplayAlert("Success", $"Payment of ${TotalFine:F2} processed successfully", "OK");

                TotalFine = 0;
                HasFines = false;

                // Refresh loans
                await LoadMemberLoansAsync(SelectedMember);
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert("Error", $"Failed to process payment: {ex.Message}", "OK");
            }
            finally
            {
                IsProcessing = false;
            }
        }

        private async Task ReturnWithoutPaymentAsync()
        {
            var confirm = await Application.Current.MainPage.DisplayAlert(
                "Confirm",
                "Are you sure you want to return books without paying fines? Fines will remain on member's account.",
                "Yes", "No");

            if (confirm)
            {
                await Shell.Current.GoToAsync("..");
            }
        }
    }
}