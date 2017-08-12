using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WhoAmIBotNode
{
    class Program
    {
        static void Main(string[] args)
        {
            bool running = true;
            if (args.Length < 1) return;
            using (PipeStream pipeClient = new AnonymousPipeClientStream(PipeDirection.In, args[0]))
            {
                using (var sr = new StreamReader(pipeClient))
                {
                    while (running)
                    {
                        var data = sr.ReadLine();
                        if (!string.IsNullOrEmpty(data))
                        {
                            var t = new Thread(() => HandleData(data));
                            t.Start();
                        }
                    }
                }
            }
        }

        private static void HandleData(string data)
        {
            
        }
    }
}
