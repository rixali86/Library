using Library;
using Library.Services;
using Library.Views;
using System.Configuration;
using Library.Models;
using Microsoft.Maui.Controls;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Library;

public partial class AppShell : Shell
{
    private readonly IAuthService _authService;
    private readonly IDatabaseService _databaseService;

    // Bindable header properties (set once in constructor)
    public string UserName { get; private set; } = "Guest";
    public string UserEmail { get; private set; } = string.Empty;
    public string UserInitials => BuildInitials(UserName);

    public Command GoToSettingsCommand { get; }

    // Updated to accept IDatabaseService so AppShell can load the logged-in member details.
    public AppShell(IAuthService authService, IDatabaseService databaseService)
    {
        InitializeComponent();
        _authService = authService;
        _databaseService = databaseService;

        // Use the shell instance as its own BindingContext so header bindings work.
        BindingContext = this;

        // Start loading member details asynchronously (fire-and-forget).
        _ = LoadMemberAsync();

        GoToSettingsCommand = new Command(async () => await Shell.Current.GoToAsync("//Setting"));

        // Register routes for pages that may be navigated via GoToAsync with absolute routes
        Routing.RegisterRoute("Login", typeof(LoginPage));
        Routing.RegisterRoute("Profile", typeof(MemberProfilePage));
        Routing.RegisterRoute("EventDetails", typeof(EventsAttendedPage));
        Routing.RegisterRoute("BranchDetails", typeof(BranchDetailsPage));

    }

    // Load member details from DB using the logged-in email and update the flyout header.
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

            var parameters = new Dictionary<string, object> { { "@email", email } };

            var member = await _databaseService.QuerySingleAsync<Member>(sql, parameters);

            if (member != null)
            {
                // Prefer real name when available; fall back to email local-part.
                var displayName = (string.IsNullOrWhiteSpace(member.FirstName) && string.IsNullOrWhiteSpace(member.LastName))
                    ? (member.Email?.Split('@')[0] ?? "Guest")
                    : $"{member.FirstName} {member.LastName}".Trim();

                UserName = displayName;
                UserEmail = member.Email ?? string.Empty;

                // Notify bindings that properties changed (Shell inherits BindableObject).
                OnPropertyChanged(nameof(UserName));
                OnPropertyChanged(nameof(UserEmail));
                OnPropertyChanged(nameof(UserInitials));
            }
        }
        catch
        {
            // Silent fail - keep Guest display if anything goes wrong.
        }
    }

    private static string BuildInitials(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "?";

        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 1)
            return parts[0].Length >= 1 ? parts[0][0].ToString().ToUpper() : "?";

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
            Application.Current.MainPage = new AuthShell();
        }
    }
}