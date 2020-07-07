using Newtonsoft.Json;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace Vintagestory.ServerMods
{
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    class UpgradeHerePacket
    {
        public BlockPos Pos;
    }

    public class UpgradeTasks : ModSystem
    {
        ICoreAPI api;
        ICoreClientAPI capi;

        public override bool ShouldLoad(EnumAppSide forSide)
        {
            return true;
        }

        public override void Start(ICoreAPI api)
        {
            api.Network
                .RegisterChannel("upgradeTasks")
                .RegisterMessageType<UpgradeHerePacket>()
            ;
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            this.api = api;
            this.capi = api;

            api.Input.InWorldAction += Input_InWorldAction;
        }

        private void Input_InWorldAction(EnumEntityAction action, bool on, ref EnumHandling handled)
        {
            if (action == EnumEntityAction.RightMouseDown)
            {
                BlockSelection bs = capi.World.Player.CurrentBlockSelection;
                if (bs == null) return;

                Block block = api.World.BlockAccessor.GetBlock(bs.Position);
                string[] parts = block.Code.Path.Split(new char[] { '-' }, 3);

                if ((parts[0] == "clayplanter" || parts[0] == "flowerpot") && parts.Length >= 3)
                {
                    capi.Network.GetChannel("upgradeTasks").SendPacket(new UpgradeHerePacket() { Pos = bs.Position });
                }
            }
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            this.api = api;
            api.Network.GetChannel("upgradeTasks").SetMessageHandler<UpgradeHerePacket>(didUseBlock);

            api.Event.DidBreakBlock += Event_DidBreakBlock;
        }

        private void didUseBlock(IServerPlayer fromPlayer, UpgradeHerePacket networkMessage)
        {
            FixOldPlantContainers(networkMessage.Pos);
        }


        private void Event_DidBreakBlock(IServerPlayer byPlayer, int oldblockId, BlockSelection blockSel)
        {
            DropPlantContainer(oldblockId, blockSel.Position);
        }







        void FixOldPlantContainers(BlockPos pos)
        {
            Block block = api.World.BlockAccessor.GetBlock(pos);
            string[] parts = block.Code.Path.Split(new char[] { '-' }, 3);

            if ((parts[0] == "clayplanter" || parts[0] == "flowerpot") && parts.Length >= 3)
            {
                Block potblock = api.World.GetBlock(new AssetLocation(parts[0] + "-" + parts[1]));
                api.World.BlockAccessor.SetBlock(potblock.Id, pos);

                var bepcont = api.World.BlockAccessor.GetBlockEntity(pos) as BlockEntityPlantContainer;

                Block plantBlock = null;

                if (plantBlock == null)
                {
                    plantBlock = api.World.GetBlock(new AssetLocation("flower-" + parts[2] + "-free"));
                }
                if (plantBlock == null)
                {
                    plantBlock = api.World.GetBlock(new AssetLocation("sapling-" + parts[2] + "-free"));
                }
                if (plantBlock == null)
                {
                    plantBlock = api.World.GetBlock(new AssetLocation("mushroom-" + parts[2] + "-normal-free"));
                }

                if (plantBlock != null)
                {
                    ItemStack plantStack = new ItemStack(plantBlock);
                    bepcont.TrySetContents(plantStack);
                }
            }

        }

        void DropPlantContainer(int blockid, BlockPos pos)
        {
            Block block = api.World.GetBlock(blockid);
            string[] parts = block.Code.Path.Split(new char[] { '-' }, 3);
            if (parts.Length < 3) return;

            if ((parts[0] == "clayplanter" || parts[1] == "flowerpot") && parts.Length >= 3)
            {
                Block potblock = api.World.GetBlock(new AssetLocation(parts[0] + "-" + parts[1]));
                ItemStack potStack = new ItemStack(potblock);
                api.World.SpawnItemEntity(potStack, pos.ToVec3d().Add(0.5, 0.5, 0.5));

                Block plantBlock = null;

                if (plantBlock == null)
                {
                    plantBlock = api.World.GetBlock(new AssetLocation("flower-" + parts[2]));
                }
                if (plantBlock == null)
                {
                    plantBlock = api.World.GetBlock(new AssetLocation("sapling-" + parts[2]));
                }
                if (plantBlock == null)
                {
                    plantBlock = api.World.GetBlock(new AssetLocation("mushroom-" + parts[2] + "-normal"));
                }

                if (plantBlock != null)
                {
                    ItemStack plantStack = new ItemStack(plantBlock);
                    api.World.SpawnItemEntity(plantStack, pos.ToVec3d().Add(0.5, 0.5, 0.5));
                }
            }
        }
    }
}
