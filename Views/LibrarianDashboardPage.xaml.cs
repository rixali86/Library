using Library.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace Library.Views;

public partial class LibrarianDashboardPage : ContentPage
{
    public LibrarianDashboardPage(LibrarianDashboardViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        if (BindingContext is LibrarianDashboardViewModel viewModel)
        {
            viewModel.RefreshCommand?.Execute(null);
        }
    }
}