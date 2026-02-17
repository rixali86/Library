using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Library.Models;
using Library.Services;
using Microsoft.Maui.Controls;
using System.Collections.ObjectModel;

namespace Library.ViewModels;

public partial class SearchBookViewModel : ObservableObject
{
    private readonly IDatabaseService _databaseService;

    private int CurrentMemberId => UserSession.Instance.UserId ?? 0;

    [ObservableProperty]
    private string searchQuery = string.Empty;

    [ObservableProperty]
    private ObservableCollection<Title> titles = new();

    [ObservableProperty]
    private bool isLoading;

    public SearchBookViewModel(IDatabaseService databaseService)
    {
        _databaseService = databaseService;
    }

    // ── Search ──────────────────────────────────────────────────────────────

    [RelayCommand]
    public async Task SearchAsync() => await LoadTitlesAsync();

    public async Task LoadTitlesAsync()
    {
        IsLoading = true;
        try
        {
            var q = string.IsNullOrWhiteSpace(SearchQuery) ? "%" : $"%{SearchQuery.Trim()}%";

            const string sql = @"
SELECT t.title_id         AS TitleId,
       t.title_name       AS TitleName,
       t.isbn             AS Isbn,
       t.publication_year AS PublicationYear,
       t.publisher        AS Publisher,
       GROUP_CONCAT(DISTINCT a.author_name SEPARATOR ', ') AS Author
FROM   titles t
LEFT JOIN title_authors ta ON t.title_id  = ta.title_id
LEFT JOIN authors       a  ON ta.author_id = a.author_id
WHERE  t.title_name  LIKE @q
   OR  t.isbn        LIKE @q
   OR  a.author_name LIKE @q
GROUP BY t.title_id
LIMIT 100;";

            var result = await _databaseService.QueryAsync<Title>(sql,
                new Dictionary<string, object> { ["@q"] = q });

            Titles = new ObservableCollection<Title>(result ?? []);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"LoadTitles error: {ex.Message}");
            Titles = [];
        }
        finally
        {
            IsLoading = false;
        }
    }

    // ── View details (tap row) ───────────────────────────────────────────────

    [RelayCommand]
    private async Task SelectTitleAsync(Title title)
    {
        if (title is null) return;
        var details = $"{title.TitleName}\n" +
                      $"Authors: {(string.IsNullOrEmpty(title.Author) ? "Unknown" : title.Author)}\n" +
                      $"ISBN: {title.Isbn}\n" +
                      $"Publisher: {title.Publisher}\n" +
                      $"Year: {title.PublicationYear}";
        await Application.Current!.MainPage!.DisplayAlert("Book Details", details, "OK");
    }

    // ── Request book ─────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task RequestBookAsync(Title title)
    {
        if (title is null) return;

        // ── Step 1: Check available copies BEFORE asking for a note ──────────
        // A copy is "available" when it is not currently checked out in loans.
        const string copyCheckSql = @"
SELECT COUNT(*) 
FROM   book_copies c
WHERE  c.title_id = @titleId
  AND  c.copy_id NOT IN (
       SELECT l.copy_id
       FROM   loans l
       WHERE  l.status = 'Checked Out'
  );";

        try
        {
            var copyScalar = await _databaseService.ExecuteScalarAsync(copyCheckSql,
                new Dictionary<string, object> { ["@titleId"] = title.TitleId });

            long availableCopies = copyScalar is not null ? Convert.ToInt64(copyScalar) : 0;

            if (availableCopies == 0)
            {
                await Application.Current!.MainPage!.DisplayAlert(
                    "No Copies Available",
                    $"Sorry, all copies of \"{title.TitleName}\" are currently checked out.\n\nYour request has not been submitted.",
                    "OK");
                return;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Copy-check error: {ex.Message}");
            await Application.Current!.MainPage!.DisplayAlert(
                "Error",
                $"Could not check copy availability.\n\n{ex.GetType().Name}: {ex.Message}",
                "OK");
            return;
        }

        // ── Step 2: Check for duplicate request ──────────────────────────────
        const string duplicateCheckSql = @"
SELECT status
FROM   book_requests
WHERE  member_id = @memberId
  AND  title_id  = @titleId
ORDER BY request_date DESC
LIMIT 1;";

        try
        {
            var existingScalar = await _databaseService.ExecuteScalarAsync(duplicateCheckSql,
                new Dictionary<string, object>
                {
                    ["@memberId"] = CurrentMemberId,
                    ["@titleId"] = title.TitleId
                });

            if (existingScalar is not null)
            {
                var existingStatus = existingScalar.ToString();
                var message = existingStatus switch
                {
                    "Pending" => "You already have a pending request for this book.",
                    "Approved" => "Your request for this book has already been approved.",
                    "Ordered" => "This book has already been ordered for you.",
                    "Rejected" => "Your previous request was rejected. Please contact the librarian.",
                    _ => "You have already requested this book."
                };
                await Application.Current!.MainPage!.DisplayAlert("Already Requested", message, "OK");
                return;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Duplicate-check error: {ex.Message}");
            // Non-fatal — continue to insert.
        }

        // ── Step 3: Ask for optional note ────────────────────────────────────
        var note = await Application.Current!.MainPage!.DisplayPromptAsync(
            title: "Request Book",
            message: $"\"{title.TitleName}\"\n\nAdd a note for the librarian (optional):",
            accept: "Send Request",
            cancel: "Cancel",
            placeholder: "e.g. Required for coursework",
            maxLength: 500,
            keyboard: Keyboard.Text);

        if (note is null) return;   // user cancelled

        // ── Step 4: Insert request ────────────────────────────────────────────
        const string insertSql = @"
INSERT INTO book_requests
    (member_id, title_id, book_title, author, isbn, status, request_date, notes)
VALUES
    (@memberId, @titleId, @bookTitle, @author, @isbn, 'Pending', UTC_TIMESTAMP(), @notes);";

        try
        {
            await _databaseService.ExecuteAsync(insertSql,
                new Dictionary<string, object>
                {
                    ["@memberId"] = CurrentMemberId,
                    ["@titleId"] = title.TitleId,
                    ["@bookTitle"] = title.TitleName,
                    ["@author"] = title.Author ?? string.Empty,
                    ["@isbn"] = title.Isbn ?? string.Empty,
                    ["@notes"] = note.Trim()
                });

            await Application.Current.MainPage!.DisplayAlert(
                "Request Sent ✓",
                $"Your request for \"{title.TitleName}\" has been sent to the librarian.",
                "OK");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"RequestBook insert error: {ex}");
            await Application.Current.MainPage!.DisplayAlert(
                "Error",
                $"Could not send your request.\n\n{ex.GetType().Name}: {ex.Message}",
                "OK");
        }
    }
}