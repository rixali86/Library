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
    public partial class EventsAttendedViewModel : ObservableObject
    {
        private readonly IAuthService _authService;
        private readonly ILibraryService _libraryService;
        private readonly IDatabaseService _databaseService;

        public EventsAttendedViewModel(IAuthService authService, ILibraryService libraryService, IDatabaseService databaseService)
        {
            _authService = authService;
            _libraryService = libraryService;
            _databaseService = databaseService;

            LoadDataCommand = new AsyncRelayCommand(LoadDataAsync);
            ViewEventDetailsCommand = new AsyncRelayCommand<AttendedEventItem>(ViewEventDetailsAsync);
            ViewBranchCommand = new AsyncRelayCommand<AttendedEventItem>(ViewBranchAsync);
            SearchCommand = new RelayCommand(PerformSearch);

            // Start loading immediately
            LoadDataCommand.Execute(null);
        }

        public IAsyncRelayCommand LoadDataCommand { get; }
        public IAsyncRelayCommand<AttendedEventItem> ViewEventDetailsCommand { get; }
        public IAsyncRelayCommand<AttendedEventItem> ViewBranchCommand { get; }
        public IRelayCommand SearchCommand { get; }

        private bool isLoading;
        public bool IsLoading
        {
            get => isLoading;
            set => SetProperty(ref isLoading, value);
        }

        // ✅ Original list (database se aaye hue sab events)
        private List<AttendedEventItem> attendedEvents = new();
        public List<AttendedEventItem> AttendedEvents
        {
            get => attendedEvents;
            set
            {
                SetProperty(ref attendedEvents, value);
                PerformSearch(); // Jab bhi data load ho, filter apply karo
            }
        }

        // ✅ Filtered list (jo UI mein dikhega)
        private List<AttendedEventItem> filteredEvents = new();
        public List<AttendedEventItem> FilteredEvents
        {
            get => filteredEvents;
            set => SetProperty(ref filteredEvents, value);
        }

        // ✅ Search text property
        private string searchText = string.Empty;
        public string SearchText
        {
            get => searchText;
            set
            {
                if (SetProperty(ref searchText, value))
                {
                    PerformSearch(); // Jab bhi text change ho, search karo
                }
            }
        }

        // ✅ Search logic
        private void PerformSearch()
        {
            if (string.IsNullOrWhiteSpace(SearchText))
            {
                // Agar search text empty hai toh sab events dikhaao
                FilteredEvents = new List<AttendedEventItem>(AttendedEvents);
            }
            else
            {
                // Filter by title ya branch name
                var searchLower = SearchText.ToLower();
                FilteredEvents = AttendedEvents
                    .Where(e =>
                        e.Title.ToLower().Contains(searchLower) ||
                        e.BranchName.ToLower().Contains(searchLower) ||
                        (e.Description != null && e.Description.ToLower().Contains(searchLower))
                    )
                    .ToList();
            }
        }

        private async Task LoadDataAsync()
        {
            IsLoading = true;
            try
            {
                var email = _authService?.CurrentUserEmail;
                if (string.IsNullOrWhiteSpace(email))
                {
                    AttendedEvents = new List<AttendedEventItem>();
                    return;
                }

                var memberQuery = "SELECT member_id, first_name FROM members WHERE email = @email";
                var memberParams = new Dictionary<string, object> { { "@email", email } };
                var member = await _databaseService.QuerySingleAsync<Member>(memberQuery, memberParams);

                if (member == null)
                {
                    AttendedEvents = new List<AttendedEventItem>();
                    return;
                }

                // Updated query - Saari past events
                var query = @"
                    SELECT
                        e.event_id       AS EventId,
                        e.branch_id      AS BranchId,
                        e.title          AS Title,
                        e.description    AS Description,
                        e.start_datetime AS StartDatetime,
                        e.end_datetime   AS EndDatetime,
                        e.max_capacity   AS MaxCapacity,
                        e.created_at     AS CreatedAt,
                        b.branch_name    AS BranchName,
                        er.registration_datetime AS RegistrationDatetime,
                        er.attendance_status     AS AttendanceStatus
                    FROM branch_events e
                    JOIN branches b ON e.branch_id = b.branch_id
                    LEFT JOIN event_registrations er ON e.event_id = er.event_id 
                        AND er.member_id = @memberId
                    WHERE e.end_datetime < @currentDateTime
                    ORDER BY e.start_datetime DESC";

                var parameters = new Dictionary<string, object>
                {
                    { "@memberId", member.MemberId },
                    { "@currentDateTime", DateTime.Now }
                };

                var list = await _databaseService.QueryAsync<AttendedEventItem>(query, parameters);
                AttendedEvents = list ?? new List<AttendedEventItem>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Load past events error: {ex.Message}");
                AttendedEvents = new List<AttendedEventItem>();
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task ViewEventDetailsAsync(AttendedEventItem item)
        {
            if (item == null) return;

            await Shell.Current.DisplayAlert(
                "Event Details",
                $"Title: {item.Title}\nBranch: {item.BranchName}\nDate: {item.StartDatetime:MMM d, yyyy}",
                "OK"
            );
        }

        private async Task ViewBranchAsync(AttendedEventItem item)
        {
            if (item == null) return;

            await Shell.Current.DisplayAlert(
                "Branch Info",
                $"Branch: {item.BranchName}",
                "OK"
            );
        }
    }

    public class AttendedEventItem
    {
        public int EventId { get; set; }
        public int BranchId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime StartDatetime { get; set; }
        public DateTime EndDatetime { get; set; }
        public int MaxCapacity { get; set; }
        public DateTime CreatedAt { get; set; }
        public string BranchName { get; set; } = string.Empty;
        public DateTime RegistrationDatetime { get; set; }
        public string AttendanceStatus { get; set; } = string.Empty;
    }
}