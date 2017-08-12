namespace WhoAmIBotSpace.Classes
{
    public class Group
    {
        public long Id { get; }
        public string LangKey { get; set; }
        public string Name { get; set; }
        public Group(long id)
        {
            Id = id;
        }
    }
}
