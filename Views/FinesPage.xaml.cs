using Library.ViewModels;

namespace Library.Views;

public partial class FinesPage : ContentPage
{
    private readonly FinesViewModel _viewModel;

    public FinesPage(FinesViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadFinesAsync();
    }
}