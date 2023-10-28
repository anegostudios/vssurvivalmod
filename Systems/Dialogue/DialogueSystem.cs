using ProtoBuf;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    [ProtoContract]
    public class DialogueVariables
    {
        [ProtoMember(1)]
        public Dictionary<string, string> Variables = new Dictionary<string, string>();

        public string this[string key]
        {
            get
            {
                Variables.TryGetValue(key, out string value);
                return value;
            }
            set
            {
                Variables[key] = value;
            }
        }
    }

    [ProtoContract]
    public class DialogueData
    {
        [ProtoMember(1)]
        public DialogueVariables GlobalVariables = new DialogueVariables();
        [ProtoMember(2)]
        public Dictionary<string, DialogueVariables> PlayerVariables = new Dictionary<string, DialogueVariables>();
        [ProtoMember(3)]
        public Dictionary<long, DialogueVariables> EntityVariables = new Dictionary<long, DialogueVariables>();
    }

    public delegate void OnDialogueControllerInitDelegate(DialogueData data, EntityPlayer playerEntity, EntityAgent npcEntity);

    public class DialogueSystem : ModSystem
    {
        public DialogueData DlgData;

        public ICoreAPI Api;
        protected ICoreServerAPI sapi;

        public event OnDialogueControllerInitDelegate OnDialogueControllerInit;

        public override void Start(ICoreAPI api)
        {
            this.Api = api;
            api.Network.RegisterChannel("dialogue").RegisterMessageType<DialogueData>();

            OnDialogueControllerInit += setDefaultVariables;
        }

        private void setDefaultVariables(DialogueData data, EntityPlayer playerEntity, EntityAgent npcEntity)
        {
            var vars = data.PlayerVariables[playerEntity.PlayerUID] = new DialogueVariables();
            vars["characterclass"] = playerEntity.WatchedAttributes.GetString("characterClass", null);
        }

        public void OnControllerInit(EntityPlayer playerEntity, EntityAgent npcEntity)
        {
            OnDialogueControllerInit?.Invoke(DlgData, playerEntity, npcEntity);
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            api.Network.GetChannel("dialogue").SetMessageHandler<DialogueData>(onDialogueData);
        }

        private void onDialogueData(DialogueData dlgData)
        {
            this.DlgData = dlgData;
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            sapi = api;

            api.Event.SaveGameLoaded += Event_SaveGameLoaded;
            api.Event.GameWorldSave += Event_GameWorldSave;
            api.Event.PlayerJoin += Event_PlayerJoin;
        }

        private void Event_PlayerJoin(IServerPlayer byPlayer)
        {
            sapi.Network.GetChannel("dialogue").SendPacket(DlgData, byPlayer);
        }

        private void Event_GameWorldSave()
        {
            sapi.WorldManager.SaveGame.StoreData("dialogueData", SerializerUtil.Serialize(DlgData));
        }

        private void Event_SaveGameLoaded()
        {
            DlgData = sapi.WorldManager.SaveGame.GetData("dialogueData", new DialogueData());
        }
    }
}
