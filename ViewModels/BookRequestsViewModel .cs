using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Library.Models;
using Library.Services;
using Microsoft.Maui.Controls;
using System.Collections.ObjectModel;

namespace Library.ViewModels;

/// <summary>
/// Librarian-facing view model.
/// - Shows pending book requests with a notification count.
/// - Librarian can Approve (auto-assigns copy → creates loan) or Reject.
/// - On Approve: checks available copies. If none → auto-rejects with reason.
/// </summary>
public partial class BookRequestsViewModel : ObservableObject
{
    private readonly IDatabaseService _databaseService;

    private int CurrentLibrarianId => UserSession.Instance.UserId ?? 0;

    // Default loan duration in days — change here to adjust globally
    private const int LoanDays = 14;

    [ObservableProperty]
    private ObservableCollection<BookRequestDisplay> requests = new();

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private string selectedStatus = "Pending";

    /// <summary>Pending request count — shown as notification badge on dashboard.</summary>
    [ObservableProperty]
    private int pendingCount;

    [ObservableProperty]
    private bool hasPendingRequests;

    partial void OnSelectedStatusChanged(string value) => Task.Run(LoadRequestsAsync);

    public BookRequestsViewModel(IDatabaseService databaseService)
    {
        _databaseService = databaseService;
    }

    // ── Load requests ─────────────────────────────────────────────────────────

    [RelayCommand]
    public async Task LoadRequestsAsync()
    {
        IsLoading = true;
        try
        {
            // Load filtered list
            var whereClause = SelectedStatus == "All"
                ? string.Empty
                : "WHERE br.status = @status";

            var sql = $@"
SELECT br.request_id     AS RequestId,
       br.member_id      AS MemberId,
       br.title_id       AS TitleId,
       br.book_title     AS BookTitle,
       br.author         AS Author,
       br.isbn           AS Isbn,
       br.status         AS Status,
       br.request_date   AS RequestDate,
       br.processed_by   AS ProcessedBy,
       br.processed_date AS ProcessedDate,
       br.notes          AS Notes,
       COALESCE(CONCAT(m.first_name, ' ', m.last_name),
                CONCAT('Member #', br.member_id)) AS MemberName
FROM   book_requests br
LEFT JOIN members m ON br.member_id = m.member_id
{whereClause}
ORDER BY br.request_date DESC
LIMIT 200;";

            var parameters = new Dictionary<string, object>();
            if (SelectedStatus != "All")
                parameters["@status"] = SelectedStatus;

            var rows = await _databaseService.QueryAsync<BookRequestRow>(sql, parameters);

            Requests = new ObservableCollection<BookRequestDisplay>(
                (rows ?? Enumerable.Empty<BookRequestRow>())
                    .Select(r => new BookRequestDisplay(r)));

            // Always refresh the pending badge count
            await RefreshPendingCountAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"LoadRequests error: {ex.Message}");
            Requests = [];
        }
        finally
        {
            IsLoading = false;
        }
    }

    // ── Pending badge count ───────────────────────────────────────────────────

    /// <summary>
    /// Call this from LibrarianDashboardViewModel.OnAppearing to refresh the badge.
    /// </summary>
    public async Task RefreshPendingCountAsync()
    {
        try
        {
            const string sql = "SELECT COUNT(*) FROM book_requests WHERE status = 'Pending';";
            var scalar = await _databaseService.ExecuteScalarAsync(sql, new Dictionary<string, object>());
            var PendingCount = scalar is not null ? Convert.ToInt32(scalar) : 0;
            var HasPendingRequests = PendingCount > 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"PendingCount error: {ex.Message}");
        }
    }

    // ── Approve ───────────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task ApproveAsync(BookRequestDisplay item)
    {
        if (item is null) return;

        // Check available copies first
        int availableCopyId = await GetAvailableCopyIdAsync(item.TitleId);

        if (availableCopyId == 0)
        {
            // No copies available — auto-reject with clear reason
            bool autoReject = await Application.Current!.MainPage!.DisplayAlert(
                "No Copies Available",
                $"All copies of \"{item.BookTitle}\" are currently checked out.\n\nWould you like to reject this request?",
                "Reject Request", "Cancel");

            if (!autoReject) return;

            await SetStatusAsync(item, "Rejected",
                "No copies currently available. Please try again later.");
            return;
        }

        // Copies available — confirm approval
        bool ok = await Application.Current!.MainPage!.DisplayAlert(
            "Approve Request",
            $"Approve request for \"{item.BookTitle}\"?\n\nA loan will be created for {item.MemberName}.\nDue date: {DateTime.Now.AddDays(LoanDays):MMM d, yyyy}",
            "Approve", "Cancel");

        if (!ok) return;

        await SetStatusAsync(item, "Approved", copyId: availableCopyId);
    }

    // ── Reject ────────────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task RejectAsync(BookRequestDisplay item)
    {
        if (item is null) return;

        var reason = await Application.Current!.MainPage!.DisplayPromptAsync(
            title: "Reject Request",
            message: $"Reason for rejecting \"{item.BookTitle}\":",
            accept: "Reject",
            cancel: "Cancel",
            placeholder: "e.g. Already available on shelf",
            maxLength: 500,
            keyboard: Keyboard.Text);

        if (reason is null) return;   // cancelled

        await SetStatusAsync(item, "Rejected", reason.Trim());
    }

    // ── Shared status updater ─────────────────────────────────────────────────

    private async Task SetStatusAsync(
        BookRequestDisplay item,
        string newStatus,
        string? notes = null,
        int copyId = 0)
    {
        var updatedNotes = notes is { Length: > 0 }
            ? (string.IsNullOrWhiteSpace(item.Notes)
                ? notes
                : $"{item.Notes}\n[Librarian] {notes}")
            : item.Notes;

        const string updateSql = @"
UPDATE book_requests
SET    status         = @status,
       processed_by   = @processedBy,
       processed_date = UTC_TIMESTAMP(),
       notes          = @notes
WHERE  request_id     = @requestId;";

        try
        {
            await _databaseService.ExecuteAsync(updateSql,
                new Dictionary<string, object>
                {
                    ["@status"] = newStatus,
                    ["@processedBy"] = CurrentLibrarianId,
                    ["@notes"] = updatedNotes ?? string.Empty,
                    ["@requestId"] = item.RequestId
                });

            // If approved and a valid copy was passed — create the loan
            if (newStatus == "Approved" && copyId > 0)
                await CreateLoanAsync(item, copyId);

            // Update display row in-place
            item.Status = newStatus;
            item.Notes = updatedNotes ?? string.Empty;
            item.ProcessedDate = DateTime.UtcNow;
            item.RefreshState();

            // Refresh badge
            await RefreshPendingCountAsync();

            var message = newStatus == "Approved"
                ? $"✓ Request approved.\nLoan created for {item.MemberName}.\nDue: {DateTime.Now.AddDays(LoanDays):MMM d, yyyy}"
                : $"Request rejected.";

            await Application.Current!.MainPage!.DisplayAlert(
                newStatus == "Approved" ? "Approved ✓" : "Rejected", message, "OK");

            // Remove from filtered view
            if (SelectedStatus != "All")
                Requests.Remove(item);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"SetStatus error: {ex.Message}");
            await Application.Current!.MainPage!.DisplayAlert(
                "Error", $"Could not update the request.\n{ex.Message}", "OK");
        }
    }

    // ── Find available copy ───────────────────────────────────────────────────

    /// <summary>
    /// Returns the copy_id of the first available copy for a title,
    /// or 0 if none are available.
    /// </summary>
    private async Task<int> GetAvailableCopyIdAsync(int titleId)
    {
        const string sql = @"
SELECT c.copy_id
FROM   book_copies c
WHERE  c.title_id = @titleId
  AND  c.copy_id NOT IN (
       SELECT l.copy_id
       FROM   loans l
       WHERE  l.status = 'Checked Out'
  )
ORDER BY c.copy_id
LIMIT 1;";

        try
        {
            var scalar = await _databaseService.ExecuteScalarAsync(sql,
                new Dictionary<string, object> { ["@titleId"] = titleId });

            return scalar is not null ? Convert.ToInt32(scalar) : 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"GetAvailableCopy error: {ex.Message}");
            return 0;
        }
    }

    // ── Create loan record ────────────────────────────────────────────────────

    private async Task CreateLoanAsync(BookRequestDisplay item, int copyId)
    {
        const string sql = @"
INSERT INTO loans (copy_id, member_id, checkout_datetime, due_datetime, status)
VALUES (@copyId, @memberId, UTC_TIMESTAMP(), @dueDate, 'Checked Out');";

        await _databaseService.ExecuteAsync(sql,
            new Dictionary<string, object>
            {
                ["@copyId"] = copyId,
                ["@memberId"] = item.MemberId,
                ["@dueDate"] = DateTime.UtcNow.AddDays(LoanDays)
            });
    }
}

