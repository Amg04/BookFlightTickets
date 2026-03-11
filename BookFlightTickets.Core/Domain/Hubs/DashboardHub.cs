using BookFlightTickets.Core.Shared.Utility;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace BookFlightTickets.Core.Domain.Hubs
{
    [Authorize(Roles = SD.Admin)]
    public class DashboardHub : Hub
    {
        public override async Task OnConnectedAsync()
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, SD.Admin);
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, SD.Admin);
            await base.OnDisconnectedAsync(exception);
        }
    }
}
