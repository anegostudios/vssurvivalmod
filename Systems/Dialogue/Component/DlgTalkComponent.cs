using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class DlgTalkComponent : DialogueComponent
    {
        public DialogeTextElement[] Text;

        bool IsPlayer => Owner == "player";

        public override string Execute()
        {
            setVars();

            var comps = genText();

            if (IsPlayer)
            {
                if (comps.Length > 0) dialog?.EmitDialogue(comps);
                return null;
            }
            else
            {
                var rnd = controller.NPCEntity.World.Rand;

                if (comps.Length > 0) dialog?.EmitDialogue(new RichTextComponent[] { comps[rnd.Next(comps.Length)] });
                return JumpTo != null ? JumpTo : "next";
            }
        }

        protected RichTextComponent[] genText()
        {
            List<RichTextComponent> comps = new List<RichTextComponent>();
            var api = controller.NPCEntity.Api;

            if (api.Side != EnumAppSide.Client) return comps.ToArray();

            var font = CairoFont.WhiteSmallText().WithLineHeightMultiplier(1.2);

            if (IsPlayer)
            {
                comps.Add(new RichTextComponent(api as ICoreClientAPI, "\r\n", font));
            }

            int answerNumber = 1;

            for (int i = 0; i < Text.Length; i++)
            {
                if (!conditionsMet(Text[i].Conditions)) continue;

                var text = Lang.Get(Text[i].Value)
                    .Replace("{characterclass}", Lang.Get("characterclass-" + controller.PlayerEntity.WatchedAttributes.GetString("characterClass", null)))
                    .Replace("{playername}", controller.PlayerEntity.GetBehavior<EntityBehaviorNameTag>()?.DisplayName)
                    .Replace("{npcname}", controller.NPCEntity.GetBehavior<EntityBehaviorNameTag>()?.DisplayName)
                ;

                if (IsPlayer)
                {
                    int index = i;
                    var lcomp = new LinkTextComponent(api as ICoreClientAPI, answerNumber + ". " + text, font, (comp) => SelectAnswer(index));
                    comps.Add(lcomp);
                    comps.Add(new RichTextComponent(api as ICoreClientAPI, "\r\n", font));

                    lcomp.Font.WithColor(GuiStyle.ColorTime1).WithOrientation(EnumTextOrientation.Right);
                    answerNumber++;
                }
                else
                {
                    comps.Add(new RichTextComponent(api as ICoreClientAPI, text + "\r\n", font));
                }
            }

            return comps.ToArray();
        }

        private bool conditionsMet(ConditionElement[] conds)
        {
            if (conds == null) return true;
            
            foreach (var cond in conds)
            {
                if (!isConditionMet(cond))
                {
                    return false;
                }
            }
            return true;
        }

        private bool isConditionMet(ConditionElement cond)
        {
            if (cond.Variable == "player.inventory")
            {
                JsonItemStack jstack = JsonItemStack.FromString(cond.IsValue);
                if (!jstack.Resolve(controller.NPCEntity.World, Code + "dialogue talk component quest item", true))
                {
                    return false;
                }

                ItemStack wantStack = jstack.ResolvedItemstack;
                var slot = FindDesiredItem(controller.PlayerEntity, wantStack);
                return slot != null;
            }

            if (IsConditionMet(cond.Variable, cond.IsValue, cond.Invert)) return true;
            return false;
        }


        public static ItemSlot FindDesiredItem(EntityAgent eagent, ItemStack wantStack)
        {
            ItemSlot foundSlot = null;
            string[] ignoredAttrs = GlobalConstants.IgnoredStackAttributes.Append("backpack").Append("condition").Append("durability");

            eagent.WalkInventory((slot) =>
            {
                if (slot.Empty) return true;
                var giveStack = slot.Itemstack;

                if (wantStack.Equals(eagent.World, giveStack, ignoredAttrs) || giveStack.Satisfies(wantStack))
                {
                    if (giveStack.Collectible.IsReasonablyFresh(eagent.World, giveStack))
                    {
                        foundSlot = slot;
                        return false;
                    }
                }

                return true;
            });

            return foundSlot;
        }

        public void SelectAnswer(int index)
        {
            var api = controller.NPCEntity.Api;
            if (api is ICoreClientAPI capi)
            {
                capi.Network.SendEntityPacket(controller.NPCEntity.EntityId, EntityBehaviorConversable.SelectAnswerPacketId, SerializerUtil.Serialize(index));
            }

            dialog?.ClearDialogue();
            jumpTo(Text[index].JumpTo);
        }

        private void jumpTo(string code)
        {
            controller.JumpTo(code);
        }
    }

}
