namespace Battleships.Models
{
    public class Player
    {
        public string ConnectionId { get; set; } = string.Empty;
        public string Nickname { get; set; } = string.Empty;
        public bool IsReady { get; set; } = false;
        public List<Ship> Ships { get; set; } = new List<Ship>(4);
    }
}
