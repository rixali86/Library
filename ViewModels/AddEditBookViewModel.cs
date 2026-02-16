 using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Library.Models;
using Library.Services;
using System.Collections.ObjectModel;

namespace Library.ViewModels
{
    public partial class AddEditBookViewModel : ObservableObject
    {
        private readonly IDatabaseService _databaseService;
        private readonly IAuthService _authService;

        [ObservableProperty]
        private Title book = new();

        [ObservableProperty]
        private ObservableCollection<Author> authors = new();

        [ObservableProperty]
        private ObservableCollection<BookCopy> copies = new();

        [ObservableProperty]
        private ObservableCollection<Branch> branches = new();

        [ObservableProperty]
        private Branch selectedBranch;

        [ObservableProperty]
        private string pageTitle = "Add New Book";

        [ObservableProperty]
        private string saveButtonText = "Save";

        [ObservableProperty]
        private bool isSaving = false;

        [ObservableProperty]
        private List<string> conditionOptions = new() { "New", "Good", "Fair", "Poor", "Damaged" };

        public IAsyncRelayCommand AddAuthorCommand { get; }
        public IAsyncRelayCommand<Author> RemoveAuthorCommand { get; }
        public IAsyncRelayCommand AddCopyCommand { get; }
        public IAsyncRelayCommand<BookCopy> RemoveCopyCommand { get; }
        public IAsyncRelayCommand SaveCommand { get; }
        public IAsyncRelayCommand CancelCommand { get; }

        public AddEditBookViewModel(IDatabaseService databaseService, IAuthService authService)
        {
            _databaseService = databaseService;
            _authService = authService;

            AddAuthorCommand = new AsyncRelayCommand(AddAuthorAsync);
            RemoveAuthorCommand = new AsyncRelayCommand<Author>(RemoveAuthorAsync);
            AddCopyCommand = new AsyncRelayCommand(AddCopyAsync);
            RemoveCopyCommand = new AsyncRelayCommand<BookCopy>(RemoveCopyAsync);
            SaveCommand = new AsyncRelayCommand(SaveAsync);
            CancelCommand = new AsyncRelayCommand(CancelAsync);

            LoadBranchesAsync().ConfigureAwait(false);
        }

        public void LoadBookForEdit(Title existingBook)
        {
            Book = existingBook;
            PageTitle = "Edit Book";
            SaveButtonText = "Update";
            // Load authors and copies for this book
        }

        private async Task LoadBranchesAsync()
        {
            try
            {
                var query = "SELECT * FROM branches ORDER BY branch_name";
                var branchesList = await _databaseService.QueryAsync<Branch>(query);
                Branches = new ObservableCollection<Branch>(branchesList ?? new List<Branch>());
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Load branches error: {ex.Message}");
            }
        }

        private Task AddAuthorAsync()
        {
            Authors.Add(new Author());
            return Task.CompletedTask;
        }

        private Task RemoveAuthorAsync(Author author)
        {
            if (author != null)
            {
                Authors.Remove(author);
            }
            return Task.CompletedTask;
        }

        private Task AddCopyAsync()
        {
            Copies.Add(new BookCopy
            {
                Barcode = GenerateBarcode(),
                ConditionStatus = "Good"
            });
            return Task.CompletedTask;
        }

        private string GenerateBarcode()
        {
            return $"BC{DateTime.Now:yyyyMMddHHmmss}{Copies.Count + 1}";
        }

        private Task RemoveCopyAsync(BookCopy copy)
        {
            if (copy != null)
            {
                Copies.Remove(copy);
            }
            return Task.CompletedTask;
        }

        private async Task SaveAsync()
        {
            try
            {
                IsSaving = true;

                if (string.IsNullOrWhiteSpace(Book.TitleName))
                {
                    await Application.Current.MainPage.DisplayAlert("Validation", "Book title is required", "OK");
                    return;
                }

                if (SelectedBranch == null)
                {
                    await Application.Current.MainPage.DisplayAlert("Validation", "Please select a branch", "OK");
                    return;
                }

                // Start a transaction
                if (Book.TitleId == 0)
                {
                    // Insert new book
                    var insertQuery = @"
                        INSERT INTO titles (title_name, isbn, publication_year, publisher)
                        VALUES (@titleName, @isbn, @year, @publisher);
                        SELECT LAST_INSERT_ID();";

                    var parameters = new Dictionary<string, object>
                    {
                        { "@titleName", Book.TitleName },
                        { "@isbn", Book.Isbn ?? "" },
                        { "@year", Book.PublicationYear ?? (object)DBNull.Value },
                        { "@publisher", Book.Publisher ?? "" }
                    };

                    var newId = Convert.ToInt32(await _databaseService.ExecuteScalarAsync(insertQuery, parameters));
                    Book.TitleId = newId;

                    // Insert authors
                    foreach (var author in Authors.Where(a => !string.IsNullOrWhiteSpace(a.AuthorName)))
                    {
                        var authorQuery = @"
                            INSERT INTO authors (author_name) VALUES (@authorName);
                            SELECT LAST_INSERT_ID();";

                        var authorParams = new Dictionary<string, object> { { "@authorName", author.AuthorName } };
                        var authorId = Convert.ToInt32(await _databaseService.ExecuteScalarAsync(authorQuery, authorParams));

                        var linkQuery = "INSERT INTO title_authors (title_id, author_id) VALUES (@titleId, @authorId)";
                        var linkParams = new Dictionary<string, object>
                        {
                            { "@titleId", Book.TitleId },
                            { "@authorId", authorId }
                        };
                        await _databaseService.ExecuteNonQueryAsync(linkQuery, linkParams);
                    }

                    // Insert copies
                    foreach (var copy in Copies)
                    {
                        var copyQuery = @"
                            INSERT INTO book_copies (title_id, branch_id, barcode, condition_status)
                            VALUES (@titleId, @branchId, @barcode, @condition)";

                        var copyParams = new Dictionary<string, object>
                        {
                            { "@titleId", Book.TitleId },
                            { "@branchId", SelectedBranch.BranchId },
                            { "@barcode", copy.Barcode },
                            { "@condition", copy.ConditionStatus }
                        };
                        await _databaseService.ExecuteNonQueryAsync(copyQuery, copyParams);
                    }

                    await Application.Current.MainPage.DisplayAlert("Success", "Book added successfully", "OK");
                }
                else
                {
                    // Update existing book
                    var updateQuery = @"
                        UPDATE titles 
                        SET title_name = @titleName, isbn = @isbn, 
                            publication_year = @year, publisher = @publisher
                        WHERE title_id = @titleId";

                    var updateParams = new Dictionary<string, object>
                    {
                        { "@titleId", Book.TitleId },
                        { "@titleName", Book.TitleName },
                        { "@isbn", Book.Isbn ?? "" },
                        { "@year", Book.PublicationYear ?? (object)DBNull.Value },
                        { "@publisher", Book.Publisher ?? "" }
                    };
                    await _databaseService.ExecuteNonQueryAsync(updateQuery, updateParams);

                    await Application.Current.MainPage.DisplayAlert("Success", "Book updated successfully", "OK");
                }

                await CancelAsync();
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert("Error", $"Failed to save: {ex.Message}", "OK");
            }
            finally
            {
                IsSaving = false;
            }
        }

        private async Task CancelAsync()
        {
            await Shell.Current.GoToAsync("..");
        }
    }
}