using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Library.Models;
using Library.Services;
using System.Collections.ObjectModel;

namespace Library.ViewModels
{
    public partial class LibrarySettingsViewModel : ObservableObject
    {
        private readonly IDatabaseService _databaseService;

        [ObservableProperty]
        private LibrarySettings settings = new();

        [ObservableProperty]
        private ObservableCollection<Branch> branches = new();

        [ObservableProperty]
        private Branch selectedBranch;

        [ObservableProperty]
        private ObservableCollection<BranchHour> branchHours = new();

        [ObservableProperty]
        private bool isSaving = false;

        public IAsyncRelayCommand LoadSettingsCommand { get; }
        public IAsyncRelayCommand SaveSettingsCommand { get; }
        public IAsyncRelayCommand<Branch> LoadBranchHoursCommand { get; }
        public IAsyncRelayCommand UpdateHoursCommand { get; }

        public LibrarySettingsViewModel(IDatabaseService databaseService)
        {
            _databaseService = databaseService;

            LoadSettingsCommand = new AsyncRelayCommand(LoadSettingsAsync);
            SaveSettingsCommand = new AsyncRelayCommand(SaveSettingsAsync);
            LoadBranchHoursCommand = new AsyncRelayCommand<Branch>(LoadBranchHoursAsync);
            UpdateHoursCommand = new AsyncRelayCommand(UpdateHoursAsync);
        }

        public async Task LoadSettingsAsync()
        {
            try
            {
                // Load branches
                var branchesQuery = "SELECT * FROM branches ORDER BY branch_name";
                var branchesList = await _databaseService.QueryAsync<Branch>(branchesQuery);

                Branches.Clear();
                if (branchesList != null)
                {
                    foreach (var branch in branchesList)
                    {
                        Branches.Add(branch);
                    }
                }

                // Load settings from a settings table (you'd need to create this)
                // For now, we'll use default values
                Settings = new LibrarySettings
                {
                    DefaultLoanDays = 14,
                    MaxBooksPerMember = 5,
                    FinePerDay = 0.50m,
                    MembershipDuration = 365,
                    AllowGuestRegistration = true,
                    SendEmailReminders = true,
                    SendSmsReminders = false,
                    ReminderDaysBeforeDue = 3
                };

                // Try to load from database if settings table exists
                try
                {
                    var settingsQuery = "SELECT * FROM library_settings LIMIT 1";
                    var dbSettings = await _databaseService.QuerySingleAsync<LibrarySettings>(settingsQuery);
                    if (dbSettings != null)
                    {
                        Settings = dbSettings;
                    }
                }
                catch
                {
                    // Settings table doesn't exist yet
                }
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert("Error", $"Failed to load settings: {ex.Message}", "OK");
            }
        }

        private async Task LoadBranchHoursAsync(Branch branch)
        {
            if (branch == null) return;

            try
            {
                var query = "SELECT * FROM branch_hours WHERE branch_id = @branchId ORDER BY day_of_week";
                var parameters = new Dictionary<string, object> { { "@branchId", branch.BranchId } };

                var hours = await _databaseService.QueryAsync<BranchHour>(query, parameters);

                BranchHours.Clear();
                if (hours != null)
                {
                    foreach (var hour in hours)
                    {
                        BranchHours.Add(hour);
                    }
                }
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert("Error", $"Failed to load branch hours: {ex.Message}", "OK");
            }
        }

        private async Task UpdateHoursAsync()
        {
            if (SelectedBranch == null) return;

            try
            {
                // In a real app, you'd have a UI to edit hours
                await Application.Current.MainPage.DisplayAlert(
                    "Info",
                    "Hours editing would open a dedicated page here.",
                    "OK");
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert("Error", $"Failed to update hours: {ex.Message}", "OK");
            }
        }

        private async Task SaveSettingsAsync()
        {
            try
            {
                IsSaving = true;

                // Check if settings table exists
                try
                {
                    var checkQuery = "SELECT COUNT(*) FROM library_settings";
                    await _databaseService.ExecuteScalarAsync(checkQuery);
                }
                catch
                {
                    // Create settings table
                    var createTableQuery = @"
                        CREATE TABLE IF NOT EXISTS library_settings (
                            setting_id INT AUTO_INCREMENT PRIMARY KEY,
                            default_loan_days INT,
                            max_books_per_member INT,
                            fine_per_day DECIMAL(5,2),
                            membership_duration INT,
                            allow_guest_registration BOOLEAN,
                            send_email_reminders BOOLEAN,
                            send_sms_reminders BOOLEAN,
                            reminder_days_before_due INT,
                            updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP
                        )";
                    await _databaseService.ExecuteNonQueryAsync(createTableQuery);
                }

                // Upsert settings
                var query = @"
                    INSERT INTO library_settings 
                        (setting_id, default_loan_days, max_books_per_member, fine_per_day, 
                         membership_duration, allow_guest_registration, send_email_reminders, 
                         send_sms_reminders, reminder_days_before_due)
                    VALUES 
                        (1, @loanDays, @maxBooks, @finePerDay, @membershipDuration, 
                         @allowGuest, @emailReminders, @smsReminders, @reminderDays)
                    ON DUPLICATE KEY UPDATE
                        default_loan_days = @loanDays,
                        max_books_per_member = @maxBooks,
                        fine_per_day = @finePerDay,
                        membership_duration = @membershipDuration,
                        allow_guest_registration = @allowGuest,
                        send_email_reminders = @emailReminders,
                        send_sms_reminders = @smsReminders,
                        reminder_days_before_due = @reminderDays";

                var parameters = new Dictionary<string, object>
                {
                    { "@loanDays", Settings.DefaultLoanDays },
                    { "@maxBooks", Settings.MaxBooksPerMember },
                    { "@finePerDay", Settings.FinePerDay },
                    { "@membershipDuration", Settings.MembershipDuration },
                    { "@allowGuest", Settings.AllowGuestRegistration },
                    { "@emailReminders", Settings.SendEmailReminders },
                    { "@smsReminders", Settings.SendSmsReminders },
                    { "@reminderDays", Settings.ReminderDaysBeforeDue }
                };

                await _databaseService.ExecuteNonQueryAsync(query, parameters);

                await Application.Current.MainPage.DisplayAlert("Success", "Settings saved successfully", "OK");
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert("Error", $"Failed to save settings: {ex.Message}", "OK");
            }
            finally
            {
                IsSaving = false;
            }
        }
    }

    public class LibrarySettings
    {
        public int DefaultLoanDays { get; set; }
        public int MaxBooksPerMember { get; set; }
        public decimal FinePerDay { get; set; }
        public int MembershipDuration { get; set; }
        public bool AllowGuestRegistration { get; set; }
        public bool SendEmailReminders { get; set; }
        public bool SendSmsReminders { get; set; }
        public int ReminderDaysBeforeDue { get; set; }
    }

    public class BranchHour
    {
        public int HourId { get; set; }
        public int BranchId { get; set; }
        public string DayOfWeek { get; set; }
        public TimeSpan OpenTime { get; set; }
        public TimeSpan CloseTime { get; set; }
    }
}