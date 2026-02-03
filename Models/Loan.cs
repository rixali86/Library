using Library.Models;
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

        // Navigation properties
        public BookCopy? Copy { get; set; }
        public Member? Member { get; set; }
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