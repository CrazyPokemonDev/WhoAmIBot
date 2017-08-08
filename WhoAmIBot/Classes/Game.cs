using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot.Types;
using WhoAmIBotSpace.Helpers;

namespace WhoAmIBotSpace.Classes
{
    public class Game
    {
        public int Id { get; }
        public long GroupId { get; }
        public string GroupName { get; }
        public List<Player> Players { get; } = new List<Player>();
        public List<Player> TotalPlayers { get; set; } = new List<Player>();
        public GameState State { get; set; } = GameState.Joining;
        public Dictionary<long, string> RoleIdDict { get; } = new Dictionary<long, string>();
        public Thread Thread { get; set; }
        public Player Winner { get; set; }
        public Message PlayerlistMessage { get; set; }
        public DateTime LastAction { get; set; }
        public Game(int id, long groupId, string groupName)
        {
            Id = id;
            GroupId = groupId;
            GroupName = groupName;
            LastAction = DateTime.Now;
        }

        public bool DictFull()
        {
            foreach (Player p in Players)
            {
                if (!RoleIdDict.ContainsKey(p.Id)) return false;
            }
            return true;
        }

        public void TrySetWinner(Player p)
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

        public string GetPlayerList() => "\n" + string.Join("\n", Help.Longer(Players, TotalPlayers).Select(x => x.Name));
    }

    public enum GameState
    {
        Joining,
        Running,
        Ended
    }
}