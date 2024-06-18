namespace Battleships.Models
{
    public class Ship
    {
        public string Name { get; set; } = string.Empty;
        public List<string> Fields { get; set; } = new List<string>();
    }
}
