using Microsoft.Extensions.Logging;
using Library.Services;
using Library.ViewModels;
using Library.Views;

namespace Library
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

#if DEBUG
            builder.Logging.AddDebug();
#endif

            // Register Services
            builder.Services.AddSingleton<IDatabaseService, DatabaseService>();
            builder.Services.AddSingleton<IAuthService, AuthService>();
            builder.Services.AddSingleton<ILibraryService, LibraryService>();

            // Register ViewModels
            builder.Services.AddTransient<LoginViewModel>();
            builder.Services.AddTransient<RegisterViewModel>();
            builder.Services.AddTransient<DashboardViewModel>();
            builder.Services.AddTransient<SearchBookViewModel>();
            builder.Services.AddTransient<SettingsViewModel>();
            builder.Services.AddTransient<MemberProfileViewModel>();
            builder.Services.AddTransient<BranchDetailsViewModel>();
            builder.Services.AddTransient<EventsAttendedViewModel>();


            // Register Views
            builder.Services.AddTransient<LoginPage>();
            builder.Services.AddTransient<RegisterPage>();
            builder.Services.AddTransient<DashboardPage>();
            builder.Services.AddTransient<SettingsPage>();
            builder.Services.AddTransient<MemberProfilePage>();
            builder.Services.AddTransient<BranchDetailsPage>();
            builder.Services.AddTransient<BranchesPage>();
            builder.Services.AddTransient<EventsAttendedPage>();


            // Add other pages as needed:
            // Add other pages as needed:
            builder.Services.AddTransient<SearchBookPage>();
            // builder.Services.AddTransient<SearchMemberPage>();

            // Register Shells
            builder.Services.AddSingleton<AuthShell>();
            builder.Services.AddSingleton<AppShell>();

            return builder.Build();
        }
    }
}