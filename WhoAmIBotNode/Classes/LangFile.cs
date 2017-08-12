using Newtonsoft.Json;
using System.Collections.Generic;

namespace WhoAmIBotSpace.Classes
{
    [JsonObject(MemberSerialization = MemberSerialization.OptIn)]
    public class LangFile
    {
        [JsonIgnore]
        private string _langkey;
        [JsonProperty(PropertyName = "key", Required = Required.Always)]
        public string LangKey
        {
            get
            {
                return _langkey.Replace("'", "''");
            }
            set
            {
                _langkey = value.Replace("''", "'");
            }
        }
        [JsonProperty(PropertyName = "name", Required = Required.Always)]
        public string Name { get; set; }
        [JsonProperty(PropertyName = "strings", Required = Required.Always)]
        public List<JString> Strings { get; set; }
    }
}
