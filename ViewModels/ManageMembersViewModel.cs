using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Library.Models;
using Library.Services;
using System.Collections.ObjectModel;
using System.Windows.Input;

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

        public ICommand AddMetaCommand { get; }
        public ICommand UpdateMetaCommand { get; }
        public ICommand DeleteMetaCommand { get; }

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

            // ── Metadata commands ──────────────────────────────────────────
            AddMetaCommand = new Command<Member>(async member =>
            {
                if (member is null) return;

                string key = await Shell.Current.DisplayPromptAsync(
                    "Add Metadata", "Enter meta key:", placeholder: "e.g. membership_level");
                if (string.IsNullOrWhiteSpace(key)) return;

                string value = await Shell.Current.DisplayPromptAsync(
                    "Add Metadata", $"Enter value for '{key}':", placeholder: "e.g. gold");
                if (string.IsNullOrWhiteSpace(value)) return;

                // ✅ Add debug here
                System.Diagnostics.Debug.WriteLine($"=== Saving meta: MemberId={member.MemberId}, key={key}, value={value}");

                await AddMemberMetaAsync(member.MemberId, key, value);

                System.Diagnostics.Debug.WriteLine($"=== Save done, now loading meta...");

                await LoadMetaForMemberAsync(member);

                System.Diagnostics.Debug.WriteLine($"=== Load done. MetaRows.Count = {member.MetaRows.Count}");
            });

            UpdateMetaCommand = new Command<Member>(async member =>
            {
                if (member is null) return;

                string key = await Shell.Current.DisplayPromptAsync(
                    "Edit Metadata", "Enter meta key to update:", placeholder: "e.g. membership_level");
                if (string.IsNullOrWhiteSpace(key)) return;

                string value = await Shell.Current.DisplayPromptAsync(
                    "Edit Metadata", $"New value for '{key}':", placeholder: "e.g. platinum");
                if (string.IsNullOrWhiteSpace(value)) return;

                await UpdateMemberMetaAsync(member.MemberId, key, value);
                await LoadMetaForMemberAsync(member);  // ✅ refreshes chips instantly
            });

            DeleteMetaCommand = new Command<Member>(async member =>
            {
                if (member is null) return;

                string key = await Shell.Current.DisplayPromptAsync(
                    "Delete Metadata", "Enter meta key to delete:", placeholder: "e.g. late_fees");
                if (string.IsNullOrWhiteSpace(key)) return;

                bool confirm = await Shell.Current.DisplayAlert(
                    "Confirm Delete",
                    $"Delete '{key}' from {member.FullName}?",
                    "Delete", "Cancel");
                if (!confirm) return;

                await DeleteMemberMetaAsync(member.MemberId, key);
                await LoadMetaForMemberAsync(member);  // ✅ refreshes chips instantly
            });
        }

        // ── Meta DB operations using _databaseService ─────────────────────

        private async Task AddMemberMetaAsync(int memberId, string key, string value)
        {
            try
            {
                // Check if key already exists for this member
                var checkQuery = @"
                    SELECT COUNT(*) FROM member_metadata 
                    WHERE member_id = @memberId AND meta_key = @key";

                var checkParams = new Dictionary<string, object>
                {
                    { "@memberId", memberId },
                    { "@key",      key      }
                };

                var exists = Convert.ToInt32(
                    await _databaseService.ExecuteScalarAsync(checkQuery, checkParams)) > 0;

                if (exists)
                {
                    await Shell.Current.DisplayAlert(
                        "Duplicate Key",
                        $"Meta key '{key}' already exists for this member. Use Edit instead.",
                        "OK");
                    return;
                }

                var query = @"
                    INSERT INTO member_metadata (member_id, meta_key, meta_value, created_at)
                    VALUES (@memberId, @key, @value, @createdAt)";

                var parameters = new Dictionary<string, object>
                {
                    { "@memberId",  memberId          },
                    { "@key",       key               },
                    { "@value",     value             },
                    { "@createdAt", DateTime.UtcNow   }
                };

                await _databaseService.ExecuteNonQueryAsync(query, parameters);

                await Shell.Current.DisplayAlert(
                    "Success", $"Added '{key}' = '{value}'", "OK");
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert(
                    "Error", $"Failed to add metadata: {ex.Message}", "OK");
            }
        }

        private async Task UpdateMemberMetaAsync(int memberId, string key, string value)
        {
            try
            {
                // Upsert — update if exists, insert if not
                var checkQuery = @"
                    SELECT COUNT(*) FROM member_metadata 
                    WHERE member_id = @memberId AND meta_key = @key";

                var checkParams = new Dictionary<string, object>
                {
                    { "@memberId", memberId },
                    { "@key",      key      }
                };

                var exists = Convert.ToInt32(
                    await _databaseService.ExecuteScalarAsync(checkQuery, checkParams)) > 0;

                string query;

                if (exists)
                {
                    query = @"
                        UPDATE member_metadata 
                        SET meta_value = @value
                        WHERE member_id = @memberId AND meta_key = @key";
                }
                else
                {
                    // WordPress-style upsert — insert if key doesn't exist
                    query = @"
                        INSERT INTO member_metadata (member_id, meta_key, meta_value, created_at)
                        VALUES (@memberId, @key, @value, @createdAt)";
                }

                var parameters = new Dictionary<string, object>
                {
                    { "@memberId",  memberId        },
                    { "@key",       key             },
                    { "@value",     value           },
                    { "@createdAt", DateTime.UtcNow }
                };

                await _databaseService.ExecuteNonQueryAsync(query, parameters);

                await Shell.Current.DisplayAlert(
                    "Success",
                    exists ? $"Updated '{key}' = '{value}'"
                           : $"Inserted '{key}' = '{value}' (key was new)",
                    "OK");
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert(
                    "Error", $"Failed to update metadata: {ex.Message}", "OK");
            }
        }

        private async Task DeleteMemberMetaAsync(int memberId, string key)
        {
            try
            {
                var query = @"
                    DELETE FROM member_metadata 
                    WHERE member_id = @memberId AND meta_key = @key";

                var parameters = new Dictionary<string, object>
                {
                    { "@memberId", memberId },
                    { "@key",      key      }
                };

                await _databaseService.ExecuteNonQueryAsync(query, parameters);

                await Shell.Current.DisplayAlert(
                    "Success", $"Deleted metadata key '{key}'", "OK");
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert(
                    "Error", $"Failed to delete metadata: {ex.Message}", "OK");
            }
        }

        // ── Existing methods (unchanged) ───────────────────────────────────

        public async Task LoadMembersAsync()
        {
            try
            {
                IsLoading = true;
                var query = BuildSearchQuery();
                var parameters = new Dictionary<string, object>();

                if (!string.IsNullOrWhiteSpace(SearchQuery))
                    parameters["@search"] = $"%{SearchQuery}%";

                var membersList = await _databaseService.QueryAsync<Member>(query, parameters);

                if (membersList != null)
                {
                    foreach (var member in membersList)
                        await LoadMemberAccountStatus(member);

                    Members = new ObservableCollection<Member>(membersList);
                }

                await LoadStatisticsAsync();
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert(
                    "Error", $"Failed to load members: {ex.Message}", "OK");
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
                baseQuery += @" AND (m.first_name LIKE @search 
                               OR m.last_name  LIKE @search 
                               OR m.email      LIKE @search 
                               OR m.phone      LIKE @search)";

            if (SelectedFilter != "All")
                baseQuery += @" AND m.member_id IN (
                    SELECT member_id FROM member_accounts WHERE status = @status)";

            baseQuery += " ORDER BY m.created_at DESC LIMIT 50";
            return baseQuery;
        }

        private async Task LoadMemberAccountStatus(Member member)
        {
            try
            {
                var query = "SELECT status FROM member_accounts WHERE member_id = @memberId LIMIT 1";
                var parameters = new Dictionary<string, object> { { "@memberId", member.MemberId } };
                await _databaseService.ExecuteScalarAsync(query, parameters);
            }
            catch { }
        }

        private async Task LoadStatisticsAsync()
        {
            try
            {
                var totalQuery = "SELECT COUNT(*) FROM members WHERE role = 'Member'";
                TotalMembers = Convert.ToInt32(
                    await _databaseService.ExecuteScalarAsync(totalQuery));

                var activeQuery = @"
                    SELECT COUNT(*) FROM members m
                    JOIN member_accounts a ON m.member_id = a.member_id
                    WHERE a.status = 'Active'";
                ActiveMembers = Convert.ToInt32(
                    await _databaseService.ExecuteScalarAsync(activeQuery));

                var suspendedQuery = @"
                    SELECT COUNT(*) FROM members m
                    JOIN member_accounts a ON m.member_id = a.member_id
                    WHERE a.status = 'Suspended'";
                SuspendedMembers = Convert.ToInt32(
                    await _databaseService.ExecuteScalarAsync(suspendedQuery));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Stats error: {ex.Message}");
            }
        }

        private async Task EditMemberAsync(Member member)
        {
            if (member == null) return;
            var parameters = new Dictionary<string, object> { ["member"] = member };
            await Shell.Current.GoToAsync("EditMember", parameters);
        }

        private async Task ViewLoansAsync(Member member)
        {
            if (member == null) return;
            var parameters = new Dictionary<string, object>
            {
                ["memberId"] = member.MemberId,
                ["memberName"] = member.FullName
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
                        { "@status",   newStatus       },
                        { "@memberId", member.MemberId }
                    };

                    await _databaseService.ExecuteNonQueryAsync(query, parameters);
                    await LoadMembersAsync();
                }
                catch (Exception ex)
                {
                    await Application.Current.MainPage.DisplayAlert(
                        "Error", $"Failed to update status: {ex.Message}", "OK");
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
            catch { return "Active"; }
        }

        private async Task LoadMetaForMemberAsync(Member member)
        {
            try
            {
                var query = @"
            SELECT meta_id, member_id, meta_key, meta_value, created_at 
            FROM member_metadata 
            WHERE member_id = @memberId
            ORDER BY created_at DESC";

                var parameters = new Dictionary<string, object>
        {
            { "@memberId", member.MemberId }
        };

                var rows = await _databaseService.QueryAsync<MemberMetadata>(query, parameters);

                // ✅ Add these debug lines
                System.Diagnostics.Debug.WriteLine($"=== LoadMeta for MemberId: {member.MemberId}");
                System.Diagnostics.Debug.WriteLine($"=== Rows returned: {rows?.Count ?? 0}");

                member.MetaRows.Clear();

                if (rows != null)
                {
                    foreach (var row in rows)
                    {
                        System.Diagnostics.Debug.WriteLine($"=== Row: {row.MetaKey} = {row.MetaValue}");
                        member.MetaRows.Add(row);
                    }
                }

                System.Diagnostics.Debug.WriteLine($"=== MetaRows.Count after load: {member.MetaRows.Count}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"=== Meta load ERROR: {ex.Message}");
            }
        }

        private async Task ApplyFilterAsync() => await LoadMembersAsync();
    }
}