namespace PL.ViewModels
{
    public class FlightFilterViewModel
    {
        public string? From { get; set; }
        public string? to { get; set; }
        public string? sortBy { get; set; }
        public string? sortDir { get; set; }
        public string? filterstring { get; set; }
        public int? filterbynum { get; set; }
        public string? filterInput { get; set; }
        public string? filterButton { get; set; }
        public int? page { get; set; }
    }
}
