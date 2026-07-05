using Microsoft.AspNetCore.SignalR;

namespace LeakGuard.Hubs
{
    /// <summary>
    /// SignalR hub for pushing real-time incident notifications to connected dashboards.
    /// </summary>
    public class IncidentHub : Hub
    {
        // Clients subscribe automatically on page load.
        // Server pushes "ReceiveIncident" events when the scanner detects a leak.
    }
}
