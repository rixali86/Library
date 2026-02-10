using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Storage;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using Microsoft.Maui.ApplicationModel;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace Library.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    // Persistent keys
    private const string PrefThemeKey = "app_theme";
    private const string PrefNotificationsKey = "notifications_enabled";
    private const string PrefLanguageKey = "app_language";

    public ObservableCollection<string> Themes { get; } = new()
    {
        "System", "Light", "Dark"
    };

    public ObservableCollection<string> Languages { get; } = new()
    {
        "English", "Spanish", "French"
    };

    [ObservableProperty]
    private string selectedTheme = "System";

    [ObservableProperty]
    private bool notificationsEnabled;

    [ObservableProperty]
    private string selectedLanguage = "English";

    [ObservableProperty]
    private string statusMessage = string.Empty;

    public SettingsViewModel()
    {
    }

    public async Task InitializeAsync()
    {
        // Load persisted settings
        SelectedTheme = Preferences.Get(PrefThemeKey, "System");
        NotificationsEnabled = Preferences.Get(PrefNotificationsKey, true);
        SelectedLanguage = Preferences.Get(PrefLanguageKey, "English");

        // Apply theme immediately
        ApplyTheme(SelectedTheme);

        await Task.CompletedTask;
    }

    partial void OnSelectedThemeChanged(string value)
    {
        ApplyTheme(value);
        Preferences.Set(PrefThemeKey, value);
        StatusMessage = $"Theme saved: {value}";
    }

    partial void OnNotificationsEnabledChanged(bool value)
    {
        Preferences.Set(PrefNotificationsKey, value);
        StatusMessage = value ? "Notifications enabled" : "Notifications disabled";
    }

    partial void OnSelectedLanguageChanged(string value)
    {
        Preferences.Set(PrefLanguageKey, value);
        StatusMessage = $"Language saved: {value}. Restart may be required.";
    }

    private void ApplyTheme(string theme)
    {
        if (Application.Current == null)
            return;

        switch (theme)
        {
            case "Light":
                Application.Current.UserAppTheme = AppTheme.Light;
                break;
            case "Dark":
                Application.Current.UserAppTheme = AppTheme.Dark;
                break;
            default:
                Application.Current.UserAppTheme = AppTheme.Unspecified; // follows system
                break;
        }
    }

    [RelayCommand]
    public async Task ReportProblemAsync()
    {
        try
        {
            var subject = Uri.EscapeDataString("Report: Problem in Library App");
            var body = Uri.EscapeDataString("Describe the problem here:\n\nDevice info:\n");
            var mailto = $"mailto:support@library.example.com?subject={subject}&body={body}";
            await Launcher.OpenAsync(mailto);
        }
        catch
        {
            await Application.Current.MainPage.DisplayAlert("Error", "Unable to open email client.", "OK");
        }
    }

    [RelayCommand]
    public async Task OpenTermsAsync()
    {
        var url = "https://www.example.com/terms-and-privacy";
        try
        {
            await Launcher.OpenAsync(url);
        }
        catch
        {
            await Application.Current.MainPage.DisplayAlert("Error", "Unable to open link.", "OK");
        }
    }

    [RelayCommand]
    public async Task PartnerWithUsAsync()
    {
        try
        {
            var mailto = "mailto:partners@library.example.com?subject=Partnership%20Inquiry";
            await Launcher.OpenAsync(mailto);
        }
        catch
        {
            await Application.Current.MainPage.DisplayAlert("Error", "Unable to open email client.", "OK");
        }
    }

    [RelayCommand]
    public async Task CheckForUpdatesAsync()
    {
        // Placeholder: integrate with your update mechanism/store APIs
        await Task.Delay(500);
        await Application.Current.MainPage.DisplayAlert("Updates", "You are running the latest version.", "OK");
    }

    [RelayCommand]
    public async Task ConnectWithUsAsync()
    {
        // Open a social links page or external links
        var url = "https://www.example.com/connect";
        try
        {
            await Launcher.OpenAsync(url);
        }
        catch
        {
            await Application.Current.MainPage.DisplayAlert("Error", "Unable to open link.", "OK");
        }
    }

    [RelayCommand]
    public async Task InviteFriendsAsync()
    {
        try
        {
            await Share.RequestAsync(new ShareTextRequest
            {
                Text = "Check out the Library app: https://www.example.com/app",
                Title = "Invite a friend"
            });
        }
        catch
        {
            await Application.Current.MainPage.DisplayAlert("Error", "Unable to open share dialog.", "OK");
        }
    }
}