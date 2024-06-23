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
                group.PlayerToMove = currentPlayer;

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
                $"User {currentPlayer.Nickname} sends his nickname and status hittedShip " +
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

        public async Task LeaveSpecificLobby()
        {

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

            Player? currentPlayer = null;
            Player? opponent = null;
            GroupInfo? group = null;

            try {
                currentPlayer = GetPlayerBy(Context.ConnectionId);
                opponent = GetOpponentBy(Context.ConnectionId);
                group = GetGroupBy(Context.ConnectionId);

                currentPlayer.IsReady = true;
                currentPlayer.Ships = ships;

                group.PlayersShips.Add(Context.ConnectionId, Ship.CopyShips(ships));

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

        }

        public async Task CheckOpponentsStatus()
        {
            await _semaphoreSlim.WaitAsync();

            Player? currentPlayer = null;
            Player? opponent = null;
            GroupInfo? group = null;

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

        public async Task CheckGameStatus()
        {
            bool isGameStarting = false;
            bool isSendersTurn = false;

            GroupInfo? group = null;
            await _semaphoreSlim.WaitAsync();

            try {
                group = GetGroupBy(Context.ConnectionId);

                if (group == null)
                    return;

                isSendersTurn = group.PlayerToMove.ConnectionId.Equals(Context.ConnectionId);
                isGameStarting = group.Players.All(player => player.IsReady) && group.MemberCount == 2;
            } finally {
                _semaphoreSlim.Release();
            }

            await Clients.Client(Context.ConnectionId)
            .SendAsync(
                "GetWhosFirstAndGameStatus",
                isGameStarting,
                isSendersTurn
            );

            Console.WriteLine("CheckGameStatus");
        }

        public async Task MakeMove(string cellId)
        {
            await _semaphoreSlim.WaitAsync();

            var isHitted = false;
            Player? sender = null;
            Player? opponent = null;
            Player? winner = null;
            GroupInfo? group = null;
            List<string> eliminatedShipFields = null;

            try {
                sender = GetPlayerBy(Context.ConnectionId);
                opponent = GetOpponentBy(Context.ConnectionId);
                group = GetGroupBy(Context.ConnectionId);

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

            } finally {
                _semaphoreSlim.Release();
            }

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
        }
    }
}
