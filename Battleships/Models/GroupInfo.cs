﻿namespace Battleships.Models
{
    public class GroupInfo
    {
        public string GroupName { get; set; } = string.Empty;
        public int MemberCount { get; set; } = 0;
        public List<Player> Players { get; set; } = new List<Player>();
    }
}
