using System;

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
        public string ConfirmPassword { get; internal set; }
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