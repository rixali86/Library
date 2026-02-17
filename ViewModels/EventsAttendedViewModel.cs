using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Library.Models;
using Library.Services;
using Microsoft.Maui.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Library.ViewModels
{
    public enum EventFilterType
    {
        Registered = 0,   // upcoming registrations
        Attended = 1,   // attended
        Missed = 2,   // registered but did not attend (past)
        PastAll = 3    // all past events
    }

    public partial class EventsAttendedViewModel : ObservableObject
    {
        private readonly IAuthService _authService;
        private readonly IDatabaseService _databaseService;

        public EventsAttendedViewModel(IAuthService authService, IDatabaseService databaseService)
        {
            _authService = authService;
            _databaseService = databaseService;

            LoadDataCommand = new AsyncRelayCommand(LoadDataAsync);
            ViewEventDetailsCommand = new AsyncRelayCommand<AttendedEventItem>(ViewEventDetailsAsync);
            ViewBranchCommand = new AsyncRelayCommand<AttendedEventItem>(ViewBranchAsync);
            SearchCommand = new RelayCommand(PerformSearch);

            // Set backing field directly to avoid triggering LoadData before commands are ready
            selectedFilter = EventFilterType.Attended;

            LoadDataCommand.Execute(null);
        }

        public IAsyncRelayCommand LoadDataCommand { get; }
        public IAsyncRelayCommand<AttendedEventItem> ViewEventDetailsCommand { get; }
        public IAsyncRelayCommand<AttendedEventItem> ViewBranchCommand { get; }
        public IRelayCommand SearchCommand { get; }

        // ── Loading ──────────────────────────────────────────────────────────
        private bool isLoading;
        public bool IsLoading
        {
            get => isLoading;
            set => SetProperty(ref isLoading, value);
        }

        // ── Filter ───────────────────────────────────────────────────────────
        private EventFilterType selectedFilter;

        /// <summary>Enum-typed filter — kept for any code-behind logic.</summary>
        public EventFilterType SelectedFilter
        {
            get => selectedFilter;
            set
            {
                if (SetProperty(ref selectedFilter, value))
                {
                    OnPropertyChanged(nameof(SelectedFilterIndex));
                    LoadDataCommand.Execute(null);
                }
            }
        }

        /// <summary>
        /// Int bridge used by the XAML Picker's SelectedIndex binding.
        /// MAUI cannot implicitly convert int ↔ enum, so this property
        /// handles the cast explicitly and keeps both sides in sync.
        /// </summary>
        public int SelectedFilterIndex
        {
            get => (int)selectedFilter;
            set
            {
                if ((int)selectedFilter != value)
                {
                    selectedFilter = (EventFilterType)value;
                    OnPropertyChanged(nameof(SelectedFilterIndex));
                    OnPropertyChanged(nameof(SelectedFilter));
                    LoadDataCommand.Execute(null);
                }
            }
        }

        // ── Events ───────────────────────────────────────────────────────────
        private List<AttendedEventItem> allEvents = new();
        public List<AttendedEventItem> AllEvents
        {
            get => allEvents;
            set
            {
                SetProperty(ref allEvents, value);
                PerformSearch();
            }
        }

        private List<AttendedEventItem> filteredEvents = new();
        public List<AttendedEventItem> FilteredEvents
        {
            get => filteredEvents;
            set => SetProperty(ref filteredEvents, value);
        }

        // ── Search ───────────────────────────────────────────────────────────
        private string searchText = string.Empty;
        public string SearchText
        {
            get => searchText;
            set
            {
                if (SetProperty(ref searchText, value))
                    PerformSearch();
            }
        }

        private void PerformSearch()
        {
            if (string.IsNullOrWhiteSpace(SearchText))
            {
                FilteredEvents = new List<AttendedEventItem>(AllEvents);
                return;
            }

            var searchLower = SearchText.ToLower();

            FilteredEvents = AllEvents
                .Where(e =>
                    e.Title.ToLower().Contains(searchLower) ||
                    e.BranchName.ToLower().Contains(searchLower) ||
                    (e.Description != null && e.Description.ToLower().Contains(searchLower))
                )
                .ToList();
        }

        // ── Data loading ─────────────────────────────────────────────────────
        private async Task LoadDataAsync()
        {
            IsLoading = true;

            try
            {
                var email = _authService?.CurrentUserEmail;

                if (string.IsNullOrWhiteSpace(email))
                {
                    AllEvents = new List<AttendedEventItem>();
                    return;
                }

                var memberQuery = "SELECT member_id FROM members WHERE email = @email";
                var member = await _databaseService.QuerySingleAsync<Member>(
                    memberQuery,
                    new Dictionary<string, object> { { "@email", email } });

                if (member == null)
                {
                    AllEvents = new List<AttendedEventItem>();
                    return;
                }

                string query;

                switch (SelectedFilter)
                {
                    case EventFilterType.Registered:
                        query = @"
                            SELECT e.*, b.branch_name AS BranchName,
                                   er.registration_datetime AS RegistrationDatetime,
                                   er.attendance_status AS AttendanceStatus
                            FROM branch_events e
                            JOIN branches b ON e.branch_id = b.branch_id
                            JOIN event_registrations er ON e.event_id = er.event_id
                            WHERE er.member_id = @memberId
                              AND e.start_datetime > @currentDateTime
                            ORDER BY e.start_datetime ASC";
                        break;

                    case EventFilterType.Attended:
                        query = @"
                            SELECT e.*, b.branch_name AS BranchName,
                                   er.registration_datetime AS RegistrationDatetime,
                                   er.attendance_status AS AttendanceStatus
                            FROM branch_events e
                            JOIN branches b ON e.branch_id = b.branch_id
                            JOIN event_registrations er ON e.event_id = er.event_id
                            WHERE er.member_id = @memberId
                              AND er.attendance_status = 'Attended'
                            ORDER BY e.start_datetime DESC";
                        break;

                    default: // PastAll
                        query = @"
                            SELECT e.*, b.branch_name AS BranchName,
                                   er.registration_datetime AS RegistrationDatetime,
                                   er.attendance_status AS AttendanceStatus
                            FROM branch_events e
                            JOIN branches b ON e.branch_id = b.branch_id
                            LEFT JOIN event_registrations er
                                 ON e.event_id = er.event_id
                                 AND er.member_id = @memberId
                            WHERE e.end_datetime < @currentDateTime
                            ORDER BY e.start_datetime DESC";
                        break;

                    case EventFilterType.Missed:
                        query = @"
        SELECT e.*, b.branch_name AS BranchName,
               er.registration_datetime AS RegistrationDatetime,
               er.attendance_status AS AttendanceStatus
        FROM branch_events e
        JOIN branches b ON e.branch_id = b.branch_id
        JOIN event_registrations er ON e.event_id = er.event_id
        WHERE er.member_id = @memberId
          AND e.start_datetime <= @currentDateTime
          AND (er.attendance_status IS NULL 
               OR er.attendance_status != 'Attended')
        ORDER BY e.start_datetime DESC";
                        break;
                }

                var parameters = new Dictionary<string, object>
                {
                    { "@memberId", member.MemberId },
                    { "@currentDateTime", DateTime.Now }
                };

                var list = await _databaseService.QueryAsync<AttendedEventItem>(query, parameters);
                AllEvents = list ?? new List<AttendedEventItem>();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                AllEvents = new List<AttendedEventItem>();
            }
            finally
            {
                IsLoading = false;
            }
        }

        // ── Navigation helpers ────────────────────────────────────────────────
        private async Task ViewEventDetailsAsync(AttendedEventItem item)
        {
            if (item == null) return;

            await Shell.Current.DisplayAlert(
                "Event Details",
                $"Title: {item.Title}\nBranch: {item.BranchName}\nDate: {item.StartDatetime:MMM d, yyyy}",
                "OK");
        }

        private async Task ViewBranchAsync(AttendedEventItem item)
        {
            if (item == null) return;

            await Shell.Current.DisplayAlert(
                "Branch Info",
                $"Branch: {item.BranchName}",
                "OK");
        }
    }

    // ── Model ─────────────────────────────────────────────────────────────────
    public class AttendedEventItem
    {
        public int EventId { get; set; }
        public int BranchId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime StartDatetime { get; set; }
        public DateTime EndDatetime { get; set; }
        public string BranchName { get; set; } = string.Empty;
        public DateTime? RegistrationDatetime { get; set; }
        public string AttendanceStatus { get; set; } = string.Empty;
    }
}