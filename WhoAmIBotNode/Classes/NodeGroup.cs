namespace WhoAmIBotSpace.Classes
{
    public class NodeGroup
    {
        public long Id { get; }
        public string LangKey { get; set; }
        public string Name { get; set; }
        public bool CancelgameAdmin { get; set; } = true;
        public int JoinTimeout { get; set; } = 10;
        public int GameTimeout { get; set; } = 1440;
        public NodeGroup(long id)
        {
            Id = id;
        }

        public static implicit operator NodeGroup(Group group)
        {
            return new NodeGroup(group.Id) { LangKey = group.LangKey, Name = group.Name,
                CancelgameAdmin = group.CancelgameAdmin, GameTimeout = group.GameTimeout,
                JoinTimeout = group.JoinTimeout };
        }
    }
}
