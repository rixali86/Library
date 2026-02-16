using Library;
using Library.Services;
using Library.Views;
using Library.Models;
using Microsoft.Maui.Controls;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Library;

public partial class AppShell : Shell
{
    private readonly IAuthService _authService;
    private readonly IDatabaseService _databaseService;

    // ================= HEADER BINDINGS =================

    public string UserName { get; private set; } = "Guest";
    public string UserEmail { get; private set; } = string.Empty;
    public string UserRole { get; private set; } = "Member";

    public bool IsMember => UserRole == "Member";
    public bool IsLibrarian => UserRole == "Librarian";

    public string UserInitials => BuildInitials(UserName);

    public AppShell(IAuthService authService, IDatabaseService databaseService)
    {
        InitializeComponent();

        _authService = authService;
        _databaseService = databaseService;

        BindingContext = this;

        // Load role immediately from session
        UserRole = _authService.CurrentUserRole ?? "Member";

        OnPropertyChanged(nameof(UserRole));
        OnPropertyChanged(nameof(IsMember));
        OnPropertyChanged(nameof(IsLibrarian));

        _ = LoadMemberAsync();

        RegisterRoutes();
    }

    private void RegisterRoutes()
    {
        // Auth routes
        Routing.RegisterRoute("Login", typeof(LoginPage));
        Routing.RegisterRoute("Register", typeof(RegisterPage));

        // Member routes
        Routing.RegisterRoute("MemberDashboard", typeof(DashboardPage));
        Routing.RegisterRoute("Search", typeof(SearchBookPage));
        Routing.RegisterRoute("Branches", typeof(BranchesPage));
        Routing.RegisterRoute("BranchDetails", typeof(BranchDetailsPage));
        Routing.RegisterRoute("Events", typeof(EventsAttendedPage));
        Routing.RegisterRoute("Profile", typeof(MemberProfilePage));
        Routing.RegisterRoute("Settings", typeof(SettingsPage));

        // Librarian routes
        Routing.RegisterRoute("LibrarianDashboard", typeof(LibrarianDashboardPage));
        Routing.RegisterRoute("ManageBooks", typeof(ManageBooksPage));
        Routing.RegisterRoute("ManageMembers", typeof(ManageMembersPage));
        Routing.RegisterRoute("IssueBook", typeof(IssueBookPage));
        Routing.RegisterRoute("AddBook", typeof(AddEditBookPage));
        Routing.RegisterRoute("EditBook", typeof(AddEditBookPage));
        Routing.RegisterRoute("Fines", typeof(FinesPage));
        Routing.RegisterRoute("LibrarySettings", typeof(LibrarySettingsPage));
        Routing.RegisterRoute("MemberLoans", typeof(MemberLoansPage));
        Routing.RegisterRoute("MemberProfile", typeof(MemberProfilePage));
        Routing.RegisterRoute("OverdueBooks", typeof(OverdueBooksPage));
        Routing.RegisterRoute("ReturnBook", typeof(ReturnBookPage));
        Routing.RegisterRoute("Reports", typeof(ReportsPage));

    }

    private async Task LoadMemberAsync()
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

            var parameters = new Dictionary<string, object>
            {
                { "@email", email }
            };

            var member = await _databaseService.QuerySingleAsync<Member>(sql, parameters);

            if (member != null)
            {
                var displayName =
                    (string.IsNullOrWhiteSpace(member.FirstName) && string.IsNullOrWhiteSpace(member.LastName))
                    ? (member.Email?.Split('@')[0] ?? "Guest")
                    : $"{member.FirstName} {member.LastName}".Trim();

                UserName = displayName;
                UserEmail = member.Email ?? string.Empty;

                OnPropertyChanged(nameof(UserName));
                OnPropertyChanged(nameof(UserEmail));
                OnPropertyChanged(nameof(UserInitials));
            }
        }
        catch
        {
            // Silent fail
        }
    }

    private static string BuildInitials(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "?";

        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length == 1)
            return parts[0].Length >= 1
                ? parts[0][0].ToString().ToUpper()
                : "?";

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

            ((App)Application.Current).SwitchToAuthShell();
        }
    }
}
