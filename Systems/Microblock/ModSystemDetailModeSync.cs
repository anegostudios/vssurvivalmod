using ProtoBuf;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.GameContent;
using Vintagestory.API.Common.CommandAbbr;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using System.Collections.Generic;

#nullable disable

namespace Vintagestory.ServerMods
{
    [ProtoContract]
    internal class SetDetailModePacket
    {
        [ProtoMember(1)]
        public bool Enable;
    }

    internal class ModSystemDetailModeSync : ModSystem
    {
        public override bool ShouldLoad(EnumAppSide forSide) => true;
        ICoreServerAPI sapi;

        public override void Start(ICoreAPI api)
        {
            sapi = api as ICoreServerAPI;
            api.Network.RegisterChannel("detailmodesync").RegisterMessageType<SetDetailModePacket>();
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            api.Network.GetChannel("detailmodesync").SetMessageHandler<SetDetailModePacket>(onPacket);
        }


        public override void StartServerSide(ICoreServerAPI api)
        {
            base.StartServerSide(api);

            new MicroblockCommands().Start(api);
        }


        private void onPacket(SetDetailModePacket packet)
        {
            BlockEntityChisel.ForceDetailingMode = packet.Enable;
        }


        internal void Toggle(string playerUID, bool on)
        {
            sapi.Network.GetChannel("detailmodesync").SendPacket(new SetDetailModePacket() { Enable = on }, sapi.World.PlayerByUid(playerUID) as IServerPlayer);
        }
    }
}
