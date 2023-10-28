using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class DialogueController
    {
        public EntityPlayer PlayerEntity;
        public EntityAgent NPCEntity;

        DialogueComponent[] dialogue;
        DialogueComponent currentDialogueCmp;
        ICoreAPI api;

        public event DialogueTriggerDelegate DialogTriggers;

        DialogueSystem dlgSys;
        DialogueData dlgData => dlgSys.DlgData;

        public DialogueVariables GlobalVariables => dlgData.GlobalVariables;
        public Dictionary<string, DialogueVariables> PlayerVariables => dlgData.PlayerVariables;
        public Dictionary<long, DialogueVariables> EntityVariables => dlgData.EntityVariables;


        


        public DialogueController(ICoreAPI api, EntityPlayer playerEntity, EntityAgent npcEntity, DialogueConfig dialogueConfig)
        {
            this.api = api;
            this.PlayerEntity = playerEntity;
            this.NPCEntity = npcEntity;
            this.dialogue = dialogueConfig.components;

            currentDialogueCmp = dialogue[0];

            dlgSys = api.ModLoader.GetModSystem<DialogueSystem>();
            dlgSys.OnControllerInit(playerEntity, npcEntity);
        }


        public int Trigger(EntityAgent triggeringEntity, string value, JsonObject data)
        {
            if (DialogTriggers == null) return 0;

            int nextCompoId = 0;
            foreach (DialogueTriggerDelegate dele in DialogTriggers.GetInvocationList())
            {
                var compoid = dele.Invoke(triggeringEntity, value, data);
                if (compoid != -1) nextCompoId = compoid;
            }

            return nextCompoId;
        }

        public void Init()
        {
            ContinueExecute();
        }

        public void PlayerSelectAnswer(int index)
        {
            if (currentDialogueCmp is DlgTalkComponent dlgTalkCompo)
            {
                dlgTalkCompo.SelectAnswer(index);
            }
        }

        public void JumpTo(string code)
        {
            currentDialogueCmp = dialogue.FirstOrDefault(dlgcmp => dlgcmp.Code == code);
            if (currentDialogueCmp == null)
            {
                (api as ICoreClientAPI)?.TriggerIngameError(this, "dialogueerror", "Invalid chat fragment of code " + code + " found");
                return;
            }

            ContinueExecute();
        }

        public void ContinueExecute()
        {
            string nextCode;

            while ((nextCode = currentDialogueCmp.Execute()) != null)
            {
                var nextCmp = dialogue.FirstOrDefault(dlgcmp => dlgcmp.Code == nextCode);
                if (nextCmp == null)
                {
                    if (nextCode == "next")
                    {
                        int nextIndex = dialogue.IndexOf(currentDialogueCmp) + 1;
                        if (nextIndex < dialogue.Length)
                        {
                            nextCmp = dialogue[nextIndex];
                        }
                    }
                }
                if (nextCmp == null) break;

                currentDialogueCmp = nextCmp;
            }

            //Console.WriteLine(dlgSys.Api.Side + ": " + currentDialogueCmp?.Code);
        }
    }
}
