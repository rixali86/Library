using Library.Models;
using Library.ViewModels;

namespace Library.Views;

public partial class AddEditBookPage : ContentPage
{
    private readonly AddEditBookViewModel _viewModel;

    public AddEditBookPage(AddEditBookViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    public AddEditBookPage(AddEditBookViewModel viewModel, Title bookToEdit) : this(viewModel)
    {
        _viewModel.Book = bookToEdit;
        _viewModel.PageTitle = "Edit Book";
        _viewModel.SaveButtonText = "Update";
        _viewModel.LoadBookForEdit(bookToEdit);
    }
}