using Library.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace Library.Views;

public partial class ManageBooksPage : ContentPage
{
    private readonly ManageBooksViewModel _viewModel;

    public ManageBooksPage(ManageBooksViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadBooksAsync();
    }
}