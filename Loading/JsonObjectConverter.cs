using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using Vintagestory.API;

namespace Vintagestory.ServerMods.NoObf
{
    /// <summary>
    /// Implementation of JsonConverter that converts objects to an instance of a JsonObject
    /// </summary>
    public class JsonObjectConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return true;
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            return new JsonObject(JObject.Load(reader));
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {

        }
    }
}
