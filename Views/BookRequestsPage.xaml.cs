using System;
using System.Collections.Generic;
using System.Text;
using Library.ViewModels;

namespace Library.Views
{
    public partial class BookRequestsPage:ContentPage
    { 
        private readonly BookRequestsViewModel _viewModel;

        public BookRequestsPage(BookRequestsViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            BindingContext = _viewModel;
        }
    }
}
