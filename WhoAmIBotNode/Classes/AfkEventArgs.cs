using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WhoAmIBotSpace.Classes
{
    public class AfkEventArgs
    {
        public NodeGame Game { get; set; }
        public NodePlayer Player { get; set; }
        public AfkEventArgs(NodeGame game, NodePlayer player)
        {
            Game = game;
            Player = player;
        }
    }
}
