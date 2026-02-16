using Library.Services;
using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace Library;

public partial class App : Application
{
    private readonly IServiceProvider _serviceProvider;

    public App(IServiceProvider serviceProvider)
    {
        InitializeComponent();

        _serviceProvider = serviceProvider;

        // Temporary page so app can load safely
        MainPage = new ContentPage
        {
            Content = new ActivityIndicator
            {
                IsRunning = true,
                VerticalOptions = LayoutOptions.Center,
                HorizontalOptions = LayoutOptions.Center
            }
        };

        // Run after UI is ready
        Dispatcher.Dispatch(async () =>
        {
            await InitializeAppAsync();
        });
    }

    public IServiceProvider Services => _serviceProvider;

    // Now returns Task (NOT void)
    private async Task InitializeAppAsync()
    {
        try
        {
            // Load session safely
            await UserSession.Instance.LoadSessionAsync();

#if DEBUG
            var devShell = _serviceProvider.GetRequiredService<AuthShell>();
            MainPage = devShell;
            return;
#endif

            if (UserSession.Instance.IsAuthenticated)
            {
                var mainShell = _serviceProvider.GetRequiredService<AppShell>();
                MainPage = mainShell;
            }
            else
            {
                var authShell = _serviceProvider.GetRequiredService<AuthShell>();
                MainPage = authShell;
            }
        }
        catch (Exception ex)
        {
            // If anything fails → show login
            System.Diagnostics.Debug.WriteLine(ex);

            var authShell = _serviceProvider.GetRequiredService<AuthShell>();
            MainPage = authShell;
        }
    }

    public void SwitchToMainShell()
    {
        MainPage = new AppShell(
            Handler.MauiContext.Services.GetService<IAuthService>(),
            Handler.MauiContext.Services.GetService<IDatabaseService>()
        );

        // Navigate based on role
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            var role = UserSession.Instance.UserRole;

            if (role == "Librarian")
            {
                await Shell.Current.GoToAsync("//LibrarianDashboard");
            }
            else
            {
                await Shell.Current.GoToAsync("//MemberDashboard");
            }
        });
    }


    public void SwitchToAuthShell()
    {
        var authShell = _serviceProvider.GetRequiredService<AuthShell>();
        MainPage = authShell;
    }
}
