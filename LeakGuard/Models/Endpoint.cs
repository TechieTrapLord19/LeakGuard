namespace LeakGuard.Models
{
    public class Endpoint
    {
        public int EndpointID { get; set; }
        public string Hostname { get; set; } = null!;
        public string? IPAddress { get; set; }
    }
}
