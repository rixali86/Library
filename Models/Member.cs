using System;
using System.Collections.ObjectModel;

namespace Library.Models
{
    public class Member
    {
        public int MemberId { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public DateTime? DateOfBirth { get; set; }
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public DateTime RegisteredDate { get; set; }
        public DateTime CreatedAt { get; set; }
        public string Password { get; set; } = string.Empty;
        public string ConfirmPassword { get; set; } = string.Empty;
        public string Role { get; set; } = "Member"; // Member or Librarian

        public ObservableCollection<MemberMetadata> MetaRows { get; set; } = new();

        public string FullName => $"{FirstName} {LastName}".Trim();
        public string Initials => GetInitials();

        private string GetInitials()
        {
            if (string.IsNullOrWhiteSpace(FirstName) && string.IsNullOrWhiteSpace(LastName))
                return "?";

            string initials = "";
            if (!string.IsNullOrWhiteSpace(FirstName))
                initials += FirstName[0];
            if (!string.IsNullOrWhiteSpace(LastName))
                initials += LastName[0];

            return initials.ToUpper();
        }
    }

    public class MemberAccount
    {
        public int AccountId { get; set; }
        public int MemberId { get; set; }
        public string MembershipTier { get; set; } = string.Empty;
        public string Status { get; set; } = "Active"; // Active, Suspended, Expired, Flagged
        public DateTime? ExpirationDate { get; set; }

    }
}