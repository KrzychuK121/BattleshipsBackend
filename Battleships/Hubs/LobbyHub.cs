using Battleships.Models;
using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;

namespace Battleships.Hubs
{
    public class LobbyHub : Hub
    {
        public async Task JoinSpecificLobby(Player connectingPlayer)
        {
            await Groups.AddToGroupAsync(
                Context.ConnectionId, 
                connectingPlayer.ChatConnection
            );

            await Clients.Group(connectingPlayer.ChatConnection)
            .SendAsync(
                "JoinSpecificLobby", 
                "admin", 
                $"user {connectingPlayer.Username} has joined."
            );
        }
    }
}
