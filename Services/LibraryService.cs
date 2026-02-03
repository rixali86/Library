using Library.Models;
using Library.Services;

namespace Library.Services
{
    public interface ILibraryService
    {
        Task<List<Branch>> GetBranchesAsync();
        Task<List<BookCopy>> SearchBooksAsync(string searchTerm);
        Task<List<Loan>> GetMemberLoansAsync(int memberId);
        Task<bool> CheckoutBookAsync(int copyId, int memberId);
        Task<bool> ReturnBookAsync(int loanId);
        Task<List<BranchEvent>> GetUpcomingEventsAsync(int branchId = 0);
        Task<bool> RegisterForEventAsync(int eventId, int memberId);
    }

    public class LibraryService : ILibraryService
    {
        private readonly IDatabaseService _databaseService;

        public LibraryService(IDatabaseService databaseService)
        {
            _databaseService = databaseService;
        }

        public async Task<List<Branch>> GetBranchesAsync()
        {
            var query = "SELECT * FROM branches ORDER BY branch_name";
            return await _databaseService.QueryAsync<Branch>(query);
        }

        public async Task<List<BookCopy>> SearchBooksAsync(string searchTerm)
        {
            var query = @"
                SELECT bc.*, t.title_name, b.branch_name 
                FROM book_copies bc
                JOIN titles t ON bc.title_id = t.title_id
                JOIN branches b ON bc.branch_id = b.branch_id
                WHERE t.title_name LIKE @searchTerm 
                   OR t.isbn LIKE @searchTerm
                ORDER BY t.title_name";

            var parameters = new Dictionary<string, object>
            {
                { "@searchTerm", $"%{searchTerm}%" }
            };

            return await _databaseService.QueryAsync<BookCopy>(query, parameters);
        }

        public async Task<List<Loan>> GetMemberLoansAsync(int memberId)
        {
            var query = @"
                SELECT l.*, t.title_name, bc.barcode, b.branch_name
                FROM loans l
                JOIN book_copies bc ON l.copy_id = bc.copy_id
                JOIN titles t ON bc.title_id = t.title_id
                JOIN branches b ON bc.branch_id = b.branch_id
                WHERE l.member_id = @memberId AND l.status != 'Returned'
                ORDER BY l.due_datetime";

            var parameters = new Dictionary<string, object>
            {
                { "@memberId", memberId }
            };

            return await _databaseService.QueryAsync<Loan>(query, parameters);
        }

        public async Task<bool> CheckoutBookAsync(int copyId, int memberId)
        {
            try
            {
                // Check if copy is available
                var checkQuery = @"
                    SELECT COUNT(*) FROM book_copies bc
                    LEFT JOIN loans l ON bc.copy_id = l.copy_id AND l.status = 'Checked Out'
                    WHERE bc.copy_id = @copyId AND l.loan_id IS NULL";

                var checkParams = new Dictionary<string, object> { { "@copyId", copyId } };
                var available = Convert.ToInt32(await _databaseService.ExecuteScalarAsync(checkQuery, checkParams)) > 0;

                if (!available)
                    return false;

                // Create loan
                var loanQuery = @"
                    INSERT INTO loans (copy_id, member_id, checkout_datetime, due_datetime, status)
                    VALUES (@copyId, @memberId, @checkoutDate, @dueDate, 'Checked Out')";

                var loanParams = new Dictionary<string, object>
                {
                    { "@copyId", copyId },
                    { "@memberId", memberId },
                    { "@checkoutDate", DateTime.Now },
                    { "@dueDate", DateTime.Now.AddDays(14) } // 2 weeks loan period
                };

                await _databaseService.ExecuteNonQueryAsync(loanQuery, loanParams);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Checkout error: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> ReturnBookAsync(int loanId)
        {
            try
            {
                var query = @"
                    UPDATE loans 
                    SET return_datetime = @returnDate, status = 'Returned' 
                    WHERE loan_id = @loanId";

                var parameters = new Dictionary<string, object>
                {
                    { "@loanId", loanId },
                    { "@returnDate", DateTime.Now }
                };

                await _databaseService.ExecuteNonQueryAsync(query, parameters);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Return error: {ex.Message}");
                return false;
            }
        }

        public async Task<List<BranchEvent>> GetUpcomingEventsAsync(int branchId = 0)
        {
            var query = @"
                SELECT e.*, b.branch_name 
                FROM branch_events e
                JOIN branches b ON e.branch_id = b.branch_id
                WHERE e.start_datetime > @now";

            var parameters = new Dictionary<string, object>
            {
                { "@now", DateTime.Now }
            };

            if (branchId > 0)
            {
                query += " AND e.branch_id = @branchId";
                parameters.Add("@branchId", branchId);
            }

            query += " ORDER BY e.start_datetime LIMIT 20";

            return await _databaseService.QueryAsync<BranchEvent>(query, parameters);
        }

        public async Task<bool> RegisterForEventAsync(int eventId, int memberId)
        {
            try
            {
                // Check if already registered
                var checkQuery = "SELECT COUNT(*) FROM event_registrations WHERE event_id = @eventId AND member_id = @memberId";
                var checkParams = new Dictionary<string, object>
                {
                    { "@eventId", eventId },
                    { "@memberId", memberId }
                };

                var alreadyRegistered = Convert.ToInt32(await _databaseService.ExecuteScalarAsync(checkQuery, checkParams)) > 0;

                if (alreadyRegistered)
                    return false;

                // Register
                var registerQuery = @"
                    INSERT INTO event_registrations (event_id, member_id, registration_datetime, attendance_status)
                    VALUES (@eventId, @memberId, @regDate, 'Registered')";

                var registerParams = new Dictionary<string, object>
                {
                    { "@eventId", eventId },
                    { "@memberId", memberId },
                    { "@regDate", DateTime.Now }
                };

                await _databaseService.ExecuteNonQueryAsync(registerQuery, registerParams);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Event registration error: {ex.Message}");
                return false;
            }
        }
    }
}   