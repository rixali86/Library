using Library.Services;

namespace Library;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        // Add navigation guards
        Navigating += OnShellNavigating;
    }

    private async void OnShellNavigating(object sender, ShellNavigatingEventArgs e)
    {
        var current = e.Current?.Location?.OriginalString;
        var target = e.Target?.Location?.OriginalString;

        Console.WriteLine($"From: {current} → To: {target}");

        // List of protected routes
        var protectedRoutes = new[]
        {
            "Dashboard",
            "SearchBook",
            "SearchMember"
        };

        // Check if navigating TO a protected route
        foreach (var route in protectedRoutes)
        {
            if (target?.Contains(route) == true)
            {
                // Check if user is authenticated
                var authService = Handler?.MauiContext?.Services?.GetService<IAuthService>();

                if (authService == null || !authService.IsAuthenticated)
                {
                    // CANCEL navigation
                    e.Cancel();

                    // Redirect to login
                    await DisplayAlert("Access Denied", "Please login first", "OK");
                    await GoToAsync("//Login");

                    return;
                }
            }
        }

    }
}