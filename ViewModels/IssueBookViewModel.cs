using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Library.Models;
using Library.Services;
using System.Collections.ObjectModel;

namespace Library.ViewModels
{
    public partial class IssueBookViewModel : ObservableObject
    {
        private readonly IDatabaseService _databaseService;
        private readonly IAuthService _authService;

        [ObservableProperty]
        private string memberSearchQuery = string.Empty;

        [ObservableProperty]
        private string bookSearchQuery = string.Empty;

        [ObservableProperty]
        private ObservableCollection<Member> foundMembers = new();

        [ObservableProperty]
        private ObservableCollection<Title> foundBooks = new();

        [ObservableProperty]
        private ObservableCollection<BookCopy> availableCopies = new();

        [ObservableProperty]
        private Member selectedMember;

        [ObservableProperty]
        private Title selectedBook;

        [ObservableProperty]
        private BookCopy selectedCopy;

        [ObservableProperty]
        private DateTime dueDate = DateTime.Now.AddDays(14);

        [ObservableProperty]
        private DateTime today = DateTime.Now;

        [ObservableProperty]
        private int loanPeriodIndex = 1; // Default 14 days

        [ObservableProperty]
        private bool isProcessing = false;

        // Computed property for CanIssue
        public bool CanIssue => SelectedMember != null && SelectedCopy != null;

        public IAsyncRelayCommand SearchMembersCommand { get; }
        public IAsyncRelayCommand SearchBooksCommand { get; }
        public IAsyncRelayCommand<Member> SelectMemberCommand { get; }
        public IAsyncRelayCommand<Title> SelectBookCommand { get; }
        public IAsyncRelayCommand IssueBookCommand { get; }

        public IssueBookViewModel(IDatabaseService databaseService, IAuthService authService)
        {
            _databaseService = databaseService;
            _authService = authService;

            SearchMembersCommand = new AsyncRelayCommand(SearchMembersAsync);
            SearchBooksCommand = new AsyncRelayCommand(SearchBooksAsync);
            SelectMemberCommand = new AsyncRelayCommand<Member>(SelectMemberAsync);
            SelectBookCommand = new AsyncRelayCommand<Title>(SelectBookAsync);
            IssueBookCommand = new AsyncRelayCommand(IssueBookAsync);

            // Set default due date based on loan period
            UpdateDueDate();
        }

        partial void OnLoanPeriodIndexChanged(int value)
        {
            UpdateDueDate();
        }

        private void UpdateDueDate()
        {
            var days = LoanPeriodIndex switch
            {
                0 => 7,
                1 => 14,
                2 => 21,
                3 => 30,
                _ => 14
            };
            DueDate = DateTime.Now.AddDays(days);
        }

        private async Task SearchMembersAsync()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(MemberSearchQuery))
                {
                    FoundMembers.Clear();
                    return;
                }

                var query = @"
                    SELECT member_id AS MemberId,
                           first_name AS FirstName,
                           last_name AS LastName,
                           email AS Email,
                           phone AS Phone
                    FROM members
                    WHERE (first_name LIKE @search 
                           OR last_name LIKE @search 
                           OR email LIKE @search)
                          AND role = 'Member'
                    LIMIT 10";

                var parameters = new Dictionary<string, object> { { "@search", $"%{MemberSearchQuery}%" } };
                var members = await _databaseService.QueryAsync<Member>(query, parameters);

