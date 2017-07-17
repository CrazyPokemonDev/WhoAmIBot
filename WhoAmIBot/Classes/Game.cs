using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WhoAmIBotSpace.Classes
{
    public class Game
    {
        public int Id { get; }
        public long GroupId { get; }
        public string GroupName { get; }
        public List<Player> Players { get; } = new List<Player>();
        public GameState State { get; set; } = GameState.Joining;
        public Game(int id, long groupId, string groupName)
        {
            Id = id;
            GroupId = groupId;
            GroupName = groupName;
        }
    }

    public enum GameState
    {
        Joining,
        Running,
        Ended
    }
}
