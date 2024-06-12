using Battleships.Models;
using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;

namespace Battleships.Hubs
{
    public class LobbyHub : Hub
    {
        public async Task JoinSpecificChatRoom(Player connectingPlayer)
        {
            await Groups.AddToGroupAsync(
                Context.ConnectionId, 
                connectingPlayer.ChatConnection
            );
        }
    }
}
