using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace Library.Models
{
    public class Branch
    {
        public int BranchId { get; set; }
        public string BranchName { get; set; } = string.Empty;
        public string BranchType { get; set; } = string.Empty; // 'Library' or 'Community Center'
        public string AddressLine1 { get; set; } = string.Empty;
        public string? AddressLine2 { get; set; }
        public string City { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;
        public string PostalCode { get; set; } = string.Empty;

        [Phone]
        public string Phone { get; set; } = string.Empty;

        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }

        // New: Geolocation (nullable if not known)
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }

        // Navigation-like properties for easier binding in UI
        public List<BranchHours> Hours { get; set; } = new();


        // Computed display address
        public string FullAddress
        {
            get
            {
                var sb = new StringBuilder();
                if (!string.IsNullOrWhiteSpace(AddressLine1)) sb.Append(AddressLine1);
                if (!string.IsNullOrWhiteSpace(AddressLine2))
                {
                    if (sb.Length > 0) sb.Append(", ");
                    sb.Append(AddressLine2);
                }
                if (!string.IsNullOrWhiteSpace(City))
                {
                    if (sb.Length > 0) sb.Append(", ");
                    sb.Append(City);
                }
                if (!string.IsNullOrWhiteSpace(State))
                {
                    if (sb.Length > 0) sb.Append(", ");
                    sb.Append(State);
                }
                if (!string.IsNullOrWhiteSpace(PostalCode))
                {
                    if (sb.Length > 0) sb.Append(" ");
                    sb.Append(PostalCode);
                }
                if (!string.IsNullOrWhiteSpace(Country))
                {
                    if (sb.Length > 0) sb.Append(", ");
                    sb.Append(Country);
                }
                return sb.ToString();
            }
        }

        // Returns a URL usable with platform launcher (opens map app or browser)
        public string GetMapsUrl()
        {
            if (Latitude.HasValue && Longitude.HasValue)
            {
                // Use Google Maps search syntax or generic lat/lon
                return $"https://www.google.com/maps/search/?api=1&query={Latitude.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)},{Longitude.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
            }
            if (!string.IsNullOrWhiteSpace(FullAddress))
            {
                return $"https://www.google.com/maps/search/?api=1&query={Uri.EscapeDataString(FullAddress)}";
            }
            return string.Empty;
        }

        public override string ToString() => $"{BranchName} ({BranchType}) - {FullAddress}";
    }

    public class BranchHours
    {
        public int HourId { get; set; }
        public int BranchId { get; set; }
        public string DayOfWeek { get; set; } = string.Empty; // e.g., "Monday"
        public TimeSpan OpenTime { get; set; }
        public TimeSpan CloseTime { get; set; }

        // Helper to show friendly text
        public string Display => $"{DayOfWeek}: {OpenTime:hh\\:mm} - {CloseTime:hh\\:mm}";

        // Check if open at a local DateTime (assumes DayOfWeek string matches)
        public bool IsOpenAt(DateTime local)
        {
            if (!Enum.TryParse<DayOfWeek>(DayOfWeek, true, out var day)) return false;
            if (local.DayOfWeek != day) return false;
            var t = local.TimeOfDay;
            return t >= OpenTime && t <= CloseTime;
        }
    }

    public class BranchContact
    {
        public int ContactId { get; set; }
        public int BranchId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;

        [Phone]
        public string Phone { get; set; } = string.Empty;

        [EmailAddress]
        public string Email { get; set; } = string.Empty;
    }
}