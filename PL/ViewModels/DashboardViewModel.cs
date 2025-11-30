namespace DAL.models
{
    public class DashboardViewModel
    {
        public int TotalAirlines { get; set; }
        public int TotalAirplanes { get; set; }
        public int TotalFlights { get; set; }
        public int TotalUsers { get; set; }
        public int TotalBookings { get; set; }
        public decimal TotalRevenue { get; set; }

        public IEnumerable<Flight> RecentFlights { get; set; } 
        public IEnumerable<Booking> RecentBookings { get; set; }

        public Dictionary<string, int> FlightsByAirline { get; set; } = new();

        public List<int> MonthlyFlights { get; set; } = new();
        public List<string> MonthlyLabels { get; set; } = new();

        public DateTime DashboardDate => DateTime.Now;
    }
}
