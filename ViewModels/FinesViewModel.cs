using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Library.Models;
using Library.Services;
using System.Collections.ObjectModel;

namespace Library.ViewModels
{
    public partial class FinesViewModel : ObservableObject
    {
        private readonly IDatabaseService _databaseService;

        [ObservableProperty]
        private ObservableCollection<FineItem> fines = new();

        [ObservableProperty]
        private bool isLoading = false;

        [ObservableProperty]
        private int pendingFinesCount;

        [ObservableProperty]
        private decimal totalPendingAmount;

        [ObservableProperty]
        private decimal totalCollected;

        [ObservableProperty]
        private List<string> filterOptions = new() { "All Fines", "Pending", "Resolved" };

        [ObservableProperty]
        private string selectedFilter = "All Fines";

        public IAsyncRelayCommand LoadFinesCommand { get; }
        public IAsyncRelayCommand<FineItem> ProcessFineCommand { get; }

        public FinesViewModel(IDatabaseService databaseService)
        {
            _databaseService = databaseService;

            LoadFinesCommand = new AsyncRelayCommand(LoadFinesAsync);
            ProcessFineCommand = new AsyncRelayCommand<FineItem>(ProcessFineAsync);
        }

        public async Task LoadFinesAsync()
        {
            try
            {
                IsLoading = true;

                var query = @"
                    SELECT p.penalty_id AS FineId,
                           p.loan_id AS LoanId,
                           p.member_id AS MemberId,
                           p.penalty_amount AS Amount,
                           p.reason AS Reason,
                           p.resolved AS IsResolved,
                           p.created_at AS CreatedAt,
                           CONCAT(m.first_name, ' ', m.last_name) AS MemberName,
                           t.title_name AS BookTitle
                    FROM penalties p
                    JOIN members m ON p.member_id = m.member_id
                    LEFT JOIN loans l ON p.loan_id = l.loan_id
                    LEFT JOIN book_copies bc ON l.copy_id = bc.copy_id
                    LEFT JOIN titles t ON bc.title_id = t.title_id";

                if (SelectedFilter == "Pending")
                {
                    query += " WHERE p.resolved = 0";
                }
                else if (SelectedFilter == "Resolved")
                {
                    query += " WHERE p.resolved = 1";
                }

                query += " ORDER BY p.created_at DESC";

                var finesList = await _databaseService.QueryAsync<FineItem>(query);

                Fines.Clear();
                if (finesList != null)
                {
                    foreach (var fine in finesList)
                    {
                        fine.AmountColor = fine.IsResolved ? "#4CAF50" : "#F44336";
                        fine.ActionText = fine.IsResolved ? "View" : "Pay";
                        fine.ActionColor = fine.IsResolved ? "#2196F3" : "#4CAF50";
                        fine.CanProcess = !fine.IsResolved;
                        Fines.Add(fine);
                    }
                }

                CalculateStatistics();
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert("Error", $"Failed to load fines: {ex.Message}", "OK");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void CalculateStatistics()
        {
            PendingFinesCount = Fines.Count(f => !f.IsResolved);
            TotalPendingAmount = Fines.Where(f => !f.IsResolved).Sum(f => f.Amount);
            TotalCollected = Fines.Where(f => f.IsResolved).Sum(f => f.Amount);
        }

        private async Task ProcessFineAsync(FineItem fine)
        {
            if (fine == null || fine.IsResolved) return;

            try
            {
                var confirm = await Application.Current.MainPage.DisplayAlert(
                    "Process Fine",
                    $"Mark fine of ${fine.Amount:F2} as paid?",
                    "Yes", "No");

                if (confirm)
                {
                    var query = "UPDATE penalties SET resolved = 1 WHERE penalty_id = @fineId";
                    var parameters = new Dictionary<string, object> { { "@fineId", fine.FineId } };

                    await _databaseService.ExecuteNonQueryAsync(query, parameters);
                    await LoadFinesAsync();

                    await Application.Current.MainPage.DisplayAlert("Success", "Fine marked as paid", "OK");
                }
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert("Error", $"Failed to process fine: {ex.Message}", "OK");
            }
        }
    }

    public partial class FineItem : ObservableObject
    {
        public int FineId { get; set; }
        public int LoanId { get; set; }
        public int MemberId { get; set; }
        public decimal Amount { get; set; }
        public string Reason { get; set; }
        public bool IsResolved { get; set; }
        public DateTime CreatedAt { get; set; }
        public string MemberName { get; set; }
        public string BookTitle { get; set; }

        // UI properties
        public string AmountColor { get; set; }
        public string ActionText { get; set; }
        public string ActionColor { get; set; }
        public bool CanProcess { get; set; }
    }
}