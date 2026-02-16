using Library.ViewModels;
using Microsoft.Maui.Controls;

namespace Library.Views
{
    public partial class MemberLoansPage : ContentPage
    {
        private readonly MemberLoansViewModel _viewModel;

        public MemberLoansPage(MemberLoansViewModel viewModel)
        {
            try
            {
                InitializeComponent();
                _viewModel = viewModel;
                BindingContext = _viewModel;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in MemberLoansPage constructor: {ex.Message}");
            }
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            try
            {
                _viewModel?.LoadLoansCommand.Execute(null);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in OnAppearing: {ex.Message}");
            }
        }
    }
}