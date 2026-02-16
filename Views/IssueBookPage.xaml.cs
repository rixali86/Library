using Library.ViewModels;
using Microsoft.Maui.Controls;

namespace Library.Views
{
    public partial class IssueBookPage : ContentPage
    {
        private IssueBookViewModel vm;

        public IssueBookPage(IssueBookViewModel viewModel)
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
    }
}