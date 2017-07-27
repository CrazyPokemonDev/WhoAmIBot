﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
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
        public Dictionary<long, string> RoleIdDict { get; } = new Dictionary<long, string>();
        public Thread Thread { get; set; }
        public Player Winner { get; set; }
        public Game(int id, long groupId, string groupName)
        {
            Id = id;
            GroupId = groupId;
            GroupName = groupName;
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
    }

    public enum GameState
    {
        Joining,
        Running,
        Ended
    }
}