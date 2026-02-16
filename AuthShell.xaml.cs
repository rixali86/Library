namespace Library;

public partial class AuthShell : Shell
{
    public AuthShell()
    {
        InitializeComponent();

        // Register routes for navigation between auth pages
        Routing.RegisterRoute("Login", typeof(Views.LoginPage));
        Routing.RegisterRoute("Register", typeof(Views.RegisterPage));
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        // Ensure we're on the login page when auth shell appears
        Current.GoToAsync("//Login");
    }
}