// ── DTOs ──────────────────────────────────────────────────────────────────────

public class BookRequestRow
{
    public int RequestId { get; set; }
    public int MemberId { get; set; }
    public int TitleId { get; set; }
    public string BookTitle { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string Isbn { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public DateTime RequestDate { get; set; }
    public int? ProcessedBy { get; set; }
    public DateTime? ProcessedDate { get; set; }
    public string Notes { get; set; } = string.Empty;
    public string MemberName { get; set; } = string.Empty;
}

public partial class BookRequestDisplay : ObservableObject
{
    public int RequestId { get; }
    public int MemberId { get; }
    public int TitleId { get; }
    public string BookTitle { get; }
    public string Author { get; }
    public string Isbn { get; }
    public DateTime RequestDate { get; }
    public string MemberName { get; }

    [ObservableProperty] private string status;
    [ObservableProperty] private string notes;
    [ObservableProperty] private DateTime? processedDate;
    [ObservableProperty] private bool isPending;
    [ObservableProperty] private bool isApproved;
    [ObservableProperty] private bool isRejected;
    [ObservableProperty] private bool isOrdered;
    [ObservableProperty] private Color statusColor;
    [ObservableProperty] private string memberInfo;

    public bool HasNotes => !string.IsNullOrWhiteSpace(Notes);

    public BookRequestDisplay(BookRequestRow r)
    {
        RequestId = r.RequestId;
        MemberId = r.MemberId;
        TitleId = r.TitleId;
        BookTitle = r.BookTitle;
        Author = r.Author;
        Isbn = r.Isbn;
        RequestDate = r.RequestDate;
        MemberName = r.MemberName;
        status = r.Status;
        notes = r.Notes;
        processedDate = r.ProcessedDate;
        memberInfo = $"{r.MemberName}  ·  {r.RequestDate:MMM d, yyyy  h:mm tt}";
        RefreshState();
    }

    public void RefreshState()
    {
        IsPending = Status == "Pending";
        IsApproved = Status == "Approved";
        IsRejected = Status == "Rejected";
        IsOrdered = Status == "Ordered";

        StatusColor = Status switch
        {
            "Approved" => Color.FromArgb("#27AE60"),
            "Rejected" => Color.FromArgb("#E74C3C"),
            "Ordered" => Color.FromArgb("#8E44AD"),
            _ => Color.FromArgb("#F39C12"),
        };
    }
}