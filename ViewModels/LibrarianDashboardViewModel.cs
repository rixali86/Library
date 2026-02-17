using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Library.Models;
using Library.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace Library.ViewModels
{
    public partial class LibrarianDashboardViewModel : ObservableObject
    {
        private readonly IAuthService _authService;
        private readonly ILibraryService _libraryService;
        private readonly IDatabaseService _databaseService;

        [ObservableProperty]
        private string userName = string.Empty;

        [ObservableProperty]
        private int totalMembers;

        [ObservableProperty]
        private int activeLoans;

        [ObservableProperty]
        private int overdueLoans;

        [ObservableProperty]
        private int availableBooks;

        [ObservableProperty]
        private decimal totalFines;

        // ── Pending book requests notification ────────────────────────────────
        [ObservableProperty]
        private int pendingRequestCount;

        [ObservableProperty]
        private bool hasPendingRequests;

        [ObservableProperty]
        private string pendingRequestLabel = string.Empty;
        // ─────────────────────────────────────────────────────────────────────

        [ObservableProperty]
        private ObservableCollection<Loan> recentLoans = new();

        [ObservableProperty]
        private ObservableCollection<Member> recentMembers = new();

        [ObservableProperty]
        private ObservableCollection<BookCopy> popularBooks = new();

        [ObservableProperty]
        private bool isLoading = false;

        [ObservableProperty]
        private DateTime lastUpdated = DateTime.Now;

        public IAsyncRelayCommand RefreshCommand { get; }
        public IAsyncRelayCommand<Loan> ViewLoanDetailsCommand { get; }
        public IAsyncRelayCommand<Member> ViewMemberDetailsCommand { get; }

        public LibrarianDashboardViewModel(
            IAuthService authService,
            ILibraryService libraryService,
            IDatabaseService databaseService)
        {
            _authService = authService;
            _libraryService = libraryService;
            _databaseService = databaseService;

            RefreshCommand = new AsyncRelayCommand(LoadDashboardDataAsync);
            ViewLoanDetailsCommand = new AsyncRelayCommand<Loan>(ViewLoanDetailsAsync);
            ViewMemberDetailsCommand = new AsyncRelayCommand<Member>(ViewMemberDetailsAsync);

            Task.Run(async () => await LoadDashboardDataAsync());
        }

        private async Task LoadDashboardDataAsync()
        {
            if (IsLoading) return;

            IsLoading = true;
            try
            {
                // Get current librarian info
                var currentUser = await _authService.GetCurrentUserAsync();
                if (currentUser != null)
                    UserName = currentUser.FirstName;

                // Load statistics + pending requests badge
                await LoadStatisticsAsync();
                await LoadPendingRequestsCountAsync();

                // Load recent data
                await LoadRecentLoansAsync();
                await LoadRecentMembersAsync();
                await LoadPopularBooksAsync();

                LastUpdated = DateTime.Now;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Dashboard load error: {ex.Message}");
                await Application.Current.MainPage.DisplayAlert(
                    "Error", "Failed to load dashboard data", "OK");
            }
            finally
            {
                IsLoading = false;
            }
        }

        // ── Pending requests badge ────────────────────────────────────────────

        private async Task LoadPendingRequestsCountAsync()
        {
            try
            {
                const string sql = "SELECT COUNT(*) FROM book_requests WHERE status = 'Pending';";
                var scalar = await _databaseService.ExecuteScalarAsync(sql,
                    new Dictionary<string, object>());

                PendingRequestCount = scalar is not null ? Convert.ToInt32(scalar) : 0;
                HasPendingRequests = PendingRequestCount > 0;
                PendingRequestLabel = PendingRequestCount == 1
                    ? "1 new book request"
                    : $"{PendingRequestCount} new book requests";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"PendingRequests count error: {ex.Message}");
            }
        }

        // ── Navigate to requests page ─────────────────────────────────────────

        [RelayCommand]
        private async Task ViewPendingRequestsAsync()
        {
            await Shell.Current.GoToAsync("BookRequests");
        }

        // ── Statistics ────────────────────────────────────────────────────────

        private async Task LoadStatisticsAsync()
        {
            var memberCountQuery = "SELECT COUNT(*) FROM members WHERE role = 'Member'";
            TotalMembers = Convert.ToInt32(await _databaseService.ExecuteScalarAsync(memberCountQuery));

            var activeLoansQuery = "SELECT COUNT(*) FROM loans WHERE status = 'Checked Out'";
            ActiveLoans = Convert.ToInt32(await _databaseService.ExecuteScalarAsync(activeLoansQuery));

            var overdueLoansQuery = @"
SELECT COUNT(*) 
FROM loans 
WHERE status = 'Checked Out' 
AND due_datetime < NOW()";
            OverdueLoans = Convert.ToInt32(await _databaseService.ExecuteScalarAsync(overdueLoansQuery));

            var availableBooksQuery = @"
SELECT COUNT(*) 
FROM book_copies bc
LEFT JOIN loans l ON bc.copy_id = l.copy_id AND l.status = 'Checked Out'
WHERE l.loan_id IS NULL";
            AvailableBooks = Convert.ToInt32(await _databaseService.ExecuteScalarAsync(availableBooksQuery));

            var finesQuery = "SELECT COALESCE(SUM(penalty_amount), 0) FROM penalties WHERE resolved = 1";
            TotalFines = Convert.ToDecimal(await _databaseService.ExecuteScalarAsync(finesQuery));
        }

        // ── Recent loans ──────────────────────────────────────────────────────

        private async Task LoadRecentLoansAsync()
        {
            var query = @"
SELECT l.*, 
       t.title_name AS BookTitle,
       m.first_name AS MemberFirstName,
       m.last_name  AS MemberLastName
FROM loans l
JOIN book_copies bc ON l.copy_id    = bc.copy_id
JOIN titles      t  ON bc.title_id  = t.title_id
JOIN members     m  ON l.member_id  = m.member_id
ORDER BY l.checkout_datetime DESC
LIMIT 5";

            var loans = await _databaseService.QueryAsync<Loan>(query);
            RecentLoans.Clear();
            if (loans != null)
                foreach (var loan in loans)
                    RecentLoans.Add(loan);
        }

        // ── Recent members ────────────────────────────────────────────────────

        private async Task LoadRecentMembersAsync()
        {
            var query = @"
SELECT member_id       AS MemberId,
       first_name      AS FirstName,
       last_name       AS LastName,
       email           AS Email,
       registered_date AS RegisteredDate
FROM members
WHERE role = 'Member'
ORDER BY registered_date DESC
LIMIT 5";

            var members = await _databaseService.QueryAsync<Member>(query);
            RecentMembers.Clear();
            if (members != null)
                foreach (var member in members)
                    RecentMembers.Add(member);
        }

        // ── Popular books ─────────────────────────────────────────────────────

        private async Task LoadPopularBooksAsync()
        {
            var query = @"
SELECT bc.*, t.title_name, COUNT(l.loan_id) AS LoanCount
FROM book_copies bc
JOIN titles t ON bc.title_id = t.title_id
LEFT JOIN loans l ON bc.copy_id = l.copy_id
GROUP BY bc.copy_id
ORDER BY LoanCount DESC
LIMIT 5";

            var books = await _databaseService.QueryAsync<BookCopy>(query);
            PopularBooks.Clear();
            if (books != null)
                foreach (var book in books)
                    PopularBooks.Add(book);
        }

        // ── Navigation ────────────────────────────────────────────────────────

        private async Task ViewLoanDetailsAsync(Loan loan)
        {
            if (loan == null) return;
            await Shell.Current.GoToAsync("LoanDetails",
                new Dictionary<string, object> { ["loan"] = loan });
        }

        private async Task ViewMemberDetailsAsync(Member member)
        {
            if (member == null) return;
            await Shell.Current.GoToAsync("MemberLoans",
                new Dictionary<string, object> { ["member"] = member });
        }

        [RelayCommand]
        private async Task QuickIssueBookAsync() =>
            await Shell.Current.GoToAsync("IssueBook");

        [RelayCommand]
        private async Task QuickReturnBookAsync() =>
            await Shell.Current.GoToAsync("ReturnBook");

        [RelayCommand]
        private async Task ViewAllReportsAsync() =>
            await Shell.Current.GoToAsync("Reports");
    }
}