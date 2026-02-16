using Library.Services;
using Library.Models;
using Library.Views;
using Microsoft.Maui.Controls;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Library;

public partial class LibrarianShell : Shell
{
    private readonly IAuthService _authService;
    private readonly IDatabaseService _databaseService;

    // Bindable header properties
    public string UserName { get; private set; } = "Librarian";
    public string UserEmail { get; private set; } = string.Empty;
    public string UserInitials => BuildInitials(UserName);

    public LibrarianShell(IAuthService authService, IDatabaseService databaseService)
    {
        InitializeComponent();
        _authService = authService;
        _databaseService = databaseService;

        // Use the shell instance as its own BindingContext so header bindings work
        BindingContext = this;

        // Load librarian details
        _ = LoadLibrarianAsync();

        // Register all routes for librarian pages
        RegisterRoutes();
    }

    private void RegisterRoutes()
    {
        // Dashboard
        Routing.RegisterRoute("LibrarianDashboard", typeof(LibrarianDashboardPage));

        // Book Management
        Routing.RegisterRoute("ManageBooks", typeof(ManageBooksPage));
        Routing.RegisterRoute("AddBook", typeof(AddEditBookPage));
        Routing.RegisterRoute("EditBook", typeof(AddEditBookPage));

        // Member Management
        Routing.RegisterRoute("ManageMembers", typeof(ManageMembersPage));
        Routing.RegisterRoute("MemberLoans", typeof(MemberLoansPage));

        // Transactions
        Routing.RegisterRoute("IssueBook", typeof(IssueBookPage));
        Routing.RegisterRoute("ReturnBook", typeof(ReturnBookPage));
        Routing.RegisterRoute("OverdueBooks", typeof(OverdueBooksPage));

        // Reports and Fines
        Routing.RegisterRoute("Reports", typeof(ReportsPage));
        Routing.RegisterRoute("Fines", typeof(FinesPage));

        // Settings
        Routing.RegisterRoute("LibrarySettings", typeof(LibrarySettingsPage));
        Routing.RegisterRoute("Settings", typeof(SettingsPage));

        // Shared Pages
        Routing.RegisterRoute("Branches", typeof(BranchesPage));
        Routing.RegisterRoute("BranchDetails", typeof(BranchDetailsPage));
        Routing.RegisterRoute("Events", typeof(EventsAttendedPage));
    }

    private async Task LoadLibrarianAsync()
    {
        try
        {
            var email = _authService.CurrentUserEmail;
            if (string.IsNullOrWhiteSpace(email))
                return;

            var sql = @"
                SELECT member_id AS MemberId,
                       first_name AS FirstName,
                       last_name AS LastName,
                       email AS Email
                FROM members
                WHERE email = @email
                LIMIT 1;";

            var parameters = new Dictionary<string, object> { { "@email", email } };
            var member = await _databaseService.QuerySingleAsync<Member>(sql, parameters);

            if (member != null)
            {
                UserName = $"{member.FirstName} {member.LastName}".Trim();
                UserEmail = member.Email ?? string.Empty;

                OnPropertyChanged(nameof(UserName));
                OnPropertyChanged(nameof(UserEmail));
                OnPropertyChanged(nameof(UserInitials));
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading librarian: {ex.Message}");
        }
    }

    private static string BuildInitials(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "L";

        var parts = name.Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 1)
            return parts[0].Length >= 1 ? parts[0][0].ToString().ToUpper() : "L";

        return (parts[0][0].ToString() + parts[^1][0].ToString()).ToUpper();
    }

    private async void OnLogoutClicked(object sender, EventArgs e)
    {
        bool confirm = await DisplayAlert(
            "Logout",
            "Are you sure you want to logout?",
            "Yes",
            "No");

        if (confirm)
        {
            await _authService.LogoutAsync();
            await UserSession.Instance.EndSessionAsync();

            // Switch back to AuthShell
            ((App)Application.Current).SwitchToAuthShell();
        }
    }

    // Override OnNavigating to handle any pre-navigation logic
    protected override async void OnNavigating(ShellNavigatingEventArgs args)
    {
        base.OnNavigating(args);

        // You can add navigation validation here if needed
        var currentLocation = args.Current?.Location?.OriginalString;
        var targetLocation = args.Target?.Location?.OriginalString;

        System.Diagnostics.Debug.WriteLine($"Navigating from {currentLocation} to {targetLocation}");
    }
}