using Library.ViewModels;

namespace Library.Views;

public partial class LibrarySettingsPage : ContentPage
{
    private readonly LibrarySettingsViewModel _viewModel;

    public LibrarySettingsPage(LibrarySettingsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadSettingsAsync();
    }
}