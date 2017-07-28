using Newtonsoft.Json;
using System.Collections.Generic;

namespace WhoAmIBotSpace.Classes
{
    [JsonObject(MemberSerialization = MemberSerialization.OptIn)]
    public class LangFile
    {
        [JsonProperty(PropertyName = "key", Required = Required.Always)]
        public string LangKey { get; set; }
        [JsonProperty(PropertyName = "name", Required = Required.Always)]
        public string Name { get; set; }
        [JsonProperty(PropertyName = "strings", Required = Required.Always)]
        public List<JString> Strings { get; set; }
    }
}
