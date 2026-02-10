using Library.Models;
using Library.Services;
using System.Security.Cryptography;
using System.Text;

namespace Library.Services
{
    public interface IAuthService
    {
        Task<bool> LoginAsync(string email, string password);
        Task<bool> RegisterAsync(string email, string password, Member memberInfo);
        Task<bool> ChangePasswordAsync(string email, string oldPassword, string newPassword);
        Task<bool> LogoutAsync();
        bool IsAuthenticated { get; }
        string CurrentUserEmail { get; }


    }

    public class AuthService : IAuthService
    {
        private readonly IDatabaseService _databaseService;
        private bool _isAuthenticated = false;
        private string _currentUserEmail = string.Empty;

        public AuthService(IDatabaseService databaseService)
        {
            _databaseService = databaseService;
        }

        public bool IsAuthenticated => _isAuthenticated;
        public string CurrentUserEmail => _currentUserEmail;


        public async Task<bool> LoginAsync(string email, string password)
        {
            try
            {
                // Hash the password for comparison
                var hashedPassword = HashPassword(password);

                var query = @"
SELECT COUNT(*) 
FROM members m
JOIN member_accounts a ON a.member_id = m.member_id
WHERE m.email = @email
AND m.password = @hashedPassword
AND a.status = 'Active'";

                var parameters = new Dictionary<string, object>
        {
            { "@email", email },
            { "@hashedPassword", hashedPassword }
        };

                var result = await _databaseService.ExecuteScalarAsync(query, parameters);
                var count = Convert.ToInt32(result);

                if (count > 0)
                {
                    _isAuthenticated = true;
                    _currentUserEmail = email;

                    await SecureStorage.SetAsync("is_authenticated", "true");
                    await SecureStorage.SetAsync("user_email", email);

                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Login error: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> RegisterAsync(string email, string password, Member memberInfo)
        {
            try
            {
                // Normalize email
                email = email.Trim().ToLower();

                // Check if email exists
                var checkQuery = "SELECT COUNT(*) FROM members WHERE LOWER(email) = @email";
                var checkParams = new Dictionary<string, object> { { "@email", email } };
                var existingCount = Convert.ToInt32(await _databaseService.ExecuteScalarAsync(checkQuery, checkParams));

                if (existingCount > 0)
                    return false; // Email already exists

                var hashedPassword = HashPassword(password);

                // Insert member and get last inserted ID
                var memberQuery = @"
INSERT INTO members 
(first_name, last_name, dateofbirth, email, phone, registered_date, password) 
VALUES (@firstName, @lastName, @dob, @email, @phone, @registeredDate, @password);
SELECT LAST_INSERT_ID();";

                var memberParams = new Dictionary<string, object>
        {
            { "@firstName", memberInfo.FirstName },
            { "@lastName", memberInfo.LastName },
            { "@dob", memberInfo.DateOfBirth ?? (object)DBNull.Value },
            { "@email", email },
            { "@phone", memberInfo.Phone },
            { "@registeredDate", DateTime.Now },
            { "@password", hashedPassword }
        };

                var memberId = Convert.ToInt32(await _databaseService.ExecuteScalarAsync(memberQuery, memberParams));

                // Create member account
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
                Console.WriteLine($"Registration error: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> ChangePasswordAsync(
    string email,
    string oldPassword,
    string newPassword)
        {
            var oldHash = HashPassword(oldPassword);
            var newHash = HashPassword(newPassword);

            // Verify old password
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

            var count = Convert.ToInt32(
                await _databaseService.ExecuteScalarAsync(checkQuery, checkParams));

            if (count == 0)
                return false;

            // Update password
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

            SecureStorage.Remove("is_authenticated");
            SecureStorage.Remove("user_email");

            return await Task.FromResult(true);
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
        private static UserSession _instance;
        public static UserSession Instance => _instance ??= new UserSession();

        private UserSession() { }

        public bool IsAuthenticated { get; private set; } = false;
        public string? UserEmail { get; private set; }

        // Start session in memory and optionally store persistently
        public async Task StartSessionAsync(string email)
        {
            UserEmail = email;
            IsAuthenticated = true;

            await SecureStorage.SetAsync("is_authenticated", "true");
            await SecureStorage.SetAsync("user_email", email);
        }

        // End session
        public async Task EndSessionAsync()
        {
            UserEmail = null;
            IsAuthenticated = false;

            SecureStorage.Remove("is_authenticated");
            SecureStorage.Remove("user_email");
        }

        // Load session from SecureStorage (on app start)
        public async Task LoadSessionAsync()
        {
            var auth = await SecureStorage.GetAsync("is_authenticated");
            var email = await SecureStorage.GetAsync("user_email");

            if (!string.IsNullOrEmpty(auth) && auth == "true" && !string.IsNullOrEmpty(email))
            {
                IsAuthenticated = true;
                UserEmail = email;
            }
            else
            {
                IsAuthenticated = false;
                UserEmail = null;
            }
        }
    }
}