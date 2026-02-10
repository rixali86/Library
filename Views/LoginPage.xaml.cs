using Microsoft.Maui.Controls;
using Library.ViewModels;

namespace Library.Views;

public partial class LoginPage : ContentPage
{
    public LoginPage(LoginViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        if (BindingContext is LoginViewModel vm)
        {
            // Ensure previous credentials are cleared each time the page becomes visible
            vm.ClearCredentials();
        }
    }
}