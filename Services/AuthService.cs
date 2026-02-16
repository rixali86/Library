using Library.Models;
using System.Security.Cryptography;
using System.Text;
using System.Diagnostics;
using Microsoft.Maui.Storage;


namespace Library.Services;

public interface IAuthService
{
    Task<bool> LoginAsync(string email, string password);
    Task<bool> RegisterAsync(string email, string password, Member memberInfo);
    Task<bool> ChangePasswordAsync(string email, string oldPassword, string newPassword);
    Task<bool> LogoutAsync();
    bool IsAuthenticated { get; }
    string CurrentUserEmail { get; }
    string CurrentUserRole { get; }
    int CurrentUserId { get; }
    Task<Member> GetCurrentUserAsync();
}

public class AuthService : IAuthService
{
    private readonly IDatabaseService _databaseService;
    private bool _isAuthenticated = false;
    private string _currentUserEmail = string.Empty;
    private string _currentUserRole = string.Empty;
    private int _currentUserId = 0;

    public AuthService(IDatabaseService databaseService)
    {
        _databaseService = databaseService;
    }

    public bool IsAuthenticated => _isAuthenticated;
    public string CurrentUserEmail => _currentUserEmail;
    public string CurrentUserRole => _currentUserRole;
    public int CurrentUserId => _currentUserId;

    public async Task<bool> LoginAsync(string email, string password)
    {
        try
        {
            var hashedPassword = HashPassword(password);

            var query = @"
        SELECT member_id, email, role
        FROM members
        WHERE email = @email
        AND password = @hashedPassword
        LIMIT 1;";

            var parameters = new Dictionary<string, object>
        {
            { "@email", email },
            { "@hashedPassword", hashedPassword }
        };

            var dataTable = await _databaseService.ExecuteQueryAsync(query, parameters);

            if (dataTable.Rows.Count > 0)
            {
                var row = dataTable.Rows[0];

                _currentUserId = Convert.ToInt32(row["member_id"]);
                _currentUserEmail = row["email"].ToString() ?? string.Empty;
                _currentUserRole = row["role"]?.ToString() ?? "Member";
                _isAuthenticated = true;

                // Start session
                await UserSession.Instance.StartSessionAsync(
                    _currentUserEmail,
                    _currentUserRole,
                    _currentUserId
                );

                // 🔥 Role Based Navigation
               /* switch (_currentUserRole)
                {
                    case "Librarian":
                        await Shell.Current.GoToAsync("//LibrarianDashboard");
                        break;

                    case "Member":
                        await Shell.Current.GoToAsync("//MemberDashboard");
                        break;

                    default:
                        await Shell.Current.GoToAsync("//DefaultPage");
                        break;
                } */

                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            Trace.TraceError("Login error: {0}", ex);
            return false;
        }
    }


    public async Task<bool> RegisterAsync(string email, string password, Member memberInfo)
    {
        try
        {
            email = email.Trim().ToLower();

            var checkQuery = "SELECT COUNT(*) FROM members WHERE LOWER(email) = @email";
            var checkParams = new Dictionary<string, object> { { "@email", email } };
            var existingCount = Convert.ToInt32(await _databaseService.ExecuteScalarAsync(checkQuery, checkParams));

            if (existingCount > 0) return false;

            var hashedPassword = HashPassword(password);

            var memberQuery = @"
INSERT INTO members 
(first_name, last_name, dateofbirth, email, phone, registered_date, password, role) 
VALUES (@firstName, @lastName, @dob, @email, @phone, @registeredDate, @password, 'Member');
SELECT LAST_INSERT_ID();";

            var memberParams = new Dictionary<string, object>
            {
                { "@firstName", memberInfo.FirstName },
                { "@lastName", memberInfo.LastName },
                { "@dob", memberInfo.DateOfBirth ?? (object)DBNull.Value },
                { "@email", email },
                { "@phone", memberInfo.Phone ?? (object)DBNull.Value },
                { "@registeredDate", DateTime.Now },
                { "@password", hashedPassword }
            };

            var memberId = Convert.ToInt32(await _databaseService.ExecuteScalarAsync(memberQuery, memberParams));

            var accountQuery = @"
INSERT INTO member_accounts 
(member_id, membership_tier, status, expiration_date) 
VALUES (@memberId, 'Basic', 'Active', @expirationDate)";

            var accountParams = new Dictionary<string, object>
            {
                { "@memberId", memberId },
                { "@expirationDate", DateTime.Now.AddYears(1) }
            };

            await _databaseService.ExecuteNonQueryAsync(accountQuery, accountParams);

            return true;
        }
        catch (Exception ex)
        {
            Trace.TraceError("Registration error: {0}", ex);
            return false;
        }
    }

    public async Task<Member> GetCurrentUserAsync()
    {
        try
        {
            if (!_isAuthenticated || string.IsNullOrEmpty(_currentUserEmail))
                return null;

            var query = @"
SELECT member_id AS MemberId,
       first_name AS FirstName,
       last_name AS LastName,
       dateofbirth AS DateOfBirth,
       email AS Email,
       phone AS Phone,
       registered_date AS RegisteredDate,
       created_at AS CreatedAt,
       role AS Role
FROM members
WHERE email = @email
LIMIT 1;";

            var parameters = new Dictionary<string, object> { { "@email", _currentUserEmail } };
            return await _databaseService.QuerySingleAsync<Member>(query, parameters);
        }
        catch (Exception ex)
        {
            Trace.TraceError("Get current user error: {0}", ex);
            return null;
        }
    }

    public async Task<bool> ChangePasswordAsync(string email, string oldPassword, string newPassword)
    {
        var oldHash = HashPassword(oldPassword);
        var newHash = HashPassword(newPassword);

        var checkQuery = @"
SELECT COUNT(*)
FROM members m
JOIN member_accounts a ON a.member_id = m.member_id
WHERE m.email = @email
AND m.password = @oldHash";

        var checkParams = new Dictionary<string, object>
        {
            { "@email", email },
            { "@oldHash", oldHash }
        };

        var count = Convert.ToInt32(await _databaseService.ExecuteScalarAsync(checkQuery, checkParams));

        if (count == 0) return false;

        var updateQuery = @"
UPDATE members
SET password = @newHash
WHERE email = @email";

        var updateParams = new Dictionary<string, object>
        {
            { "@newHash", newHash },
            { "@email", email }
        };

        await _databaseService.ExecuteNonQueryAsync(updateQuery, updateParams);

        return true;
    }

    public async Task<bool> LogoutAsync()
    {
        _isAuthenticated = false;
        _currentUserEmail = string.Empty;
        _currentUserRole = "Member";
        _currentUserId = 0;

        SecureStorage.Remove("is_authenticated");
        SecureStorage.Remove("user_email");


        await UserSession.Instance.EndSessionAsync();

        return true;
    }

    private string HashPassword(string password)
    {
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(password);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }
}

public class UserSession
{
    // Thread-safe lazy singleton
    private static readonly Lazy<UserSession> _instance = new(() => new UserSession());
    public static UserSession Instance => _instance.Value;

