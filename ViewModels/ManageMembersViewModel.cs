using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Library.Models;
using Library.Services;
using System.Collections.ObjectModel;

namespace Library.ViewModels
{
    public partial class ManageMembersViewModel : ObservableObject
    {
        private readonly IDatabaseService _databaseService;
        private readonly IAuthService _authService;

        [ObservableProperty]
        private string searchQuery = string.Empty;

        [ObservableProperty]
        private ObservableCollection<Member> members = new();

        [ObservableProperty]
        private bool isLoading = false;

        [ObservableProperty]
        private int totalMembers;

        [ObservableProperty]
        private int activeMembers;

        [ObservableProperty]
        private int suspendedMembers;

        [ObservableProperty]
        private List<string> filterOptions = new() { "All", "Active", "Suspended", "Expired" };

        [ObservableProperty]
        private string selectedFilter = "All";

        public IAsyncRelayCommand SearchCommand { get; }
        public IAsyncRelayCommand LoadMembersCommand { get; }
        public IAsyncRelayCommand<Member> EditMemberCommand { get; }
        public IAsyncRelayCommand<Member> ViewLoansCommand { get; }
        public IAsyncRelayCommand<Member> ToggleStatusCommand { get; }
        public IAsyncRelayCommand ApplyFilterCommand { get; }

        public ManageMembersViewModel(IDatabaseService databaseService, IAuthService authService)
        {
            _databaseService = databaseService;
            _authService = authService;

            SearchCommand = new AsyncRelayCommand(LoadMembersAsync);
            LoadMembersCommand = new AsyncRelayCommand(LoadMembersAsync);
            EditMemberCommand = new AsyncRelayCommand<Member>(EditMemberAsync);
            ViewLoansCommand = new AsyncRelayCommand<Member>(ViewLoansAsync);
            ToggleStatusCommand = new AsyncRelayCommand<Member>(ToggleMemberStatusAsync);
            ApplyFilterCommand = new AsyncRelayCommand(ApplyFilterAsync);
        }

        public async Task LoadMembersAsync()
        {
            try
            {
                IsLoading = true;
                var query = BuildSearchQuery();
                var parameters = new Dictionary<string, object>();

                if (!string.IsNullOrWhiteSpace(SearchQuery))
                {
                    parameters["@search"] = $"%{SearchQuery}%";
                }

                var membersList = await _databaseService.QueryAsync<Member>(query, parameters);

                if (membersList != null)
                {
                    // Load account status for each member
                    foreach (var member in membersList)
                    {
                        await LoadMemberAccountStatus(member);
                    }

                    Members = new ObservableCollection<Member>(membersList);
                }

                await LoadStatisticsAsync();
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert("Error", $"Failed to load members: {ex.Message}", "OK");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private string BuildSearchQuery()
        {
            var baseQuery = @"
                SELECT m.* 
                FROM members m
                WHERE 1=1";

            if (!string.IsNullOrWhiteSpace(SearchQuery))
            {
                baseQuery += @" AND (m.first_name LIKE @search 
                               OR m.last_name LIKE @search 
                               OR m.email LIKE @search 
                               OR m.phone LIKE @search)";
            }

            if (SelectedFilter != "All")
            {
                baseQuery += @" AND m.member_id IN (
                    SELECT member_id FROM member_accounts WHERE status = @status
                )";
            }

            baseQuery += " ORDER BY m.created_at DESC LIMIT 50";
            return baseQuery;
        }

        private async Task LoadMemberAccountStatus(Member member)
        {
            try
            {
                var query = "SELECT status FROM member_accounts WHERE member_id = @memberId LIMIT 1";
                var parameters = new Dictionary<string, object> { { "@memberId", member.MemberId } };
                var status = await _databaseService.ExecuteScalarAsync(query, parameters);

                // Add a dynamic property for account status
                var property = member.GetType().GetProperty("AccountStatus");
                if (property == null)
                {
                    // You might want to extend the Member class to include AccountStatus
                    // For now, we'll handle it in the UI with a converter
                }
            }
            catch { }
        }

        private async Task LoadStatisticsAsync()
        {
            try
            {
                var totalQuery = "SELECT COUNT(*) FROM members WHERE role = 'Member'";
                TotalMembers = Convert.ToInt32(await _databaseService.ExecuteScalarAsync(totalQuery));

                var activeQuery = @"
                    SELECT COUNT(*) 
                    FROM members m
                    JOIN member_accounts a ON m.member_id = a.member_id
                    WHERE a.status = 'Active'";
                ActiveMembers = Convert.ToInt32(await _databaseService.ExecuteScalarAsync(activeQuery));

                var suspendedQuery = @"
                    SELECT COUNT(*) 
                    FROM members m
                    JOIN member_accounts a ON m.member_id = a.member_id
                    WHERE a.status = 'Suspended'";
                SuspendedMembers = Convert.ToInt32(await _databaseService.ExecuteScalarAsync(suspendedQuery));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Stats error: {ex.Message}");
            }
        }

        private async Task EditMemberAsync(Member member)
        {
            if (member == null) return;

            var parameters = new Dictionary<string, object>
            {
                ["member"] = member
            };
            await Shell.Current.GoToAsync("EditMember", parameters);
        }

        private async Task ViewLoansAsync(Member member)
        {
            if (member == null) return;

            var parameters = new Dictionary<string, object>
            {
                ["memberId"] = member.MemberId,
                ["memberName"] = $"{member.FirstName} {member.LastName}"
            };
            await Shell.Current.GoToAsync("MemberLoans", parameters);
        }

        private async Task ToggleMemberStatusAsync(Member member)
        {
            if (member == null) return;

            var currentStatus = await GetMemberAccountStatus(member.MemberId);
            var newStatus = currentStatus == "Active" ? "Suspended" : "Active";

            var confirm = await Application.Current.MainPage.DisplayAlert(
                "Confirm Status Change",
                $"Are you sure you want to {newStatus.ToLower()} this member?",
                "Yes", "No");

            if (confirm)
            {
                try
                {
                    var query = @"
                        UPDATE member_accounts 
                        SET status = @status 
                        WHERE member_id = @memberId";

                    var parameters = new Dictionary<string, object>
                    {
                        { "@status", newStatus },
                        { "@memberId", member.MemberId }
                    };

                    await _databaseService.ExecuteNonQueryAsync(query, parameters);
                    await LoadMembersAsync();
                }
                catch (Exception ex)
                {
                    await Application.Current.MainPage.DisplayAlert("Error", $"Failed to update status: {ex.Message}", "OK");
                }
            }
        }

        private async Task<string> GetMemberAccountStatus(int memberId)
        {
            try
            {
                var query = "SELECT status FROM member_accounts WHERE member_id = @memberId";
                var parameters = new Dictionary<string, object> { { "@memberId", memberId } };
                return (await _databaseService.ExecuteScalarAsync(query, parameters))?.ToString() ?? "Active";
            }
            catch
            {
                return "Active";
            }
        }

        private async Task ApplyFilterAsync()
        {
            await LoadMembersAsync();
        }
    }
}