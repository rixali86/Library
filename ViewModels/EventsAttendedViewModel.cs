using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Library.Models;
using Library.Services; 
using Microsoft.Maui.Controls;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Library.ViewModels
{
    public class EventsAttendedViewModel : ObservableObject
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

            // Start loading immediately
            LoadDataCommand.Execute(null);
        }

        public IAsyncRelayCommand LoadDataCommand { get; }
        public IAsyncRelayCommand<AttendedEventItem> ViewEventDetailsCommand { get; }
        public IAsyncRelayCommand<AttendedEventItem> ViewBranchCommand { get; }

        private bool isLoading;
        public bool IsLoading
        {
            get => isLoading;
            set => SetProperty(ref isLoading, value);
        }

        private List<AttendedEventItem> attendedEvents = new();
        public List<AttendedEventItem> AttendedEvents
        {
            get => attendedEvents;
            set => SetProperty(ref attendedEvents, value);
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
                    FROM event_registrations er
                    JOIN branch_events e ON er.event_id = e.event_id
                    JOIN branches b ON e.branch_id = b.branch_id
                    WHERE er.member_id = @memberId AND er.attendance_status = 'Attended'
                    ORDER BY e.start_datetime DESC";

                var parameters = new Dictionary<string, object> { { "@memberId", member.MemberId } };

                var list = await _databaseService.QueryAsync<AttendedEventItem>(query, parameters);
                AttendedEvents = list ?? new List<AttendedEventItem>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Load attended events error: {ex.Message}");
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

            // Navigate to your event details page.
            // Ensure the route "EventDetails" (or "EventDetailsPage") is registered in AppShell with a query parameter "eventId".
            await Shell.Current.GoToAsync($"EventDetails?eventId={item.EventId}");
        }

        private async Task ViewBranchAsync(AttendedEventItem item)
        {
            if (item == null) return;

            // Navigate to branch details. Ensure the route is registered in AppShell and supports query param branchId.
            await Shell.Current.GoToAsync($"BranchDetails?branchId={item.BranchId}");
        }
    }

    // Lightweight DTO used for binding the attended events list
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

        // From branches table
        public string BranchName { get; set; } = string.Empty;

        // From registrations table
        public DateTime RegistrationDatetime { get; set; }
        public string AttendanceStatus { get; set; } = string.Empty;
    }
}
