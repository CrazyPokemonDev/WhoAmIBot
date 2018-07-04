namespace WhoAmIBotSpace.Classes
{
    public class NodeUser
    {
        public long Id { get; }
        public string LangKey { get; set; }
        public string Name { get; set; }
        public string Username { get; set; }
        public NodeUser(long id)
        {
            Id = id;
        }
    }
}
