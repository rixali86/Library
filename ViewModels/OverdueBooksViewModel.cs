using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Library.Models;
using Library.Services;
using System.Collections.ObjectModel;

namespace Library.ViewModels
{
    public partial class OverdueBooksViewModel : ObservableObject
    {
        private readonly IDatabaseService _databaseService;

        [ObservableProperty]
        private ObservableCollection<Loan> overdueLoans = new();

        [ObservableProperty]
        private bool isLoading = false;

        [ObservableProperty]
        private int totalOverdue;

        [ObservableProperty]
        private decimal totalFine;

        [ObservableProperty]
        private int avgDaysOverdue;

        [ObservableProperty]
        private List<string> filterOptions = new() { "All", "7+ Days", "14+ Days", "30+ Days" };

        [ObservableProperty]
        private string selectedFilter = "All";

        public IAsyncRelayCommand LoadOverdueLoansCommand { get; }
        public IAsyncRelayCommand ApplyFilterCommand { get; }
        public IAsyncRelayCommand<Loan> SendReminderCommand { get; }

        public OverdueBooksViewModel(IDatabaseService databaseService)
        {
            _databaseService = databaseService;

            LoadOverdueLoansCommand = new AsyncRelayCommand(LoadOverdueLoansAsync);
            ApplyFilterCommand = new AsyncRelayCommand(ApplyFilterAsync);
            SendReminderCommand = new AsyncRelayCommand<Loan>(SendReminderAsync);
        }

        public async Task LoadOverdueLoansAsync()
        {
            try
            {
                IsLoading = true;

                var query = @"
                    SELECT l.loan_id AS LoanId,
                           l.member_id AS MemberId,
                           l.copy_id AS CopyId,
                           l.checkout_datetime AS CheckoutDatetime,
                           l.due_datetime AS DueDatetime,
                           DATEDIFF(NOW(), l.due_datetime) AS DaysOverdue,
                           t.title_name AS BookTitle,
                           m.first_name AS MemberFirstName,
                           m.last_name AS MemberLastName,
                           m.email AS MemberEmail,
                           m.phone AS MemberPhone
                    FROM loans l
                    JOIN book_copies bc ON l.copy_id = bc.copy_id
                    JOIN titles t ON bc.title_id = t.title_id
                    JOIN members m ON l.member_id = m.member_id
                    WHERE l.status = 'Checked Out' AND l.due_datetime < NOW()";

                if (SelectedFilter != "All")
                {
                    var days = SelectedFilter switch
                    {
                        "7+ Days" => 7,
                        "14+ Days" => 14,
                        "30+ Days" => 30,
                        _ => 0
                    };

                    if (days > 0)
                    {
                        query += $" AND DATEDIFF(NOW(), l.due_datetime) >= {days}";
                    }
                }

                query += " ORDER BY l.due_datetime";

                var loans = await _databaseService.QueryAsync<Loan>(query);

                OverdueLoans.Clear();
                if (loans != null)
                {
                    foreach (var loan in loans)
                    {
                        OverdueLoans.Add(loan);
                    }
                }

                CalculateStatistics();
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert("Error", $"Failed to load overdue items: {ex.Message}", "OK");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void CalculateStatistics()
        {
            TotalOverdue = OverdueLoans.Count;
            TotalFine = OverdueLoans.Sum(l => l.FineAmount);

            if (TotalOverdue > 0)
            {
                AvgDaysOverdue = (int)Math.Round(OverdueLoans.Average(l => l.DaysOverdue));
            }
            else
            {
                AvgDaysOverdue = 0;
            }
        }

        private async Task ApplyFilterAsync()
        {
            await LoadOverdueLoansAsync();
        }

        private async Task SendReminderAsync(Loan loan)
        {
            if (loan == null) return;

            try
            {
                // In a real app, you'd send an email or SMS here
                var message = $"Reminder: Book '{loan.BookTitle}' is {loan.DaysOverdue} days overdue. Fine: ${loan.FineAmount:F2}";

                await Application.Current.MainPage.DisplayAlert(
                    "Reminder Sent",
                    $"Notification sent to {loan.MemberFullName}\n{message}",
                    "OK");

                // Log the reminder (you'd need a notifications table)
                try
                {
                    var logQuery = @"
                        INSERT INTO notifications (member_id, type, message, sent_date)
                        VALUES (@memberId, 'OverdueReminder', @message, NOW())";

                    var parameters = new Dictionary<string, object>
                    {
                        { "@memberId", loan.MemberId },
                        { "@message", message }
                    };

                    await _databaseService.ExecuteNonQueryAsync(logQuery, parameters);
                }
                catch
                {
                    // Notifications table might not exist - ignore
                }
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert("Error", $"Failed to send reminder: {ex.Message}", "OK");
            }
        }
    }
}