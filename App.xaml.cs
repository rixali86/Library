using Library.Services;
using System;
using Microsoft.Maui.Controls;
using Microsoft.Extensions.DependencyInjection;
namespace Library;

public partial class App : Application
{
    private readonly IServiceProvider _serviceProvider;

    public App(IServiceProvider serviceProvider)
    {
        InitializeComponent();
        _serviceProvider = serviceProvider;

        // Load session and determine which shell to show
        InitializeApp();
    }

    // Expose the IServiceProvider so pages can resolve services when needed.
    // Prefer constructor injection for pages/viewmodels where possible.
    public IServiceProvider Services => _serviceProvider;

    private async void InitializeApp()
    {
        // Load session from secure storage
        await UserSession.Instance.LoadSessionAsync();

#if DEBUG
        // Development helper: always show login page on startup so dashboard doesn't open automatically.
        var authShellDev = _serviceProvider.GetRequiredService<AuthShell>();
        MainPage = authShellDev;
        return;
#endif

        // Production behavior: show main shell if authenticated, otherwise show auth shell.
        if (UserSession.Instance.IsAuthenticated)
        {
            // User is logged in - show MainShell
            var mainShell = _serviceProvider.GetRequiredService<AppShell>();
            MainPage = mainShell;
        }
        else
        {
            // User is not logged in - show AuthShell
            var authShell = _serviceProvider.GetRequiredService<AuthShell>();
            MainPage = authShell;
        }
    }

    // Method to switch to MainShell after successful login
    public void SwitchToMainShell()
    {
        var mainShell = _serviceProvider.GetRequiredService<AppShell>();
        MainPage = mainShell;
    }

    // Method to switch back to AuthShell after logout
    public void SwitchToAuthShell()
    {
        var authShell = _serviceProvider.GetRequiredService<AuthShell>();
        MainPage = authShell;
    }
}