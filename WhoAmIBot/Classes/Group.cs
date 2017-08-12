namespace WhoAmIBotSpace.Classes
{
    public class Group
    {
        public long Id { get; }
        public string LangKey { get; set; }
        public string Name { get; set; }
        public bool CancelgameAdmin { get; set; } = true;
        public int JoinTimeout { get; set; } = 10;
        public int GameTimeout { get; set; } = 1440;
        public Group(long id)
        {
            Id = id;
        }
    }
}
