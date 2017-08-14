using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WhoAmIBotSpace.Classes
{
    public class HelperException : Exception
    {
        public HelperException(string message) : base(message)
        {

        }
    }
}
