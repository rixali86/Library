using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Library.Services;
using System.Collections.ObjectModel;
using Library.Models;
using Microsoft.Maui.Controls;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace Library.ViewModels;

public partial class SearchBookViewModel : ObservableObject
{
    private readonly IDatabaseService _databaseService;

    [ObservableProperty]
    private string searchQuery = string.Empty;

    [ObservableProperty]
    private ObservableCollection<Title> titles = new();

    [ObservableProperty]
    private bool isLoading = false;

    public SearchBookViewModel(IDatabaseService databaseService)
    {
        _databaseService = databaseService;
    }

    [RelayCommand]
    public async Task SearchAsync()
    {
        await LoadTitlesAsync();
    }

    public async Task LoadTitlesAsync()
    {
        IsLoading = true;
        try
        {
            var q = string.IsNullOrWhiteSpace(SearchQuery) ? "%" : $"%{SearchQuery.Trim()}%";

            // MySQL-compatible GROUP_CONCAT and alias to match Title.Author property
            var sql = @"
SELECT t.title_id AS TitleId,
       t.title_name AS TitleName,
       t.isbn AS Isbn,
       t.publication_year AS PublicationYear,
       t.publisher AS Publisher,
       GROUP_CONCAT(DISTINCT a.author_name SEPARATOR ', ') AS Author
FROM titles t
LEFT JOIN title_authors ta ON t.title_id = ta.title_id
LEFT JOIN authors a ON ta.author_id = a.author_id
WHERE t.title_name LIKE @q
   OR t.isbn LIKE @q
   OR a.author_name LIKE @q
GROUP BY t.title_id
LIMIT 100;";

            var parameters = new Dictionary<string, object> { { "@q", q } };

            var result = await _databaseService.QueryAsync<Title>(sql, parameters);

            Titles = new ObservableCollection<Title>(result ?? new List<Title>());
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"Load titles error: {ex.Message}");
            Titles = new ObservableCollection<Title>();
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task SelectTitleAsync(Title title)
    {
        if (title == null) return;

        var details = $"{title.TitleName}\nAuthors: {(string.IsNullOrEmpty(title.Author) ? "Unknown" : title.Author)}\nISBN: {title.Isbn}\nPublisher: {title.Publisher}\nYear: {title.PublicationYear}";
        await Application.Current.MainPage.DisplayAlert("Title", details, "OK");
    }
}