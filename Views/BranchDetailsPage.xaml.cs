using Library.Models;
using Library.ViewModels;
using Microsoft.Maui.Controls;

namespace Library.Views

{
    public partial class BranchDetailsPage : ContentPage
    {
        public BranchDetailsPage(Branch branch)
        {
            InitializeComponent();
            BindingContext = new BranchDetailsViewModel(branch);
        }
    }
}