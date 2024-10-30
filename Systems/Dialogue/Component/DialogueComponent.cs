using Newtonsoft.Json;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace Vintagestory.GameContent
{
    public class DialogeTextElement
    {
        public string Value;
        public string JumpTo;

        /// <summary>
        /// Alias for Conditions[0] = new ConditionElement[] { value };
        /// </summary>
        public ConditionElement Condition
        {
            set
            {
                Conditions = new ConditionElement[] { value };
            }
        }

        public ConditionElement[] Conditions;
    }

    [JsonConverter(typeof(DialogueConditionComponentJsonConverter))]
    public class ConditionElement
    {
        public string Variable;
        public string IsValue;
        public bool Invert;
    }

    [JsonConverter(typeof(DialogueComponentJsonConverter))]
    public abstract class DialogueComponent
    {
        public string Code;
        public string Owner;
        public string Sound;
        public string Type;
        public Dictionary<string, string> SetVariables;
        public string JumpTo;
        public string Trigger;
        public JsonObject TriggerData;


        protected DialogueController controller;

        // This is null on the server
        protected GuiDialogueDialog dialog;

        public virtual void SetReferences(DialogueController controller, GuiDialogueDialog dialogue)
        {
            this.controller = controller;
            this.dialog = dialogue;
        }

        /// <summary>
        /// Run this dialogue component
        /// </summary>
        /// <returns>The next component code or null to halt the dialogue</returns>
        public abstract string Execute();

        protected void setVars()
        {
            if (Trigger != null)
            {
                controller.Trigger(controller.PlayerEntity, Trigger, TriggerData);
            }

            if (Sound != null)
            {
                var api = controller.PlayerEntity.Api;
                api.World.PlaySoundAt(new AssetLocation(Sound).WithPathPrefixOnce("sounds/"), controller.NPCEntity, controller.PlayerEntity?.Player, false, 16);
            }

            if (SetVariables == null) return;
            foreach (var setvar in SetVariables)
            {
                string[] parts = setvar.Key.Split('.');

                if (parts[0] == "global")
                {
                    controller.GlobalVariables[parts[1]] = setvar.Value;
                }
                if (parts[0] == "player")
                {
                    controller.PlayerVariables[controller.PlayerEntity.PlayerUID][parts[1]] = setvar.Value;
                }
                if (parts[0] == "entity")
                {
                    controller.EntityVariables[controller.NPCEntity.EntityId][controller.PlayerEntity.PlayerUID + "-" + parts[1]] = setvar.Value;
                }
            }
        }


        protected bool IsConditionMet(string variable, string isValue, bool invertCheck)
        {
            string[] parts = variable.Split(new char[] { '.' }, 2);
            string curValue = null;

            if (parts[0] == "global")
            {
                curValue = controller.GlobalVariables[parts[1]];
            }
            if (parts[0] == "player")
            {
                if (!controller.PlayerVariables.ContainsKey(controller.PlayerEntity.PlayerUID)) controller.PlayerVariables[controller.PlayerEntity.PlayerUID] = new EntityVariables();

                curValue = controller.PlayerVariables[controller.PlayerEntity.PlayerUID][parts[1]];
            }
            if (parts[0] == "entity")
            {
                if (!controller.EntityVariables.ContainsKey(controller.NPCEntity.EntityId)) controller.EntityVariables[controller.NPCEntity.EntityId] = new EntityVariables();

                curValue = controller.EntityVariables[controller.NPCEntity.EntityId][controller.PlayerEntity.PlayerUID + "-" + parts[1]];
            }

            return invertCheck ? curValue != isValue : curValue == isValue;
        }
    }

}
