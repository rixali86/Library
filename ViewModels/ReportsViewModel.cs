using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Library.Models;
using Library.Services;
using System.Collections.ObjectModel;
using System.Text;

namespace Library.ViewModels
{
    public partial class ReportsViewModel : ObservableObject
    {
        private readonly IDatabaseService _databaseService;

        [ObservableProperty]
        private DateTime startDate = DateTime.Now.AddMonths(-1);

        [ObservableProperty]
        private DateTime endDate = DateTime.Now;

        [ObservableProperty]
        private int totalLoans;

        [ObservableProperty]
        private decimal totalFines;

        [ObservableProperty]
        private int activeMembers;

        [ObservableProperty]
        private int newMembers;

        [ObservableProperty]
        private int activeBorrowers;

        [ObservableProperty]
        private ObservableCollection<PopularBook> popularBooks = new();

        [ObservableProperty]
        private bool isLoading = false;

        public IAsyncRelayCommand ApplyDateRangeCommand { get; }
        public IAsyncRelayCommand ExportToCsvCommand { get; }
        public IAsyncRelayCommand PrintReportCommand { get; }

        public ReportsViewModel(IDatabaseService databaseService)
        {
            _databaseService = databaseService;

            ApplyDateRangeCommand = new AsyncRelayCommand(LoadReportsAsync);
            ExportToCsvCommand = new AsyncRelayCommand(ExportToCsvAsync);
            PrintReportCommand = new AsyncRelayCommand(PrintReportAsync);
        }

        public async Task LoadReportsAsync()
        {
            try
            {
                IsLoading = true;

                await LoadLoanStatisticsAsync();
                await LoadPopularBooksAsync();
                await LoadMemberStatisticsAsync();
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert("Error", $"Failed to load reports: {ex.Message}", "OK");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task LoadLoanStatisticsAsync()
        {
            // Total loans in date range
            var loansQuery = @"
                SELECT COUNT(*) 
                FROM loans 
                WHERE checkout_datetime BETWEEN @startDate AND @endDate";

            var parameters = new Dictionary<string, object>
            {
                { "@startDate", StartDate },
                { "@endDate", EndDate }
            };

            TotalLoans = Convert.ToInt32(await _databaseService.ExecuteScalarAsync(loansQuery, parameters));

            // Total fines collected
            var finesQuery = @"
                SELECT COALESCE(SUM(penalty_amount), 0)
                FROM penalties
                WHERE created_at BETWEEN @startDate AND @endDate AND resolved = 1";

            TotalFines = Convert.ToDecimal(await _databaseService.ExecuteScalarAsync(finesQuery, parameters));
        }

        private async Task LoadPopularBooksAsync()
        {
            var query = @"
                SELECT t.title_id, 
                       t.title_name AS TitleName,
                       COUNT(l.loan_id) AS LoanCount
                FROM titles t
                JOIN book_copies bc ON t.title_id = bc.title_id
                LEFT JOIN loans l ON bc.copy_id = l.copy_id 
                    AND l.checkout_datetime BETWEEN @startDate AND @endDate
                GROUP BY t.title_id, t.title_name
                HAVING LoanCount > 0
                ORDER BY LoanCount DESC
                LIMIT 10";

            var parameters = new Dictionary<string, object>
            {
                { "@startDate", StartDate },
                { "@endDate", EndDate }
            };

            var books = await _databaseService.QueryAsync<PopularBook>(query, parameters);

            PopularBooks.Clear();
            if (books != null)
            {
                foreach (var book in books)
                {
                    PopularBooks.Add(book);
                }
            }
        }

        private async Task LoadMemberStatisticsAsync()
        {
            // Active members
            var activeQuery = @"
                SELECT COUNT(DISTINCT m.member_id)
                FROM members m
                JOIN member_accounts a ON m.member_id = a.member_id
                WHERE a.status = 'Active'";

            ActiveMembers = Convert.ToInt32(await _databaseService.ExecuteScalarAsync(activeQuery));

            // New members in date range
            var newQuery = @"
                SELECT COUNT(*)
                FROM members
                WHERE registered_date BETWEEN @startDate AND @endDate";

            var parameters = new Dictionary<string, object>
            {
                { "@startDate", StartDate },
                { "@endDate", EndDate }
            };

            NewMembers = Convert.ToInt32(await _databaseService.ExecuteScalarAsync(newQuery, parameters));

            // Active borrowers (members with loans in date range)
            var borrowersQuery = @"
                SELECT COUNT(DISTINCT member_id)
                FROM loans
                WHERE checkout_datetime BETWEEN @startDate AND @endDate";

            ActiveBorrowers = Convert.ToInt32(await _databaseService.ExecuteScalarAsync(borrowersQuery, parameters));
        }

        private async Task ExportToCsvAsync()
        {
            try
            {
                var csv = new StringBuilder();

                // Header
                csv.AppendLine("Report Period,Start Date,End Date,Total Loans,Total Fines,Active Members,New Members,Active Borrowers");
                csv.AppendLine($"Library Statistics,{StartDate:d},{EndDate:d},{TotalLoans},{TotalFines:C},{ActiveMembers},{NewMembers},{ActiveBorrowers}");

                csv.AppendLine();
                csv.AppendLine("Popular Books,Title,Loans");
                foreach (var book in PopularBooks)
                {
                    csv.AppendLine($"Popular Books,{book.TitleName},{book.LoanCount}");
                }

                // In a real app, you'd save this file
                var fileName = $"Library_Report_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
                var filePath = Path.Combine(FileSystem.CacheDirectory, fileName);
                await File.WriteAllTextAsync(filePath, csv.ToString());

                await Application.Current.MainPage.DisplayAlert(
                    "Export Complete",
                    $"Report saved to:\n{filePath}",
                    "OK");

                // Share the file
                await Share.Default.RequestAsync(new ShareFileRequest
                {
                    Title = "Export Library Report",
                    File = new ShareFile(filePath)
                });
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert("Error", $"Failed to export: {ex.Message}", "OK");
            }
        }

        private async Task PrintReportAsync()
        {
            // In a real app, you'd generate a PDF and print it
            await Application.Current.MainPage.DisplayAlert(
                "Print Report",
                "Print functionality would open a print dialog here.",
                "OK");
        }
    }

    public class PopularBook
    {
        public int TitleId { get; set; }
        public string TitleName { get; set; }
        public int LoanCount { get; set; }
    }
}