    private UserSession() { }

    public bool IsAuthenticated { get; private set; } = false;
    public string? UserEmail { get; private set; }
    public string? UserRole { get; private set; }
    public int? UserId { get; private set; }

    // Helper that safely reads SecureStorage keys and logs errors.
    private async Task<string?> TryGetSecureAsync(string key)
    {
        try
        {
            return await SecureStorage.GetAsync(key);
        }
        catch
        {
            return null; // prevent crash
        }
    }


    public async Task StartSessionAsync(string email, string role, int userId)
    {
        UserEmail = email;
        UserRole = role;
        UserId = userId;
        IsAuthenticated = true;

        try
        {
            // Wrap each call to reduce blast radius and ensure we don't throw.
            try { await SecureStorage.SetAsync("is_authenticated", "true"); } catch (Exception ex) { Trace.TraceWarning("Set is_authenticated failed: {0}", ex.Message); }
            try { await SecureStorage.SetAsync("user_email", email); } catch (Exception ex) { Trace.TraceWarning("Set user_email failed: {0}", ex.Message); }
            try { await SecureStorage.SetAsync("user_role", role); } catch (Exception ex) { Trace.TraceWarning("Set user_role failed: {0}", ex.Message); }
            try { await SecureStorage.SetAsync("user_id", userId.ToString()); } catch (Exception ex) { Trace.TraceWarning("Set user_id failed: {0}", ex.Message); }
        }
        catch (Exception ex)
        {
            // Defensive: should not bubble out
            Trace.TraceError("StartSessionAsync unexpected error: {0}", ex);
        }
    }

    public async Task EndSessionAsync()
    {
        UserEmail = null;
        UserRole = null;
        UserId = null;
        IsAuthenticated = false;

        try
        {
            try { SecureStorage.Remove("is_authenticated"); } catch (Exception ex) { Trace.TraceWarning("Remove is_authenticated failed: {0}", ex.Message); }
            try { SecureStorage.Remove("user_email"); } catch (Exception ex) { Trace.TraceWarning("Remove user_email failed: {0}", ex.Message); }
            try { SecureStorage.Remove("user_role"); } catch (Exception ex) { Trace.TraceWarning("Remove user_role failed: {0}", ex.Message); }
            try { SecureStorage.Remove("user_id"); } catch (Exception ex) { Trace.TraceWarning("Remove user_id failed: {0}", ex.Message); }
        }
        catch (Exception ex)
        {
            Trace.TraceError("EndSessionAsync unexpected error: {0}", ex);
        }

        await Task.CompletedTask;
    }
    private void ClearSession()
    {
        IsAuthenticated = false;
        UserEmail = null;
        UserRole = null;
        UserId = 0;

    }



     public async Task LoadSessionAsync()
    {
        var auth = await SecureStorage.GetAsync("is_authenticated");
        var email = await SecureStorage.GetAsync("user_email");
        var role = await SecureStorage.GetAsync("user_role");
        var userIdStr = await SecureStorage.GetAsync("user_id");

        if (!string.IsNullOrEmpty(auth) && auth == "true" && !string.IsNullOrEmpty(email))
        {
            IsAuthenticated = true;
            UserEmail = email;
            UserRole = role ?? "Member";

            if (int.TryParse(userIdStr, out int userId))
            {
                UserId = userId;
            }
        }
        else
        {
            IsAuthenticated = false;
            UserEmail = null;
            UserRole = null;
            UserId = null;
        }
    }
}
