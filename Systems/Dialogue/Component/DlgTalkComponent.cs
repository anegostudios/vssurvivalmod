using System.Collections.Generic;
using System.Linq;
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

            var comps = genText(!IsPlayer);
            if (comps.Length > 0) dialog?.EmitDialogue(comps);

            if (IsPlayer)
            {
                return null;
            }
            else
            {
                return JumpTo != null ? JumpTo : "next";
            }
        }

        protected RichTextComponentBase[] genText(bool selectRandom)
        {
            List<RichTextComponentBase> comps = new List<RichTextComponentBase>();
            var api = controller.NPCEntity.Api;

            if (api.Side != EnumAppSide.Client) return comps.ToArray();

            var font = CairoFont.WhiteSmallText().WithLineHeightMultiplier(1.2);

            if (IsPlayer)
            {
                comps.Add(new RichTextComponent(api as ICoreClientAPI, "\r\n", font));
            }

            int answerNumber = 1;

            List<DialogeTextElement> elems = new List<DialogeTextElement>();
            for (int i = 0; i < Text.Length; i++)
            {
                if (!selectRandom || conditionsMet(Text[i].Conditions)) {
                    elems.Add(Text[i]);
                }
            }

            int rnd = api.World.Rand.Next(elems.Count);

            for (int i = 0; i < elems.Count; i++)
            {
                if (!selectRandom && !conditionsMet(Text[i].Conditions)) continue;
                if (selectRandom && i != rnd) continue;

                var text = Lang.Get(elems[i].Value)
                    .Replace("{characterclass}", Lang.Get("characterclass-" + controller.PlayerEntity.WatchedAttributes.GetString("characterClass", null)))
                    .Replace("{playername}", controller.PlayerEntity.GetBehavior<EntityBehaviorNameTag>()?.DisplayName)
                    .Replace("{npcname}", controller.NPCEntity.GetBehavior<EntityBehaviorNameTag>()?.DisplayName)
                ;

                if (IsPlayer)
                {
                    int id = elems[i].Id;
                    var lcomp = new LinkTextComponent(api as ICoreClientAPI, answerNumber + ". " + text, font, (comp) => SelectAnswerById(id));
                    comps.Add(lcomp);
                    comps.Add(new RichTextComponent(api as ICoreClientAPI, "\r\n", font));

                    lcomp.Font.WithColor(usedAnswers?.Contains(id) == true ? GuiStyle.ColorParchment : GuiStyle.ColorTime1).WithOrientation(EnumTextOrientation.Right);
                    answerNumber++;
                }
                else
                {
                    comps.AddRange(VtmlUtil.Richtextify(api as ICoreClientAPI, text + "\r\n", font));
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
            if (IsConditionMet(cond.Variable, cond.IsValue, cond.Invert)) return true;
            return false;
        }



        public void SelectAnswerById(int id)
        {
            var api = controller.NPCEntity.Api;

            var answer = Text.FirstOrDefault(elem => elem.Id == id);

            if (answer == null)
            {
                api.Logger.Warning($"Got invalid answer index: {id} for {controller.NPCEntity.Code}");
                return;
            }
            if (IsPlayer)
            {
                if (usedAnswers == null) usedAnswers = new();
                usedAnswers.Add(id);
            }

            if (api is ICoreClientAPI capi)
            {
                capi.Network.SendEntityPacket(controller.NPCEntity.EntityId, EntityBehaviorConversable.SelectAnswerPacketId, SerializerUtil.Serialize(id));
            }

            dialog?.ClearDialogue();
            jumpTo(answer.JumpTo);
        }


        HashSet<int> usedAnswers;
        private void jumpTo(string code)
        {
            controller.JumpTo(code);
        }


        public override void Init(ref int uniqueIdCounter)
        {
            foreach (var val in Text)
            {
                val.Id = uniqueIdCounter++;
            }
        }
    }

}
