using Library.ViewModels;

namespace Library.Views;

public partial class ManageMembersPage : ContentPage
{
    private readonly ManageMembersViewModel _viewModel;

    public ManageMembersPage(ManageMembersViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadMembersAsync();
    }
}