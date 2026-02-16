using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Library.Models;
using Library.Services;
using System.Collections.ObjectModel;

namespace Library.ViewModels
{
    public partial class ManageBooksViewModel : ObservableObject
    {
        private readonly IDatabaseService _databaseService;
        private readonly ILibraryService _libraryService;

        [ObservableProperty]
        private string searchQuery = string.Empty;

        [ObservableProperty]
        private ObservableCollection<Title> books = new();

        [ObservableProperty]
        private bool isLoading = false;

        [ObservableProperty]
        private int totalBooks;

        [ObservableProperty]
        private int availableBooks;

        [ObservableProperty]
        private int checkedOutBooks;

        [ObservableProperty]
        private List<string> filterOptions = new() { "All", "Available", "Checked Out", "Overdue" };

        [ObservableProperty]
        private string selectedFilter = "All";

        public IAsyncRelayCommand SearchCommand { get; }
        public IAsyncRelayCommand AddBookCommand { get; }
        public IAsyncRelayCommand<Title> EditBookCommand { get; }
        public IAsyncRelayCommand<Title> DeleteBookCommand { get; }
        public IAsyncRelayCommand<Title> ViewCopiesCommand { get; }
        public IAsyncRelayCommand ApplyFilterCommand { get; }
        public IRelayCommand ClearFilterCommand { get; }

        public ManageBooksViewModel(IDatabaseService databaseService, ILibraryService libraryService)
        {
            _databaseService = databaseService;
            _libraryService = libraryService;

            SearchCommand = new AsyncRelayCommand(LoadBooksAsync);
            AddBookCommand = new AsyncRelayCommand(AddBookAsync);
            EditBookCommand = new AsyncRelayCommand<Title>(EditBookAsync);
            DeleteBookCommand = new AsyncRelayCommand<Title>(DeleteBookAsync);
            ViewCopiesCommand = new AsyncRelayCommand<Title>(ViewCopiesAsync);
            ApplyFilterCommand = new AsyncRelayCommand(ApplyFilterAsync);
            ClearFilterCommand = new RelayCommand(ClearFilter);
        }

        public async Task LoadBooksAsync()
        {
            try
            {
                IsLoading = true;
                var query = BuildSearchQuery();
                var parameters = new Dictionary<string, object>();

                if (!string.IsNullOrWhiteSpace(SearchQuery))
                {
                    parameters["@search"] = $"%{SearchQuery}%";
                }

                var booksList = await _databaseService.QueryAsync<Title>(query, parameters);
                Books= new ObservableCollection<Title>(booksList ?? new List<Title>());

                await LoadStatisticsAsync();
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert("Error", $"Failed to load books: {ex.Message}", "OK");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private string BuildSearchQuery()
        {
            var baseQuery = @"
                SELECT t.*, 
                       GROUP_CONCAT(DISTINCT a.author_name SEPARATOR ', ') as Author
                FROM titles t
                LEFT JOIN title_authors ta ON t.title_id = ta.title_id
                LEFT JOIN authors a ON ta.author_id = a.author_id";

            if (!string.IsNullOrWhiteSpace(SearchQuery))
            {
                baseQuery += " WHERE t.title_name LIKE @search OR t.isbn LIKE @search";
            }

            baseQuery += " GROUP BY t.title_id ORDER BY t.title_name";
            return baseQuery;
        }

        private async Task LoadStatisticsAsync()
        {
            try
            {
                var totalQuery = "SELECT COUNT(*) FROM titles";
                TotalBooks = Convert.ToInt32(await _databaseService.ExecuteScalarAsync(totalQuery));

                var availableQuery = @"
                    SELECT COUNT(*) 
                    FROM book_copies bc
                    LEFT JOIN loans l ON bc.copy_id = l.copy_id AND l.status = 'Checked Out'
                    WHERE l.loan_id IS NULL";
                AvailableBooks = Convert.ToInt32(await _databaseService.ExecuteScalarAsync(availableQuery));

                var checkedOutQuery = @"
                    SELECT COUNT(DISTINCT bc.copy_id)
                    FROM book_copies bc
                    JOIN loans l ON bc.copy_id = l.copy_id
                    WHERE l.status = 'Checked Out'";
                CheckedOutBooks = Convert.ToInt32(await _databaseService.ExecuteScalarAsync(checkedOutQuery));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Stats error: {ex.Message}");
            }
        }

        private async Task AddBookAsync()
        {
            var parameters = new Dictionary<string, object>
            {
                ["book"] = new Title()
            };
            await Shell.Current.GoToAsync("AddBook", parameters);
        }

        private async Task EditBookAsync(Title book)
        {
            if (book == null) return;

            var parameters = new Dictionary<string, object>
            {
                ["book"] = book
            };
            await Shell.Current.GoToAsync("EditBook", parameters);
        }

        private async Task DeleteBookAsync(Title book)
        {
            if (book == null) return;

            var confirm = await Application.Current.MainPage.DisplayAlert(
                "Confirm Delete",
                $"Are you sure you want to delete '{book.TitleName}'?",
                "Yes", "No");

            if (confirm)
            {
                try
                {
                    var query = "DELETE FROM titles WHERE title_id = @titleId";
                    var parameters = new Dictionary<string, object> { { "@titleId", book.TitleId } };
                    await _databaseService.ExecuteNonQueryAsync(query, parameters);
                    await LoadBooksAsync();
                }
                catch (Exception ex)
                {
                    await Application.Current.MainPage.DisplayAlert("Error", $"Failed to delete: {ex.Message}", "OK");
                }
            }
        }

        private async Task ViewCopiesAsync(Title book)
        {
            if (book == null) return;
            // Navigate to copies management page
            await Application.Current.MainPage.DisplayAlert("Info", $"View copies for {book.TitleName}", "OK");
        }

        private async Task ApplyFilterAsync()
        {
            await LoadBooksAsync();
        }

        private void ClearFilter()
        {
            SelectedFilter = "All";
            SearchQuery = string.Empty;
            _ = LoadBooksAsync();
        }
    }
}