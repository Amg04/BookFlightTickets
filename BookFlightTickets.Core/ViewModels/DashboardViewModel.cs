using BookFlightTickets.Core.Domain.Entities;
namespace BookFlightTickets.Core.ViewModels
{
    public class DashboardViewModel
    {
        public int TotalAirlines { get; set; }
        public int TotalAirplanes { get; set; }
        public int TotalFlights { get; set; }
        public int TotalUsers { get; set; }
        public int TotalBookings { get; set; }
        public decimal TotalRevenue { get; set; }
        public IEnumerable<FlightViewModel> RecentFlights { get; set; } = default!; 
        public IEnumerable<BookingViewModel> RecentBookings { get; set; } = default!;
        public Dictionary<string, int> FlightsByAirline { get; set; } = new();
        public List<int> MonthlyFlights { get; set; } = new();
        public List<string> MonthlyLabels { get; set; } = new();
        public DateTime DashboardDate => DateTime.Now;
    }
}
