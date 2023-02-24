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
                dialog?.EmitDialogue(comps);
                return null;
            } else
            {
                var rnd = controller.NPCEntity.World.Rand;

                dialog?.EmitDialogue(new RichTextComponent[] { comps[rnd.Next(comps.Length)] });
                return JumpTo != null ? JumpTo : "next";
            }
        }

        protected RichTextComponent[] genText()
        {
            var font = CairoFont.WhiteSmallText().WithLineHeightMultiplier(1.2);

            List<RichTextComponent> comps = new List<RichTextComponent>();

            var api = controller.NPCEntity.Api;

            if (IsPlayer)
            {
                comps.Add(new RichTextComponent(api as ICoreClientAPI, "\r\n", font));
            }

            int answerNumber = 1;

            for (int i = 0; i < Text.Length; i++)
            {
                if (!conditionsMet(Text[i].Conditions)) continue;

                var text = Text[i].Value
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

                bool found = false;
                controller.PlayerEntity.WalkInventory((slot) =>
                {
                    if (!slot.Empty && slot.Itemstack.Satisfies(jstack.ResolvedItemstack)) { found = true; return false; }
                    return true;
                });

                return found;
            }

            if (IsConditionMet(cond.Variable, cond.IsValue, cond.Invert)) return true;
            return false;
        }

        public void SelectAnswer(int index)
        {
            var api = controller.NPCEntity.Api;
            if (api is ICoreClientAPI capi)
            {
                capi.Network.SendEntityPacket(controller.NPCEntity.EntityId, EntityBehaviorConversable.SelectAnswerPacketId, SerializerUtil.Serialize(index));
            }

            jumpTo(Text[index].JumpTo);
        }

        private void jumpTo(string code)
        {
            controller.JumpTo(code);
        }
    }

}
