﻿using ProtoBuf;
using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    [ProtoContract]
    public class EntityVariables
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
    public class VariableData
    {
        [ProtoMember(1)]
        public EntityVariables GlobalVariables = new EntityVariables();
        [ProtoMember(2)]
        public Dictionary<string, EntityVariables> PlayerVariables = new Dictionary<string, EntityVariables>();
        [ProtoMember(4)]
        public Dictionary<string, EntityVariables> GroupVariables = new Dictionary<string, EntityVariables>();
    }

    public delegate void OnDialogueControllerInitDelegate(VariableData data, EntityPlayer playerEntity, EntityAgent npcEntity);

    public class VariablesModSystem : ModSystem
    {
        public VariableData VarData;

        public ICoreAPI Api;
        protected ICoreServerAPI sapi;

        public event OnDialogueControllerInitDelegate OnDialogueControllerInit;

        public override void Start(ICoreAPI api)
        {
            this.Api = api;
            api.Network.RegisterChannel("variable").RegisterMessageType<VariableData>();

            api.ChatCommands.GetOrCreate("debug").BeginSubCommand("clearvariables").HandleWith(cmdClearVariables);

            OnDialogueControllerInit += setDefaultVariables;
        }

        private TextCommandResult cmdClearVariables(TextCommandCallingArgs args)
        {
            VarData.GlobalVariables = new EntityVariables();
            VarData.PlayerVariables = new Dictionary<string, EntityVariables>();
            VarData.GroupVariables = new Dictionary<string, EntityVariables>();
            return TextCommandResult.Success("Variables cleared");
        }

        public void SetVariable(Entity callingEntity, EnumActivityVariableScope scope, string name, string value)
        {
            switch (scope)
            {
                case EnumActivityVariableScope.Entity:
                    {
                        var tree = callingEntity.WatchedAttributes.GetTreeAttribute("variables");
                        if (tree == null) callingEntity.WatchedAttributes["variables"] = tree = new TreeAttribute();
                        tree[name] = new StringAttribute(value);
                        break;
                    }

                case EnumActivityVariableScope.Global:
                    VarData.GlobalVariables[name] = value;
                    break;
                case EnumActivityVariableScope.Group:
                    {
                        var groupCode = callingEntity.WatchedAttributes.GetString("groupCode");
                        EntityVariables variables = null;
                        if (!VarData.GroupVariables.TryGetValue(groupCode, out variables))
                        {
                            VarData.GroupVariables[groupCode] = variables = new EntityVariables();
                        }
                        variables[name] = value;
                        break;
                    }

                case EnumActivityVariableScope.Player:
                    {
                        var uid = (callingEntity as EntityPlayer).Player.PlayerUID;
                        EntityVariables variables = null;
                        if (!VarData.PlayerVariables.TryGetValue(uid, out variables))
                        {
                            VarData.PlayerVariables[uid] = variables = new EntityVariables();
                        }
                        variables[name] = value;
                        break;
                    }

                case EnumActivityVariableScope.EntityPlayer:
                    {
                        var uid = (callingEntity as EntityPlayer).Player.PlayerUID;

                        var tree = callingEntity.WatchedAttributes.GetTreeAttribute("variables");
                        if (tree == null) callingEntity.WatchedAttributes["variables"] = tree = new TreeAttribute();
                        tree[uid + "-" + name] = new StringAttribute(value);
                        break;
                    }

            }
        }

        public void SetPlayerVariable(string playerUid, string name, string value)
        {
            EntityVariables variables = null;
            if (!VarData.PlayerVariables.TryGetValue(playerUid, out variables))
            {
                VarData.PlayerVariables[playerUid] = variables = new EntityVariables();
            }
            variables[name] = value;
        }


        public string GetVariable(EnumActivityVariableScope scope, string name, Entity callingEntity)
        {
            switch (scope)
            {
                case EnumActivityVariableScope.Entity:
                    {
                        var tree = callingEntity.WatchedAttributes.GetTreeAttribute("variables");
                        if (tree != null)
                        {
                            return (tree[name] as StringAttribute)?.value;
                        }

                        return null;
                    }

                case EnumActivityVariableScope.Global:
                    return VarData.GlobalVariables[name];
                case EnumActivityVariableScope.Group:
                    {
                        var groupCode = callingEntity.WatchedAttributes.GetString("groupCode");
                        EntityVariables variables = null;
                        if (!VarData.GroupVariables.TryGetValue(groupCode, out variables))
                        {
                            return null;
                        }
                        return variables[name];
                    }

                case EnumActivityVariableScope.Player:
                    {
                        var uid = (callingEntity as EntityPlayer).Player.PlayerUID;
                        EntityVariables variables = null;
                        if (!VarData.PlayerVariables.TryGetValue(uid, out variables))
                        {
                            return null;
                        }
                        return variables[name];
                    }

                case EnumActivityVariableScope.EntityPlayer:
                    {
                        var uid = (callingEntity as EntityPlayer).Player.PlayerUID;
                        var tree = callingEntity.WatchedAttributes.GetTreeAttribute("variables");
                        if (tree != null)
                        {
                            return (tree[uid + "-" + name] as StringAttribute)?.value;
                        }

                        return null;
                    }
            }

            return null;
        }

        public string GetPlayerVariable(string playerUid, string name)
        {
            EntityVariables variables = null;
            if (!VarData.PlayerVariables.TryGetValue(playerUid, out variables))
            {
                return null;
            }
            return variables[name];
        }

        private void setDefaultVariables(VariableData data, EntityPlayer playerEntity, EntityAgent npcEntity)
        {
            if (!data.PlayerVariables.TryGetValue(playerEntity.PlayerUID, out var vars))
            {
                vars = data.PlayerVariables[playerEntity.PlayerUID] = new EntityVariables();
            }

            vars["characterclass"] = playerEntity.WatchedAttributes.GetString("characterClass", null);
        }

        public void OnControllerInit(EntityPlayer playerEntity, EntityAgent npcEntity)
        {
            if (VarData == null)
            {
                playerEntity.Api.Logger.Warning("Variable system has not received initial state from server, may produce wrong dialogue for state-dependent cases eg. Treasure Hunter trader.");
                VarData = new VariableData();   // radfast 4.2.24  It can sometimes be null on clients, maybe the onDialogueData packet was not received?
            }
            OnDialogueControllerInit?.Invoke(VarData, playerEntity, npcEntity);
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            api.Network.GetChannel("variable").SetMessageHandler<VariableData>(onDialogueData);
        }

        private void onDialogueData(VariableData dlgData)
        {
            this.VarData = dlgData;
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
            sapi.Network.GetChannel("variable").SendPacket(VarData, byPlayer);
        }

        private void Event_GameWorldSave()
        {
            sapi.WorldManager.SaveGame.StoreData("dialogueData", SerializerUtil.Serialize(VarData));
        }

        private void Event_SaveGameLoaded()
        {
            VarData = sapi.WorldManager.SaveGame.GetData("dialogueData", new VariableData());
        }
    }
}
