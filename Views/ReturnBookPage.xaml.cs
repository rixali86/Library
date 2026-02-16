using Library.ViewModels;
using Microsoft.Maui.Controls;
using System.Threading.Tasks;

namespace Library.Views
{
    public partial class ReturnBookPage : ContentPage
    {
        public ReturnBookViewModel ViewModel =>
            BindingContext as ReturnBookViewModel;

        // ✅ Only ONE constructor
        public ReturnBookPage(ReturnBookViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = viewModel;
        }

        // ✅ Only ONE OnAppearing
        protected override async void OnAppearing()
        {
            base.OnAppearing();

            if (ViewModel != null)
            {
                await ViewModel.LoadMembersAsync();
            }
        }
    }
}
