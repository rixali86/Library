using Library.ViewModels;
using Microsoft.Maui.Controls;

namespace Library.Views
{
    public partial class OverdueBooksPage : ContentPage
    {
        private OverdueBooksViewModel vm;

        public OverdueBooksPage(OverdueBooksViewModel viewModel)
        {
            try
            {
                InitializeComponent();
                vm = viewModel;
                BindingContext = vm;
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error: {ex.Message}");
            }
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            if (vm != null)
            {
                await vm.LoadOverdueLoansAsync();
            }
        }
    }
}