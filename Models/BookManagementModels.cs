using System;
using System.Collections.Generic;

namespace Library.Models
{
    public class BookWithCopies
    {
        public Title Book { get; set; } = new();
        public List<BookCopy> Copies { get; set; } = new();
        public int TotalCopies => Copies.Count;
        public int AvailableCopies => Copies.Count(c => IsCopyAvailable(c.CopyId));

        private bool IsCopyAvailable(int copyId)
        {
            // This will be populated from service
            return true; // Placeholder
        }
    }

    public class MemberWithLoans
    {
        public Member Member { get; set; } = new();
        public List<Loan> CurrentLoans { get; set; } = new();
        public List<Penalty> PendingFines { get; set; } = new();
        public int TotalLoans => CurrentLoans.Count;
        public decimal TotalFines => GetTotalFines();

        private decimal GetTotalFines()
        {
            decimal total = 0;
            foreach (var fine in PendingFines)
            {
                if (!fine.Resolved)
                    total += fine.PenaltyAmount;
            }
            return total;
        }
    }

    public class DashboardStats
    {
        public int TotalMembers { get; set; }
        public int ActiveLoans { get; set; }
        public int OverdueLoans { get; set; }
        public int AvailableBooks { get; set; }
        public decimal TotalFinesCollected { get; set; }
        public List<Loan> RecentLoans { get; set; } = new();
        public List<Member> RecentMembers { get; set; } = new();
    }
}