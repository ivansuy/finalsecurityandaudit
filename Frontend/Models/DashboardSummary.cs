namespace Frontend.Models
{
    public class DashboardSummary
    {
        public int WindowHours { get; set; }
        public int AuthSuccess { get; set; }
        public int AuthFailed { get; set; }
        public List<EndpointStat> TopEndpoints { get; set; } = new();
    }

    public class EndpointStat
    {
        public string Endpoint { get; set; } = "";
        public int Count { get; set; }
    }
}
