using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WhoAmIBotSpace.Classes
{
    public class User
    {
        public long Id { get; }
        public string LangKey { get; set; }
        public string Name { get; set; }
        public string Username { get; set; }
        public User(long id)
        {
            Id = id;
        }
    }
}
