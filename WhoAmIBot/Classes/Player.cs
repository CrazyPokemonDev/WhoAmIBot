using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WhoAmIBotSpace.Classes
{
    public class Player
    {
        public long Id { get; }
        public string Name { get; }
        public Player(long id, string name)
        {
            Id = id;
            Name = name;
        }
    }
}
