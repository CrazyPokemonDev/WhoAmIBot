namespace WhoAmIBotSpace.Classes
{
    public class RestartEventArgs
    {
        public string NewDllPath { get; }
        public string OldDllPath { get; }

        public RestartEventArgs(string dllPath, string newDllPath)
        {
            OldDllPath = dllPath;
            NewDllPath = newDllPath;
        }
    }
}