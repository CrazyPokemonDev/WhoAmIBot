using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WhoAmIBotSpace.Classes
{
    public class NodePlayer
    {
        public long Id { get; }
        public string Name { get; }
        public bool GaveUp { get; set; } = false;
        public NodePlayer(long id, string name)
        {
            Id = id;
            Name = name;
        }
    }
}
