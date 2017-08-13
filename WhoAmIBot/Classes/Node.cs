using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Threading;

namespace WhoAmIBotSpace.Classes
{
    public class Node
    {
        public Process Process { get; }
        private AnonymousPipeServerStream Pipe { get; }
        public NodeState State { get; set; } = NodeState.Primary;
        public string Path { get; set; }
        private List<string> queue = new List<string>();
        private Thread QThread;
        
        public Node(string path)
        {
            Console.WriteLine("Initializing Node at {0}", path);
            QThread = new Thread(Queue_Thread);
            Path = path;
            Process = new Process();
            Process.StartInfo.FileName = path;
            Pipe = new AnonymousPipeServerStream(PipeDirection.Out, HandleInheritability.Inheritable);
            Process.StartInfo.Arguments = Pipe.GetClientHandleAsString();
            Process.StartInfo.UseShellExecute = false;
        }

        public void Start(string token)
        {
            Process.Start();
            var sw = new StreamWriter(Pipe);
            sw.WriteLine("TOKEN:" + token);
            sw.Flush();
            Pipe.WaitForPipeDrain();
            QThread.Start();
        }
        
        public void Stop()
        {
            QThread.Abort();
            try
            {
                Process.Kill();
            }
            catch (Exception x)
            {
                Console.WriteLine("Node at {1}:\nFailed to kill process with error: {0}", x.Message, Path);
            }
        }

        private void Queue_Thread()
        {
            try
            {
                using (var sw = new StreamWriter(Pipe))
                {
                    while (true)
                    {
                        while (queue.Count < 1) ;
                        var data = queue[0];
                        sw.WriteLine(data);
                        sw.Flush();
                        Pipe.WaitForPipeDrain();
                        queue.Remove(data);
                    }
                }
            }
            finally
            {
                Console.WriteLine("Queue thread of node at {0} stopped.", Path);
                State = NodeState.Stopped;
            }
        }

        public void Queue(string data)
        {
            queue.Add(data);
        }
    }
    
    public enum NodeState
    {
        Primary,
        Stopping,
        Stopped
    }
}
