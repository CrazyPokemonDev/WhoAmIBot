using Newtonsoft.Json;

namespace WhoAmIBotSpace.Classes
{
    [JsonObject(MemberSerialization = MemberSerialization.OptIn)]
    public class JString
    {
        [JsonProperty(PropertyName = "key")]
        public string Key { get; set; }
        [JsonProperty(PropertyName = "value")]
        public string Value { get; set; }

        public JString(string key, string value)
        {
            Key = key;
            Value = value;
        }
    }
}
