using System;

namespace Library.Models
{
    public class Loan
    {
        public int LoanId { get; set; }
        public int CopyId { get; set; }
        public int MemberId { get; set; }
        public DateTime CheckoutDatetime { get; set; }
        public DateTime DueDatetime { get; set; }
        public DateTime? ReturnDatetime { get; set; }
        public string Status { get; set; } = "Checked Out"; // Checked Out, Returned, Overdue

        // Navigation properties (not mapped to database)
        public string BookTitle { get; set; } = string.Empty;
        public string Barcode { get; set; } = string.Empty;
        public string MemberFirstName { get; set; } = string.Empty;
        public string MemberLastName { get; set; } = string.Empty;
        public string MemberFullName => $"{MemberFirstName} {MemberLastName}".Trim();
        public string BranchName { get; set; } = string.Empty;

        // Calculated properties
        public bool IsOverdue => Status == "Checked Out" && DueDatetime < DateTime.Now;
        public int DaysOverdue => IsOverdue ? (DateTime.Now - DueDatetime).Days : 0;
        public decimal FineAmount => DaysOverdue * 0.50m; // $0.50 per day
    }

    public class Penalty
    {
        public int PenaltyId { get; set; }
        public int LoanId { get; set; }
        public int MemberId { get; set; }
        public decimal PenaltyAmount { get; set; }
        public string Reason { get; set; } = string.Empty;
        public bool Resolved { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}