using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WhoAmIBotSpace.Classes
{
    public class GameFinishedEventArgs : EventArgs
    {
        public NodeGame Game;
        public GameFinishedEventArgs(NodeGame game)
        {
            Game = game;
        }
    }
}
