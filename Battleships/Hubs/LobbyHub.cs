using Battleships.Models;
using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;
using System.ComponentModel;

namespace Battleships.Hubs
{
    public class LobbyHub : Hub
    {
        private static readonly List<GroupInfo> _connectionGroups = new List<GroupInfo>();
        private static readonly SemaphoreSlim _semaphoreSlim = new SemaphoreSlim(1, 1);

        public async Task JoinSpecificLobby(PlayerConn connectingPlayer)
        {
            await _semaphoreSlim.WaitAsync();

            Player? currentPlayer = null;
            Player? opponent = null;
            
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

                currentPlayer = new Player {
                    ConnectionId = Context.ConnectionId,
                    Nickname = connectingPlayer.Username
                };

                group.Players.Add(
                    currentPlayer
                );

                group.MemberCount++;

                opponent = GetOpponentBy(Context.ConnectionId);
            } finally {
                _semaphoreSlim.Release();
            }

            await Clients.Client(Context.ConnectionId).SendAsync("JoinSuccessHandler");

            await Groups.AddToGroupAsync(
                Context.ConnectionId,
                connectingPlayer.ChatConnection
            );

            Console.WriteLine(
                $"User {currentPlayer.Nickname} sends his nickname and status info " +
                (
                    opponent != null
                    ? $"to {opponent.Nickname}."
                    : "but there's no opponent."
                )
            );

            if (opponent != null)
                await SendStatusInfo(
                    "UpdateOpponentsStatus",
                    opponent,
                    currentPlayer
                );

            await Clients.Group(connectingPlayer.ChatConnection)
            .SendAsync(
                "JoinSpecificLobby",
                "admin",
                $"user {connectingPlayer.Username} has joined with Context.ConnectionId {Context.ConnectionId}."
            );
            
        }

        /**
         * This method sends status about opponent to the receiver.
         * 
         * method - name of method that handles message
         * receiver - the one that reveives the information
         * opponent - info about reveicer's opponent
         * 
         */
        private async Task SendStatusInfo(
            string method,
            Player receiver,
            Player opponent
        )
        {
            await Clients.Client(receiver.ConnectionId)
            .SendAsync(
                method,
                opponent.Nickname,
                opponent.IsReady
            );
        }

        public async Task SetReady(List<Ship> ships)
        {
            await _semaphoreSlim.WaitAsync();

            Player? currentPlayer = null;
            Player? opponent = null;

            try {
                currentPlayer = GetPlayerBy(Context.ConnectionId);
                opponent = GetOpponentBy(Context.ConnectionId);

                currentPlayer.IsReady = true;
                currentPlayer.Ships = ships;

                Console.WriteLine(
                    $"User {currentPlayer.Nickname} set his ready status to {currentPlayer.IsReady}" +
                    (
                        opponent != null
                        ? $" and informs {opponent.Nickname} about it."
                        : "."
                    )
                );

            } finally {
                _semaphoreSlim.Release();
            }

            if (opponent != null)
                await SendStatusInfo(
                    "UpdateOpponentsStatus",
                    opponent,
                    currentPlayer
                );

        }

        public async Task CheckOpponentsStatus()
        {
            await _semaphoreSlim.WaitAsync();

            Player? currentPlayer = null;
            Player? opponent = null;

            try
            {
                currentPlayer = GetPlayerBy(Context.ConnectionId);
                opponent = GetOpponentBy(Context.ConnectionId);

                Console.WriteLine(
                    opponent != null
                    ? $"User {currentPlayer.Nickname} checks {opponent.Nickname} ready status."
                    : $"User {currentPlayer.Nickname} tried to check opponent ready status but there is no opponent yet."
                );

            }
            finally
            {
                _semaphoreSlim.Release();
            }

            if (opponent != null)
                await SendStatusInfo(
                    "UpdateOpponentsStatus",
                    currentPlayer,
                    opponent
                );
        }

        private GroupInfo? GetGroupBy(string ConnectionId)
        {
            var group = _connectionGroups.FirstOrDefault(
                group => group.Players.Any(
                    player => player.ConnectionId.Equals(ConnectionId)
                )
            );

            return group;
        }

        /**
         * This method returns player by its ConnectionId.
         * If IfOpponent is true, the function will return opponent of user
         * that his connection id is equals to ConnectionId. Otherwise the 
         * owner of ConnectionId is returned (when IfOpponent is true)
         * 
         * ConnectionId - id of player that you want to get
         * IfOpponent - decides if the player or his opponent should be returned
         */
        private Player? GetPlayerBy(string ConnectionId, bool IfOpponent = false)
        {
            var group = GetGroupBy(ConnectionId);
            var player = group != null
            ? group.Players.FirstOrDefault(
                player => (
                    IfOpponent
                    ? !player.ConnectionId.Equals(ConnectionId)
                    : player.ConnectionId.Equals(ConnectionId)
                )
            )
            : null;

            return player;
        }

        private Player? GetOpponentBy(string ConnectionId)
        {
            return GetPlayerBy(ConnectionId, true);
        }
    }
}
