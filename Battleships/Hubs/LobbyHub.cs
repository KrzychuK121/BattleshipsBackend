using Battleships.Models;
using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Text.RegularExpressions;

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

                await Clients.Client(Context.ConnectionId).SendAsync("JoinSuccessHandler");

                await Groups.AddToGroupAsync(
                    Context.ConnectionId,
                    connectingPlayer.ChatConnection
                );

                Console.WriteLine(
                    $"User {currentPlayer.Nickname} sends his nickname and status " +
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

            } finally {
                _semaphoreSlim.Release();
            }
        }

        public async Task LeaveSpecificLobby()
        {
            await _semaphoreSlim.WaitAsync();

            try {
                GroupInfo? group = GetGroupBy(Context.ConnectionId);
                Player? sender = GetPlayerBy(Context.ConnectionId);

                if (group == null || sender == null)
                    return;

                // When last player in group just remove the group
                if (group.MemberCount == 1) {
                    _connectionGroups.Remove(group);
                    Console.WriteLine($"Group {group.GroupName} was removed from server because all players left it.");
                } else {
                    // When not last player, remove him from group
                    group.PlayersShips.Remove(sender.ConnectionId);
                    group.MemberCount--;
                    group.Players.Remove(sender);

                    Console.WriteLine($"Player {sender.Nickname} was removed from {group.GroupName} lobby.");
                }

                await Groups.RemoveFromGroupAsync(sender.ConnectionId, group.GroupName);

            } finally {
                _semaphoreSlim.Release();
            }
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

        private bool isGameStarting(GroupInfo? group)
        {
            return group.Players.All(player => player.IsReady) && group.MemberCount == 2;
        }

        public async Task SetReady(List<Ship> ships)
        {
            await _semaphoreSlim.WaitAsync();
            try {
                Player? currentPlayer = GetPlayerBy(Context.ConnectionId);
                Player? opponent = GetOpponentBy(Context.ConnectionId);
                GroupInfo? group = GetGroupBy(Context.ConnectionId);

                if (currentPlayer == null || group == null)
                    return;

                currentPlayer.IsReady = true;
                currentPlayer.Ships = ships;
                if(group.PlayerToMove == null)
                    group.PlayerToMove = currentPlayer;

                group.PlayersShips.Add(Context.ConnectionId, Ship.CopyShips(ships));

                Console.WriteLine(
                    $"User {currentPlayer.Nickname} set his ready status to {currentPlayer.IsReady}" +
                    (
                        opponent != null
                        ? $" and informs {opponent.Nickname} about it."
                        : "."
                    )
                );

                if (opponent != null)
                    await SendStatusInfo(
                        "UpdateOpponentsStatus",
                        opponent,
                        currentPlayer
                    );

                if (isGameStarting(group))
                {
                    bool isSendersTurn = group.PlayerToMove.ConnectionId.Equals(opponent.ConnectionId);
                    await Clients.Client(opponent.ConnectionId)
                    .SendAsync(
                        "GetWhosFirstAndGameStatus",
                        true,
                        isSendersTurn
                    );

                    Console.WriteLine($"SetReady sent to {opponent.Nickname}");
                }

            } finally {
                _semaphoreSlim.Release();
            }
        }

        public async Task CheckOpponentsStatus()
        {
            await _semaphoreSlim.WaitAsync();
            try
            {
                Player? currentPlayer = GetPlayerBy(Context.ConnectionId);
                Player? opponent = GetOpponentBy(Context.ConnectionId);

                if (currentPlayer == null)
                    return;

                Console.WriteLine(
                    opponent != null
                    ? $"User {currentPlayer.Nickname} checks {opponent.Nickname} ready status."
                    : $"User {currentPlayer.Nickname} tried to check opponent ready status but there is no opponent yet."
                );

                if (opponent != null)
                    await SendStatusInfo(
                        "UpdateOpponentsStatus",
                        currentPlayer,
                        opponent
                    );

            }
            finally
            {
                _semaphoreSlim.Release();
            }
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

        public async Task CheckGameStatus()
        {
            await _semaphoreSlim.WaitAsync();
            try {
                bool isSendersTurn = false;
                GroupInfo? group = GetGroupBy(Context.ConnectionId);

                if (group == null)
                    return;

                isSendersTurn = group.PlayerToMove.ConnectionId.Equals(Context.ConnectionId);

                await Clients.Client(Context.ConnectionId)
                .SendAsync(
                    "GetWhosFirstAndGameStatus",
                    isGameStarting(group),
                    isSendersTurn
                );
            } finally {
                _semaphoreSlim.Release();
            }            
        }

        public async Task MakeMove(string cellId)
        {
            await _semaphoreSlim.WaitAsync();
            try {
                Player? sender = GetPlayerBy(Context.ConnectionId);
                Player? opponent = GetOpponentBy(Context.ConnectionId);
                Player? winner = null;
                GroupInfo? group = GetGroupBy(Context.ConnectionId);
                var isHitted = false;
                List<string> eliminatedShipFields = null;

                if (sender == null || opponent == null || group == null)
                    return;

                if (!sender.ConnectionId.Equals(group.PlayerToMove.ConnectionId))
                {
                    Console.WriteLine($"The {sender.Nickname} is trying to make move but it is not his turn.");
                    return;
                }

                if (
                    opponent.Ships.Any(
                        ship => ship.BoardFields.Any(
                            field => field.Equals(cellId)
                        )
                    )
                ) {
                    isHitted = true;

                    Ship hittedShip = group.PlayersShips[opponent.ConnectionId].First(
                        ship => ship.BoardFields.Any(
                            field => field.Equals(cellId)
                        )
                    );

                    hittedShip.BoardFields.Remove(cellId);

                    // Checking if the whole ship is sunken. If yes, sending info about it instead of single hitted field
                    if (hittedShip.BoardFields.Count() == 0) {
                        eliminatedShipFields = opponent.Ships.First(
                                ship => ship.Name.Equals(hittedShip.Name)
                            ).BoardFields;
                        group.PlayersShips[opponent.ConnectionId].Remove(hittedShip);
                    }
                        
                }

                // Check after hit if the player won
                if (group.PlayersShips[opponent.ConnectionId].Count() == 0)
                    winner = sender;

                if (eliminatedShipFields == null)
                    await Clients.Group(group.GroupName).SendAsync(
                        "PlayerShotted",
                        Context.ConnectionId,
                        cellId,
                        isHitted
                    );
                else
                    await Clients.Group(group.GroupName).SendAsync(
                        "PlayerSunkenShip",
                        Context.ConnectionId,
                        eliminatedShipFields
                    );


                if (winner != null)
                    await Clients.Group(group.GroupName).SendAsync(
                        "PlayerWon",
                        winner.ConnectionId,
                        winner.Nickname
                    );

                group.PlayerToMove = opponent;

            } finally {
                _semaphoreSlim.Release();
            }
        }
    }
}
