using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WhoAmIBotSpace.Classes
{
    public class GameFinishedEventArgs : EventArgs
    {
        public Game Game;
        public GameFinishedEventArgs(Game game)
        {
            Game = game;
        }
    }
}
