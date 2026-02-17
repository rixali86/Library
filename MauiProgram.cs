using Library.Helpers;
using Library.Services;
using Library.ViewModels;
using Library.Views;
using Microsoft.Extensions.Logging;

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


            //helpers 


            builder.Services.AddSingleton<IValueConverter, BoolToObjectConverter>();
            builder.Services.AddSingleton<IValueConverter, DueDateColorConverter>();
            builder.Services.AddSingleton<Behavior<VisualElement>, EventToCommandBehavior>();
            builder.Services.AddSingleton<IValueConverter, IntToBoolConverter>();
            builder.Services.AddSingleton<IValueConverter, InverseBoolConverter>();
            builder.Services.AddSingleton<IValueConverter, NotEmptyConverter>();




            // Register Services
            builder.Services.AddSingleton<IDatabaseService, DatabaseService>();
            builder.Services.AddSingleton<IAuthService, AuthService>();
            builder.Services.AddSingleton<ILibraryService, LibraryService>();

            // Register ViewModels - Member ViewModels
            builder.Services.AddTransient<LoginViewModel>();
            builder.Services.AddTransient<RegisterViewModel>();
            builder.Services.AddTransient<DashboardViewModel>();
            builder.Services.AddTransient<SearchBookViewModel>();
            builder.Services.AddTransient<SettingsViewModel>();
            builder.Services.AddTransient<MemberProfileViewModel>();
            builder.Services.AddTransient<BranchDetailsViewModel>();
            builder.Services.AddTransient<EventsAttendedViewModel>();






            // Register ViewModels - Librarian ViewModels
            builder.Services.AddTransient<LibrarianDashboardViewModel>();
            builder.Services.AddTransient<ManageBooksViewModel>();
            builder.Services.AddTransient<AddEditBookViewModel>();
            builder.Services.AddTransient<IssueBookViewModel>();
            builder.Services.AddTransient<ManageMembersViewModel>();
            builder.Services.AddTransient<FinesViewModel>();
            builder.Services.AddTransient<LibrarySettingsViewModel>();
            builder.Services.AddTransient<MemberLoansViewModel>();
            builder.Services.AddTransient<MemberProfileViewModel>();
            builder.Services.AddTransient<OverdueBooksViewModel>();
            builder.Services.AddTransient<ReturnBookViewModel>();

            builder.Services.AddTransient<ReportsViewModel>();
            builder.Services.AddTransient<BookRequestsViewModel>();

            // Register Views - Member Views
            builder.Services.AddTransient<LoginPage>();
            builder.Services.AddTransient<RegisterPage>();
            builder.Services.AddTransient<DashboardPage>();
            builder.Services.AddTransient<SettingsPage>();
            builder.Services.AddTransient<MemberProfilePage>();
            builder.Services.AddTransient<BranchDetailsPage>();
            builder.Services.AddTransient<BranchesPage>();
            builder.Services.AddTransient<EventsAttendedPage>();
            builder.Services.AddTransient<SearchBookPage>();


            // Register Views - Librarian Views
            builder.Services.AddTransient<LibrarianDashboardPage>();
            builder.Services.AddTransient<ManageBooksPage>();
            builder.Services.AddTransient<AddEditBookViewModel>();
            builder.Services.AddTransient<IssueBookPage>();
            builder.Services.AddTransient<ManageMembersPage>();
            builder.Services.AddTransient<ManageMembersPage>();
            builder.Services.AddTransient<FinesPage>();
            builder.Services.AddTransient<LibrarySettingsPage>();
            builder.Services.AddTransient<MemberLoansPage>();
            builder.Services.AddTransient<MemberProfilePage>();
            builder.Services.AddTransient<OverdueBooksPage>();
            builder.Services.AddTransient<ReturnBookPage>();

            builder.Services.AddTransient<ReportsPage>();
            builder.Services.AddTransient<BookRequestsPage>();

            // Register Shells
            builder.Services.AddSingleton<AuthShell>();
            builder.Services.AddSingleton<AppShell>();
            builder.Services.AddSingleton<LibrarianShell>();

            return builder.Build();
        }
    }
}