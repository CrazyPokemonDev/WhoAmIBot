using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using TelegramBotApi.Types;
using WhoAmIBotSpace.Helpers;

namespace WhoAmIBotSpace.Classes
{
    public class NodeGame
    {
        public long Id { get; }
        public long GroupId { get; }
        public string GroupName { get; }
        public List<NodePlayer> Players { get; } = new List<NodePlayer>();
        public List<NodePlayer> TotalPlayers { get; set; } = new List<NodePlayer>();
        public GameState State { get; set; } = GameState.Joining;
        public Dictionary<long, string> RoleIdDict { get; } = new Dictionary<long, string>();
        public Thread Thread { get; set; }
        public NodePlayer Winner { get; set; }
        public Message PlayerlistMessage { get; set; }
        public Timer InactivityTimer { get; set; }
        public NodePlayer Turn { get; set; }
        public NodeGroup Group { get; set; }
        public NodeGame(long id, long groupId, string groupName, NodeGroup group)
        {
            Id = id;
            GroupId = groupId;
            GroupName = groupName;
            Group = group;
        }

        public bool DictFull()
        {
            foreach (NodePlayer p in Players)
            {
                if (!RoleIdDict.ContainsKey(p.Id)) return false;
            }
            return true;
        }

        public void TrySetWinner(NodePlayer p)
        {
            if (Winner == null) Winner = p;
        }

        public string GetRolesAsString()
        {
            string s = "";
            foreach (var kvp in RoleIdDict)
            {
                s += $"\n<b>{WebUtility.HtmlEncode(TotalPlayers.Find(x => x.Id == kvp.Key).Name)}</b>: " +
                    $"<i>{WebUtility.HtmlEncode(kvp.Value)}</i>";
            }
            return s;
        }

        public string GetPlayerList() => 
            "\n" + string.Join("\n", Help.Longer(Players, TotalPlayers).Select(x => x.Name + " " + 
            (Players.Any(y => y.Id == x.Id) ? "<code>Ingame</code>" : (x.GaveUp ? "<code>Gave up</code>" : "<code>Finished</code>"))));
    }

    public enum GameState
    {
        Joining,
        Running,
        Ended
    }
}