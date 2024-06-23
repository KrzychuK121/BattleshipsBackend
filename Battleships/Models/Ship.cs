namespace Battleships.Models
{
    public class Ship
    {
        public string Name { get; set; } = string.Empty;
        public List<string> BoardFields { get; set; } = new List<string>();

        public static List<Ship> CopyShips(List<Ship> toCopy)
        {
            var copy = new List<Ship>();

            foreach (var ship in toCopy)
            {
                Ship copyOfShip = new Ship(){ 
                    Name = ship.Name,
                    BoardFields = new List<string>()
                };
                
                foreach(var field in ship.BoardFields)
                {
                    copyOfShip.BoardFields.Add(field);
                }

                copy.Add(copyOfShip);
            }
            

            return copy;
        }
    }
}
