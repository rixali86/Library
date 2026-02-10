using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Library.Services;
using Library.Models;
using Microsoft.Maui.Controls;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Library.ViewModels;

public partial class MemberProfileViewModel : ObservableObject
{
    private readonly IDatabaseService _databaseService;
    private readonly IAuthService _authService;

    [ObservableProperty]
    private int memberId;

    [ObservableProperty]
    private string firstName = string.Empty;

    [ObservableProperty]
    private string lastName = string.Empty;

    [ObservableProperty]
    private DateTime? dateOfBirth;

    [ObservableProperty]
    private string email = string.Empty;

    [ObservableProperty]
    private string phone = string.Empty;

    [ObservableProperty]
    private DateTime? registeredDate;

    [ObservableProperty]
    private DateTime? createdAt;

    [ObservableProperty]
    private string password = string.Empty;

    [ObservableProperty]
    private string confirmPassword = string.Empty;

    [ObservableProperty]
    private bool isLoading = false;

    [ObservableProperty]
    private string errorMessage = string.Empty;

    public MemberProfileViewModel(IDatabaseService databaseService, IAuthService authService)
    {
        _databaseService = databaseService;
        _authService = authService;
    }

    [RelayCommand]
    public async Task LoadMemberAsync()
    {
        IsLoading = true;
        try
        {
            var userEmail = _authService.CurrentUserEmail;
            if (string.IsNullOrWhiteSpace(userEmail))
            {
                ErrorMessage = "No logged-in user.";
                return;
            }

            var sql = @"
SELECT member_id AS MemberId,
       first_name AS FirstName,
       last_name AS LastName,
       dateofbirth AS DateOfBirth,
       email AS Email,
       phone AS Phone,
       registered_date AS RegisteredDate,
       created_at AS CreatedAt
FROM members
WHERE email = @email
LIMIT 1;";

            var parameters = new Dictionary<string, object> { { "@email", userEmail } };

            var member = await _databaseService.QuerySingleAsync<Member>(sql, parameters);

            if (member != null)
            {
                MemberId = member.MemberId;
                FirstName = member.FirstName;
                LastName = member.LastName;
                DateOfBirth = member.DateOfBirth;
                Email = member.Email;
                Phone = member.Phone;
                RegisteredDate = member.RegisteredDate == default ? null : member.RegisteredDate;
                CreatedAt = member.CreatedAt == default ? null : member.CreatedAt;
            }
            else
            {
                ErrorMessage = "Member not found.";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Load error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task SaveAsync()
    {
        IsLoading = true;
        try
        {
            // Basic validation
            if (string.IsNullOrWhiteSpace(FirstName) || string.IsNullOrWhiteSpace(LastName))
            {
                await Application.Current.MainPage.DisplayAlert("Validation", "First and last name are required.", "OK");
                return;
            }

            if (!string.IsNullOrWhiteSpace(Password) || !string.IsNullOrWhiteSpace(ConfirmPassword))
            {
                if (Password != ConfirmPassword)
                {
                    await Application.Current.MainPage.DisplayAlert("Validation", "Passwords do not match.", "OK");
                    return;
                }

                if (Password.Length < 8)
                {
                    await Application.Current.MainPage.DisplayAlert("Validation", "Password must be at least 8 characters.", "OK");
                    return;
                }
            }

            // Build update SQL. Only set password if provided.
            string sql;
            var parameters = new Dictionary<string, object>
            {
                { "@firstName", FirstName.Trim() },
                { "@lastName", LastName.Trim() },
                { "@dateOfBirth", (object?)DateOfBirth ?? DBNull.Value },
                { "@phone", string.IsNullOrWhiteSpace(Phone) ? DBNull.Value : (object)Phone.Trim() },
                { "@memberId", MemberId }
            };

            if (!string.IsNullOrWhiteSpace(Password))
            {
                // NOTE: In production you must hash the password before storing.
                sql = @"
UPDATE members
SET first_name = @firstName,
    last_name = @lastName,
    dateofbirth = @dateOfBirth,
    phone = @phone,
    password = @password
WHERE member_id = @memberId;";
                parameters.Add("@password", Password);
            }
            else
            {
                sql = @"
UPDATE members
SET first_name = @firstName,
    last_name = @lastName,
    dateofbirth = @dateOfBirth,
    phone = @phone
WHERE member_id = @memberId;";
            }

            var rows = await _databaseService.ExecuteNonQueryAsync(sql, parameters);

            if (rows > 0)
            {
                await Application.Current.MainPage.DisplayAlert("Success", "Profile updated.", "OK");
                // Reload to reflect any DB-side changes
                await LoadMemberAsync();
            }
            else
            {
                await Application.Current.MainPage.DisplayAlert("Info", "No changes were saved.", "OK");
            }
        }
        catch (Exception ex)
        {
            await Application.Current.MainPage.DisplayAlert("Error", $"Save failed: {ex.Message}", "OK");
        }
        finally
        {
            IsLoading = false;
        }
    }
}