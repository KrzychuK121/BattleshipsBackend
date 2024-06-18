using Battleships.Models;
using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;

namespace Battleships.Hubs
{
    public class LobbyHub : Hub
    {
        private static readonly List<GroupInfo> _connectionGroups = new List<GroupInfo>();
        private static readonly SemaphoreSlim _semaphoreSlim = new SemaphoreSlim(1, 1);

        public async Task JoinSpecificLobby(PlayerConn connectingPlayer)
        {
            await _semaphoreSlim.WaitAsync();
            try { 
                var group = _connectionGroups.FirstOrDefault(
                    gi => gi.GroupName.Equals(connectingPlayer.ChatConnection)
                );

                if (group == null)
                {
                    group = new GroupInfo { GroupName = connectingPlayer.ChatConnection };
                    _connectionGroups.Add(group);
                    Console.WriteLine("Not found GroupInfo. Creating new one");
                }


                if (group.MemberCount == 2)
                {
                    Console.WriteLine($"There are already 2 players in '{connectingPlayer.ChatConnection}' lobby");
                    await Clients.Client(Context.ConnectionId).SendAsync(
                        "JoinErrorHandler",
                        $"W {connectingPlayer.ChatConnection} znajduje się już dwóch graczy."
                    );

                    return;
                }

                if (
                    _connectionGroups.Any(
                        group => group.Players.Any(
                            player => player.Nickname.Equals(connectingPlayer.Username)
                        )
                    )
                )
                {
                    Console.WriteLine("There is player with the same nickname.");
                    await Clients.Client(Context.ConnectionId).SendAsync(
                        "JoinErrorHandler",
                        $"Gracz o nicku ({connectingPlayer.Username}) już istnieje."
                    );

                    return;
                }


                group.Players.Add(
                    new Player {
                        ConnectionId = Context.ConnectionId,
                        Nickname = connectingPlayer.Username
                    }
                );

                group.MemberCount++;
            } finally {
                _semaphoreSlim.Release();
            }

            await Clients.Client(Context.ConnectionId).SendAsync("JoinSuccessHandler");

            await Groups.AddToGroupAsync(
                Context.ConnectionId,
                connectingPlayer.ChatConnection
            );

            await Clients.Group(connectingPlayer.ChatConnection)
            .SendAsync(
                "JoinSpecificLobby",
                "admin",
                $"user {connectingPlayer.Username} has joined with Context.ConnectionId {Context.ConnectionId}."
            );
            
        }

        public async Task GetReadyStatus()
        {
            //Clients.Client("").
        }
    }
}
