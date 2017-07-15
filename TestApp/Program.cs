using FlomBotFactory;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WhoAmIBotSpace;

namespace TestApp
{
    class Program
    {
        static void Main(string[] args)
        {
            FlomBot b = new WhoAmIBot("305529329:AAHc_bq07foP68PUScjFx5cshScruXRINDM");
            b.StartBot();
            while (true)
            {

            }
        }
    }
}
