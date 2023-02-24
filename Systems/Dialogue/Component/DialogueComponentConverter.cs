using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using Vintagestory.API.Datastructures;

namespace Vintagestory.GameContent
{
    public class DialogueConditionComponentJsonConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(ConditionElement);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            JToken token = JToken.ReadFrom(reader);
            JsonObject jobj = new JsonObject(token);

            bool invert = !jobj["isvalue"].Exists && jobj["isnotvalue"].Exists;

            return new ConditionElement()
            {
                Variable = jobj["variable"].AsString(),
                IsValue = invert ? jobj["isnotvalue"].AsString() : jobj["isvalue"].AsString(),
                Invert = invert,
            };
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
        }
    }

    public class DialogueComponentJsonConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(DialogueComponent);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            JToken token = JToken.ReadFrom(reader);
            JsonObject jobj = new JsonObject(token);

            string code = jobj["code"].AsString();
            string jumpto = jobj["jumpTo"].AsString();
            string owner = jobj["owner"].AsString();
            string sound = jobj["sound"].AsString();
            DialogeTextElement[] text = jobj["text"].AsArray<DialogeTextElement>();
            string type = jobj["type"].AsString("");
            var variables = jobj["setVariables"].AsObject<Dictionary<string, string>>();
            string trigger = jobj["trigger"].AsString();
            bool invert = !jobj["isvalue"].Exists && jobj["isnotvalue"].Exists;

            switch (type)
            {
                case "talk":
                    return new DlgTalkComponent() { Code = code, SetVariables = variables, Owner = owner, Text = text, Type = type, Trigger = trigger, TriggerData = jobj["triggerdata"], JumpTo = jumpto, Sound = sound };
                case "condition":
                    return new DlgConditionComponent() { 
                        Code = code,
                        SetVariables = variables,
                        Owner = owner, 
                        Type = type, 
                        Variable = jobj["variable"].AsString(), 
                        IsValue = invert ? jobj["isnotvalue"].AsString() : jobj["isvalue"].AsString(),
                        InvertCondition = invert,
                        ThenJumpTo = jobj["thenJumpTo"].AsString(), 
                        ElseJumpTo = jobj["elseJumpTo"].AsString(),
                        Trigger = trigger,
                        TriggerData = jobj["triggerdata"],
                        Sound = sound
                    };
                case "":
                case "trigger":
                case "setvariables":
                case "jump":
                    return new DlgGenericComponent() { Code = code, SetVariables = variables, Owner = owner, Type = type, Trigger = trigger, TriggerData = jobj["triggerdata"], JumpTo = jumpto, Sound = sound };

                default: throw new JsonReaderException("Invalid dialog component type " + type);
            }
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            
        }
    }
}