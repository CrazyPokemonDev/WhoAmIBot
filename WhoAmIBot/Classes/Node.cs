using System;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WhoAmIBotSpace.Classes
{
    public class Node
    {
        public void Stop()
        {
            //nothing yet
        }

        public AnonymousPipeServerStream Pipe { get; }
        public NodeState State { get; set; } = NodeState.Primary;
    }
    
    public enum NodeState
    {
        Primary,
        Stopping,
        Stopped
    }
}
