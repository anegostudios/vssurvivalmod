using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Util;

#nullable disable

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
        public int Id;
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
                EnumActivityVariableScope scope = scopeFromString(parts[0]);

                controller.VarSys.SetVariable(scope == EnumActivityVariableScope.Player ? controller.PlayerEntity : controller.NPCEntity, scope, parts[1], setvar.Value);
            }
        }

        protected bool IsConditionMet(string variable, string isValue, bool invertCheck)
        {
            if (variable == "player.inventory")
            {
                JsonItemStack jstack = JsonItemStack.FromString(isValue);
                if (!jstack.Resolve(controller.NPCEntity.World, Code + "dialogue talk component quest item", true))
                {
                    return false;
                }

                ItemStack wantStack = jstack.ResolvedItemstack;
                var slot = FindDesiredItem(controller.PlayerEntity, wantStack);
                return invertCheck ? slot == null : slot != null;
            }
            if (variable == "player.inventorywildcard")
            {
                var slot = FindDesiredItem(controller.PlayerEntity, isValue);
                return invertCheck ? slot == null : slot != null;
            }
            if (variable == "player.heldstack")
            {
                if (isValue == "damagedtool")
                {
                    var hotbarslot = controller.PlayerEntity.RightHandItemSlot;
                    if (hotbarslot.Empty) return false;
                    var d = hotbarslot.Itemstack.Collectible.GetRemainingDurability(hotbarslot.Itemstack);
                    var max = hotbarslot.Itemstack.Collectible.GetMaxDurability(hotbarslot.Itemstack);
                    return hotbarslot.Itemstack.Collectible.GetTool(hotbarslot) != null && d < max;
                }
                else if (isValue == "damagedarmor")
                {
                    var hotbarslot = controller.PlayerEntity.RightHandItemSlot;
                    if (hotbarslot.Empty) return false;
                    var d = hotbarslot.Itemstack.Collectible.GetRemainingDurability(hotbarslot.Itemstack);
                    var max = hotbarslot.Itemstack.Collectible.GetMaxDurability(hotbarslot.Itemstack);
                    return hotbarslot.Itemstack.Collectible.FirstCodePart() == "armor" && d < max;
                }
                else
                {
                    JsonItemStack jstack = JsonItemStack.FromString(isValue);
                    if (!jstack.Resolve(controller.NPCEntity.World, Code + "dialogue talk component quest item", true))
                    {
                        return false;
                    }

                    ItemStack wantStack = jstack.ResolvedItemstack;
                    var hotbarslot = controller.PlayerEntity.RightHandItemSlot;
                    if (matches(controller.PlayerEntity, wantStack, hotbarslot, getIgnoreAttrs()))
                    {
                        return true;
                    }
                }
            }

            string[] parts = variable.Split(new char[] { '.' }, 2);
            var scope = scopeFromString(parts[0]);
            string curValue = controller.VarSys.GetVariable(scope, parts[1], scope == EnumActivityVariableScope.Player ? controller.PlayerEntity : controller.NPCEntity);

            return invertCheck ? curValue != isValue : curValue == isValue;
        }




        public static ItemSlot FindDesiredItem(EntityAgent eagent, ItemStack wantStack)
        {
            ItemSlot foundSlot = null;
            string[] ignoredAttrs = getIgnoreAttrs();

            eagent.WalkInventory((slot) =>
            {
                if (slot.Empty) return true;
                if (matches(eagent, wantStack, slot, ignoredAttrs))
                {
                    foundSlot = slot;
                    return false;
                }

                return true;
            });

            return foundSlot;
        }
        public static ItemSlot FindDesiredItem(EntityAgent eagent, AssetLocation wildcardcode)
        {
            ItemSlot foundSlot = null;
            eagent.WalkInventory((slot) =>
            {
                if (slot.Empty) return true;
                if (WildcardUtil.Match(wildcardcode, slot.Itemstack.Collectible.Code))
                {
                    foundSlot = slot;
                    return false;
                }

                return true;
            });

            return foundSlot;
        }



        private static string[] getIgnoreAttrs()
        {
            return GlobalConstants.IgnoredStackAttributes.Append("backpack").Append("condition").Append("durability").Append("randomX").Append("randomZ");
        }

        private static bool matches(EntityAgent eagent, ItemStack wantStack, ItemSlot slot, string[] ignoredAttrs)
        {
            var giveStack = slot.Itemstack;

            if (wantStack.Equals(eagent.World, giveStack, ignoredAttrs) || giveStack.Satisfies(wantStack))
            {
                if (giveStack.Collectible.IsReasonablyFresh(eagent.World, giveStack) && giveStack.StackSize >= wantStack.StackSize)
                {
                    return true;
                }
            }

            return false;
        }


        private static EnumActivityVariableScope scopeFromString(string name)
        {
            EnumActivityVariableScope scope = EnumActivityVariableScope.Global;
            if (name == "global")
            {
                scope = EnumActivityVariableScope.Global;
            }
            if (name == "player")
            {
                scope = EnumActivityVariableScope.Player;
            }
            if (name == "entity")
            {
                scope = EnumActivityVariableScope.Entity;
            }
            if (name == "group")
            {
                scope = EnumActivityVariableScope.Group;
            }

            return scope;
        }

        public virtual void Init(ref int uniqueIdCounter)
        {
            
        }
    }

}
