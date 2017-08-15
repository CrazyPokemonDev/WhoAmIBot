namespace WhoAmIBotSpace.Classes
{
    public class NodeGroup
    {
        public long Id { get; }
        public string LangKey { get; set; }
        public string Name { get; set; }
        public bool CancelgameAdmin { get; set; } = true;
        public long JoinTimeout { get; set; } = 10;
        public long GameTimeout { get; set; } = 1440;
        public NodeGroup(long id)
        {
            Id = id;
        }
    }
}
