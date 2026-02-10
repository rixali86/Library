using Library.ViewModels;





namespace Library.Views;

public partial class MemberProfilePage : ContentPage
{
    private readonly MemberProfileViewModel _viewModel;

    public MemberProfilePage(MemberProfileViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (_viewModel == null) return;

        // Load member data (safe to call multiple times, viewmodel prevents redundant DB calls)
        await _viewModel.LoadMemberAsync();
    }
}