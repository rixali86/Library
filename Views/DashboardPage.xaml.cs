using Library.ViewModels;

namespace Library.Views;

    public partial class DashboardPage : ContentPage
    {
        public DashboardPage(DashboardViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = viewModel;
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();

            // Load data when page appears
            if (BindingContext is DashboardViewModel viewModel)
            {
                viewModel.LoadDataCommand?.Execute(null);
            }
        }
    }


