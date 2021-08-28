using System;
using System.Threading;

namespace WhoAmIBotSpace
{
    public class Program
    {
        private static WhoAmIBot Bot;

        public static void Main(string[] args)
        {
            string token;

            if (args.Length == 0)
            {
                Console.Write("Please enter the token (or pass it as console argument): ");
                token = Console.ReadLine();
            }
            else
            {
                token = args[0];
            }

            Console.WriteLine("Starting WhoAmIBot...");

            Bot = new WhoAmIBot(token);
            Bot.StartBot();

            Console.WriteLine("WhoAmIBot running!");

            Thread.Sleep(-1);
        }
    }
}
