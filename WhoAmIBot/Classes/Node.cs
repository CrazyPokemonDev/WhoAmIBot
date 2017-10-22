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
        private NamedPipeServerStream Pipe { get; }
        public NodeState State { get; set; } = NodeState.Primary;
        public string Path { get; set; }
        private List<string> queue = new List<string>();
        private Thread QThread;
        public event EventHandler<Node> NodeStopped;

        public Node(string path)
        {
            Console.WriteLine("Initializing Node at {0}", path);
            QThread = new Thread(Queue_Thread);
            Path = path;
            Process = new Process();
            Process.StartInfo.FileName = path;
            string pipename = DateTime.Now.ToString("MMddhhmmss");
            Pipe = new NamedPipeServerStream(pipename, PipeDirection.Out);
            Process.StartInfo.Arguments = pipename;
            Process.StartInfo.UseShellExecute = false;
        }

        public void Start(string token)
        {
            Console.WriteLine("Starting node at {0}", Path);
            Process.Start();
            Pipe.WaitForConnection();
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
            State = NodeState.Stopped;
            NodeStopped?.Invoke(this, this);
        }

        public void SoftStop()
        {
            Queue("STOP");
            State = NodeState.Stopping;
        }

        private void Queue_Thread()
        {
            try
            {
                using (var sw = new StreamWriter(Pipe))
                {
                    Console.WriteLine("Queue thread at {0} started", Path);
                    while (true)
                    {
                        try { Pipe.WaitForConnection(); } catch (InvalidOperationException) { }
                        while (queue.Count < 1) ;
                        var data = queue[0];
                        sw.WriteLine(data);
                        sw.Flush();
                        Pipe.WaitForPipeDrain();
                        queue.Remove(data);
                    }
                }
            }
            catch
            {
                Console.WriteLine("Queue thread of node at {0} stopped.", Path);
                State = NodeState.Stopped;
                NodeStopped?.Invoke(this, this);
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
