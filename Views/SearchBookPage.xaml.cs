using Library.ViewModels;

namespace Library.Views;

public partial class SearchBookPage : ContentPage
{
    private readonly SearchBookViewModel _viewModel;

    public SearchBookPage(SearchBookViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // Prevent reloading every time page appears
        if (_viewModel.Titles.Count == 0)
        {
            try
            {
                await _viewModel.LoadTitlesAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Search page load error: {ex.Message}");
            }
        }
    }
}
