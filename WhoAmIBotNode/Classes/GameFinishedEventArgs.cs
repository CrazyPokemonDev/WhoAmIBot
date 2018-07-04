using System;

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
