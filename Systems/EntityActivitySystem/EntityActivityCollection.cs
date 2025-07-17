using Newtonsoft.Json;
using System.Collections.Generic;
using Vintagestory.API.Common;

#nullable disable

namespace Vintagestory.GameContent
{
    [JsonObject(MemberSerialization.OptIn)]
    public class EntityActivityCollection
    {
        [JsonProperty]
        public string Name;
        [JsonProperty]
        public List<EntityActivity> Activities = new List<EntityActivity>();


        EntityActivitySystem vas;

        public EntityActivityCollection() { }
        public EntityActivityCollection(EntityActivitySystem vas)
        {
            this.vas = vas;
        }

        public EntityActivityCollection Clone()
        {
            JsonSerializerSettings settings = new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All };
            string json = JsonConvert.SerializeObject(this, Formatting.Indented, settings);
            return JsonUtil.ToObject<EntityActivityCollection>(json, "", settings);
        }

        public void OnLoaded(EntityActivitySystem vas)
        {
            this.vas = vas;
            foreach (var activity in Activities) activity.OnLoaded(vas);
        }
    }

}