                FoundMembers.Clear();
                if (members != null)
                {
                    foreach (var member in members)
                    {
                        FoundMembers.Add(member);
                    }
                }
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert("Error", $"Search failed: {ex.Message}", "OK");
            }
        }

        private async Task SearchBooksAsync()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(BookSearchQuery))
                {
                    FoundBooks.Clear();
                    return;
                }

                var query = @"
                    SELECT t.*, 
                           GROUP_CONCAT(DISTINCT a.author_name SEPARATOR ', ') as Author
                    FROM titles t
                    LEFT JOIN title_authors ta ON t.title_id = ta.title_id
                    LEFT JOIN authors a ON ta.author_id = a.author_id
                    WHERE t.title_name LIKE @search OR t.isbn LIKE @search
                    GROUP BY t.title_id
                    LIMIT 10";

                var parameters = new Dictionary<string, object> { { "@search", $"%{BookSearchQuery}%" } };
                var books = await _databaseService.QueryAsync<Title>(query, parameters);

                FoundBooks.Clear();
                if (books != null)
                {
                    foreach (var book in books)
                    {
                        FoundBooks.Add(book);
                    }
                }
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert("Error", $"Search failed: {ex.Message}", "OK");
            }
        }

        private async Task SelectMemberAsync(Member member)
        {
            if (member == null) return;

            SelectedMember = member;

            // Check if member can borrow more books
            var canBorrow = await CanMemberBorrow(member.MemberId);
            if (!canBorrow)
            {
                await Application.Current.MainPage.DisplayAlert(
                    "Cannot Issue",
                    "This member has reached the maximum number of books or has overdue items.",
                    "OK");
                SelectedMember = null;
            }

            FoundMembers.Clear();
            MemberSearchQuery = string.Empty;
        }

        private async Task<bool> CanMemberBorrow(int memberId)
        {
            try
            {
                // Check active loans count
                var loanQuery = @"
                    SELECT COUNT(*) 
                    FROM loans 
                    WHERE member_id = @memberId AND status = 'Checked Out'";

                var parameters = new Dictionary<string, object> { { "@memberId", memberId } };
                var activeLoans = Convert.ToInt32(await _databaseService.ExecuteScalarAsync(loanQuery, parameters));

                // Check for overdue items
                var overdueQuery = @"
                    SELECT COUNT(*) 
                    FROM loans 
                    WHERE member_id = @memberId 
                          AND status = 'Checked Out' 
                          AND due_datetime < NOW()";

                var overdueItems = Convert.ToInt32(await _databaseService.ExecuteScalarAsync(overdueQuery, parameters));

                // Check member status
                var statusQuery = @"
                    SELECT status 
                    FROM member_accounts 
                    WHERE member_id = @memberId";

                var status = await _databaseService.ExecuteScalarAsync(statusQuery, parameters);

                return activeLoans < 5 && overdueItems == 0 && status?.ToString() == "Active";
            }
            catch
            {
                return false;
            }
        }

        private async Task SelectBookAsync(Title book)
        {
            if (book == null) return;

            SelectedBook = book;

            // Load available copies (NULL-safe)
            var query = @"
SELECT bc.*, b.branch_name
FROM book_copies bc
JOIN branches b ON bc.branch_id = b.branch_id
LEFT JOIN loans l ON bc.copy_id = l.copy_id AND l.status = 'Checked Out'
WHERE bc.title_id = @titleId
  AND l.copy_id IS NULL";

            var parameters = new Dictionary<string, object> { { "titleId", book.TitleId } };
            var copies = await _databaseService.QueryAsync<BookCopy>(query, parameters);

            AvailableCopies.Clear();
            if (copies != null)
            {
                foreach (var copy in copies)
                {
                    AvailableCopies.Add(copy);
                }
            }

            if (AvailableCopies.Count == 0)
            {
                await Application.Current.MainPage.DisplayAlert(
                    "No Copies Available",
                    "This book has no available copies at the moment.",
                    "OK");
            }

            FoundBooks.Clear();
            BookSearchQuery = string.Empty;
        }



        private async Task IssueBookAsync()
        {
            if (!CanIssue) return;

            try
            {
                IsProcessing = true;

                var query = @"
                    INSERT INTO loans (copy_id, member_id, checkout_datetime, due_datetime, status)
                    VALUES (@copyId, @memberId, @checkoutDate, @dueDate, 'Checked Out')";

                var parameters = new Dictionary<string, object>
                {
                    { "@copyId", SelectedCopy.CopyId },
                    { "@memberId", SelectedMember.MemberId },
                    { "@checkoutDate", DateTime.Now },
                    { "@dueDate", DueDate }
                };

                await _databaseService.ExecuteNonQueryAsync(query, parameters);

                await Application.Current.MainPage.DisplayAlert(
                    "Success",
                    $"Book issued successfully to {SelectedMember.FirstName} {SelectedMember.LastName}",
                    "OK");

                // Clear selections
                SelectedMember = null;
                SelectedBook = null;
                SelectedCopy = null;
                AvailableCopies.Clear();

                // Navigate back
                await Shell.Current.GoToAsync("..");
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert("Error", $"Failed to issue book: {ex.Message}", "OK");
            }
            finally
            {
                IsProcessing = false;
            }
        }
    }
}