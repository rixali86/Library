using Library.Services;
using Library.ViewModels;

namespace Library.Views;

    public partial class LoginPage : ContentPage
    {
        public LoginPage(LoginViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = viewModel;
        }
 



    }
