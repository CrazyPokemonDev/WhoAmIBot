namespace WhoAmIBotSpace.Classes
{
    public class Group
    {
        public long Id { get; }
        public string LangKey { get; set; }
        public bool LangSet { get; private set; } = false;
        public Group(long id, bool langSet = false)
        {
            Id = id;
            LangSet = langSet;
        }

        public void SetLanguage(string langKey)
        {
            LangKey = langKey;
            LangSet = true;
        }
    }
}
