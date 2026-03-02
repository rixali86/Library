using Library.Models;
using Library.Services;
using MySql.Data.MySqlClient;
using System.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

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

        Task<List<MemberMetadata>> GetMemberMetadataAsync(int memberId);
        Task<bool> AddMemberMetadataAsync(int memberId, string key, string value, string performedBy, string userRole);
        Task<bool> UpdateMemberMetadataAsync(int metaId, string newValue, string performedBy, string userRole);
        Task<bool> DeleteMemberMetadataAsync(int metaId, string performedBy, string userRole);

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
            var branches = await _databaseService.QueryAsync<Branch>(query);

            if (branches == null || branches.Count == 0)
                return branches;

            // Build parameters for IN() safely (param per id)
            var parameters = new Dictionary<string, object>();
            var paramNames = new List<string>();
            for (int i = 0; i < branches.Count; i++)
            {
                var name = $"@id{i}";
                parameters[name] = branches[i].BranchId;
                paramNames.Add(name);
            }

            var inClause = string.Join(",", paramNames);

            // Load hours and contacts in two queries and attach to each branch.
            var hoursQuery = $"SELECT * FROM branch_hours WHERE branch_id IN ({inClause}) ORDER BY branch_id, hour_id";
            var contactsQuery = $"SELECT * FROM branch_contacts WHERE branch_id IN ({inClause}) ORDER BY branch_id, contact_id";

            var hours = await _databaseService.QueryAsync<BranchHours>(hoursQuery, parameters);
            var contacts = await _databaseService.QueryAsync<BranchContact>(contactsQuery, parameters);

            var hoursLookup = hours?.GroupBy(h => h.BranchId).ToDictionary(g => g.Key, g => g.ToList()) ?? new Dictionary<int, List<BranchHours>>();
            var contactsLookup = contacts?.GroupBy(c => c.BranchId).ToDictionary(g => g.Key, g => g.ToList()) ?? new Dictionary<int, List<BranchContact>>();

            foreach (var b in branches)
            {
                if (hoursLookup.TryGetValue(b.BranchId, out var hlist))
                    b.Hours = hlist;
               
            }

            return branches;
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
                var checkQuery = @"
                    SELECT COUNT(*) FROM book_copies bc
                    LEFT JOIN loans l ON bc.copy_id = l.copy_id AND l.status = 'Checked Out'
                    WHERE bc.copy_id = @copyId AND l.loan_id IS NULL";

                var checkParams = new Dictionary<string, object> { { "@copyId", copyId } };
                var available = Convert.ToInt32(await _databaseService.ExecuteScalarAsync(checkQuery, checkParams)) > 0;

                if (!available)
                    return false;

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
                var checkQuery = "SELECT COUNT(*) FROM event_registrations WHERE event_id = @eventId AND member_id = @memberId";
                var checkParams = new Dictionary<string, object>
                {
                    { "@eventId", eventId },
                    { "@memberId", memberId }
                };

                var alreadyRegistered = Convert.ToInt32(await _databaseService.ExecuteScalarAsync(checkQuery, checkParams)) > 0;

                if (alreadyRegistered)
                    return false;

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

        public async Task<List<MemberMetadata>> GetMemberMetadataAsync(int memberId)
        {
            var query = "SELECT * FROM member_metadata WHERE member_id = @memberId ORDER BY created_at DESC";
            var parameters = new Dictionary<string, object> { { "@memberId", memberId } };
            return await _databaseService.QueryAsync<MemberMetadata>(query, parameters);
        }

        public async Task<bool> AddMemberMetadataAsync(int memberId, string key, string value, string performedBy, string userRole)
        {
            if (userRole != "Librarian")
                throw new UnauthorizedAccessException("Only librarians can add metadata.");

            var query = "INSERT INTO member_metadata (member_id, meta_key, meta_value) VALUES (@memberId, @key, @value)";
            var parameters = new Dictionary<string, object>
        {
            { "@memberId", memberId },
            { "@key", key },
            { "@value", value }
        };

            await _databaseService.ExecuteNonQueryAsync(query, parameters);

            // Fetch newly created meta_id
            var metaId = Convert.ToInt32(await _databaseService.ExecuteScalarAsync("SELECT LAST_INSERT_ID();", null));

            var meta = new MemberMetadata
            {
                MetaId = metaId,
                MemberId = memberId,
                MetaKey = key,
                MetaValue = value
            };

            await LogMetadataActionAsync(meta, "Added", performedBy, oldValue: null);

            return true;
        }

        public async Task<bool> UpdateMemberMetadataAsync(int metaId, string newValue, string performedBy, string userRole)
        {
            if (userRole != "Librarian")
                throw new UnauthorizedAccessException("Only librarians can edit metadata.");

            var meta = await _databaseService.QuerySingleAsync<MemberMetadata>(
                "SELECT * FROM member_metadata WHERE meta_id = @metaId",
                new Dictionary<string, object> { { "@metaId", metaId } });

            if (meta == null) return false;

            var oldValue = meta.MetaValue;

            var query = "UPDATE member_metadata SET meta_value = @value WHERE meta_id = @metaId";
            await _databaseService.ExecuteNonQueryAsync(query, new Dictionary<string, object>
        {
            { "@value", newValue },
            { "@metaId", metaId }
        });

            meta.MetaValue = newValue;
            await LogMetadataActionAsync(meta, "Edited", performedBy, oldValue);

            return true;
        }

        public async Task<bool> DeleteMemberMetadataAsync(int metaId, string performedBy, string userRole)
        {
            if (userRole != "Librarian")
                throw new UnauthorizedAccessException("Only librarians can delete metadata.");

            var meta = await _databaseService.QuerySingleAsync<MemberMetadata>(
                "SELECT * FROM member_metadata WHERE meta_id = @metaId",
                new Dictionary<string, object> { { "@metaId", metaId } });

            if (meta == null) return false;

            await _databaseService.ExecuteNonQueryAsync(
                "DELETE FROM member_metadata WHERE meta_id = @metaId",
                new Dictionary<string, object> { { "@metaId", metaId } });

            await LogMetadataActionAsync(meta, "Deleted", performedBy, oldValue: meta.MetaValue);

            return true;
        }

        private async Task LogMetadataActionAsync(MemberMetadata meta, string action, string performedBy, string oldValue)
        {
            var query = @"
            INSERT INTO member_metadata_audit (meta_id, member_id, action, old_value, new_value, performed_by)
            VALUES (@metaId, @memberId, @action, @oldValue, @newValue, @performedBy)";

            await _databaseService.ExecuteNonQueryAsync(query, new Dictionary<string, object>
        {
            { "@metaId", meta.MetaId },
            { "@memberId", meta.MemberId },
            { "@action", action },
            { "@oldValue", oldValue ?? "" },
            { "@newValue", meta.MetaValue ?? "" },
            { "@performedBy", performedBy }
        });
        }

        // TODO: Keep your existing LibraryService methods like GetMemberLoansAsync, ReturnBookAsync, etc.
    }

}
