using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ControlUpdater
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length < 2) return;
            string pathToCopyTo = args[0];
            string newFilePath = args[1];
            Thread.Sleep(5000);    //give time to stop old program
            if (File.Exists(pathToCopyTo)) File.Delete(pathToCopyTo);
            File.Copy(newFilePath, pathToCopyTo);
            string exePath = Path.Combine(Path.GetDirectoryName(pathToCopyTo), "WAIBControl.exe");
            ProcessStartInfo psi = new ProcessStartInfo(exePath);
            Process.Start(psi);
        }
    }
}
