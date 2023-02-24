using ProtoBuf;
using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

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

            api.RegisterCommand("microblocktf", "", "", onMicroblockTf, Privilege.gamemode);
        }

        private void onMicroblockTf(IServerPlayer player, int groupId, CmdArgs args)
        {
            /*BlockEntityMicroBlock bem = sapi.World.BlockAccessor.GetBlockEntity(player.CurrentBlockSelection.Position) as BlockEntityMicroBlock;
            if (bem == null)
            {
                player.SendMessage(groupId, "Look at a chiseled block first", EnumChatType.CommandError);
                return;
            }

            string word = args.PopWord();
            switch (word)
            {
                case "t":
                    {
                        Matrixf m = new Matrixf();
                        m.Translate((float)args.PopFloat(0), (float)args.PopFloat(0), (float)args.PopFloat(0));
                        bem.tfMatrix = m.Values;
                        bem.MarkDirty(true);
                        break;
                    }
                case "r":
                    {
                        Matrixf m = new Matrixf();
                        Vec3f rot = new Vec3f((float)args.PopFloat(0), (float)args.PopFloat(0), (float)args.PopFloat(0));
                        Vec3f origin = new Vec3f(0.5f, 0.5f, 0.5f);
                        if (args.Length >= 3)
                        {
                            origin = new Vec3f((float)args.PopFloat(0), (float)args.PopFloat(0), (float)args.PopFloat(0));
                        }
                        
                        m.Translate(origin.X, origin.Y, origin.Z);
                        m.Rotate(rot);
                        m.Translate(-origin.X, -origin.Y, -origin.Z);
                        bem.tfMatrix = m.Values;
                        bem.MarkDirty(true);
                        break;
                    }
            }*/
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
