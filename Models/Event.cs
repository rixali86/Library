using Library.Models;
using System;

namespace Library.Models
{
    public class BranchEvent
    {
        public int EventId { get; set; }
        public int BranchId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime StartDatetime { get; set; }
        public DateTime EndDatetime { get; set; }
        public int MaxCapacity { get; set; }
        public DateTime CreatedAt { get; set; }
        public Branch? Branch { get; set; }
    }

    public class EventRegistration
    {
        public int RegistrationId { get; set; }
        public int EventId { get; set; }
        public int MemberId { get; set; }
        public DateTime RegistrationDatetime { get; set; }
        public string AttendanceStatus { get; set; } = "Registered"; // Registered, Attended, No Show, Cancelled

        public BranchEvent? Event { get; set; }
        public Member? Member { get; set; }
    }
}