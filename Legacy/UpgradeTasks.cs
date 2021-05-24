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
using Vintagestory.API.Datastructures;
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
                if (block.Code == null) return;

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

            api.RegisterCommand("upgradearea", "Fixes chiseled blocks, pots and planters broken in v1.13", "", onUpgradeCmd, "worldedit");
            api.RegisterCommand("setchiselblockmat", "Sets the material of a currently looked at chisel block to the material in the active hands", "", onSetChiselMat, "worldedit");
            api.RegisterCommand("setchiseleditable", "Upgrade/Downgrade chiseled blocks to an editable/non-editable state in given area", "", onSetChiselEditable, "worldedit");
        }

        private void onSetChiselMat(IServerPlayer player, int groupId, CmdArgs args)
        {
            BlockPos pos = player.CurrentBlockSelection?.Position;
            if (pos == null)
            {
                player.SendMessage(groupId, "Look at a block first", EnumChatType.CommandError);
                return;
            }

            BlockEntityChisel bechisel = api.World.BlockAccessor.GetBlockEntity(pos) as BlockEntityChisel;
            if (bechisel == null)
            {
                player.SendMessage(groupId, "Not looking at a chiseled block", EnumChatType.CommandError);
                return;
            }

            Block block = player.InventoryManager?.ActiveHotbarSlot?.Itemstack?.Block;

            if (block == null)
            {
                player.SendMessage(groupId, "You need a block in your active hand", EnumChatType.CommandError);
                return;
            }

            for (int i = 0; i < bechisel.MaterialIds.Length; i++)
            {
                bechisel.MaterialIds[i] = block.Id;
            }

            bechisel.MarkDirty(true);
            player.SendMessage(groupId, "Ok material set", EnumChatType.CommandError);
            return;
        }



        private void onSetChiselEditable(IServerPlayer player, int groupId, CmdArgs args)
        {
            var wmod = api.ModLoader.GetModSystem<WorldEdit.WorldEdit>();

            var workspace = wmod.GetWorkSpace(player.PlayerUID);

            if (workspace == null || workspace.StartMarker == null || workspace.EndMarker == null)
            {
                player.SendMessage(groupId, "Select an area with worldedit first", EnumChatType.CommandError);
                return;
            }

            bool editable = args.PopBool() == true;
            bool resetName = args.PopBool() == true;

            Block chiselBlock = api.World.GetBlock(new AssetLocation("chiseledblock"));
            Block microblock = api.World.GetBlock(new AssetLocation("microblock"));

            Block targetBlock = editable ? chiselBlock : microblock;

            int startx = Math.Min(workspace.StartMarker.X, workspace.EndMarker.X);
            int endx = Math.Max(workspace.StartMarker.X, workspace.EndMarker.X);
            int starty = Math.Min(workspace.StartMarker.Y, workspace.EndMarker.Y);
            int endy = Math.Max(workspace.StartMarker.Y, workspace.EndMarker.Y);
            int startz = Math.Min(workspace.StartMarker.Z, workspace.EndMarker.Z);
            int endZ = Math.Max(workspace.StartMarker.Z, workspace.EndMarker.Z);
            BlockPos pos = new BlockPos();

            IBulkBlockAccessor ba = api.World.BulkBlockAccessor;

            int cnt = 0;

            for (int x = startx; x < endx; x++)
            {
                for (int y = starty; y < endy; y++)
                {
                    for (int z = startz; z < endZ; z++)
                    {
                        pos.Set(x, y, z);

                        Block block = ba.GetBlock(x, y, z);
                        if (block is BlockMicroBlock && block.Id != targetBlock.Id)
                        {
                            cnt++;

                            TreeAttribute tree = new TreeAttribute();
                            BlockEntityMicroBlock be = api.World.BlockAccessor.GetBlockEntity(pos) as BlockEntityMicroBlock;
                            be.ToTreeAttributes(tree);

                            api.World.BlockAccessor.SetBlock(targetBlock.Id, pos);

                            be = api.World.BlockAccessor.GetBlockEntity(pos) as BlockEntityMicroBlock;
                            be.FromTreeAttributes(tree, api.World);

                            if (resetName)
                            {
                                be.BlockName = api.World.BlockAccessor.GetBlock(be.MaterialIds[0]).GetPlacedBlockName(api.World, pos);
                            }
                        }
                    }
                }
            }

            player.SendMessage(groupId, string.Format("Ok. {0} Chisel blocks exchanged", cnt), EnumChatType.CommandSuccess);
        }




        private void onUpgradeCmd(IServerPlayer player, int groupId, CmdArgs args)
        {
            var wmod = api.ModLoader.GetModSystem<WorldEdit.WorldEdit>();

            var workspace = wmod.GetWorkSpace(player.PlayerUID);

            if (workspace == null || workspace.StartMarker == null || workspace.EndMarker == null)
            {
                player.SendMessage(groupId, "Select an area with worldedit first", EnumChatType.CommandError);
                return;
            }

            int startx = Math.Min(workspace.StartMarker.X, workspace.EndMarker.X);
            int endx = Math.Max(workspace.StartMarker.X, workspace.EndMarker.X);
            int starty = Math.Min(workspace.StartMarker.Y, workspace.EndMarker.Y);
            int endy = Math.Max(workspace.StartMarker.Y, workspace.EndMarker.Y);
            int startz = Math.Min(workspace.StartMarker.Z, workspace.EndMarker.Z);
            int endZ = Math.Max(workspace.StartMarker.Z, workspace.EndMarker.Z);
            BlockPos pos = new BlockPos();

            Dictionary<string, Block> blocksByName = new Dictionary<string, Block>();
            foreach (var block in api.World.Blocks)
            {
                if (block.IsMissing || block.Code == null) continue;
                blocksByName[block.GetHeldItemName(new ItemStack(block))] = block;
            }

            int graniteBlockId = api.World.GetBlock(new AssetLocation("rock-granite")).Id;

            for (int x = startx; x < endx; x++)
            {
                for (int y = starty; y < endy; y++)
                {
                    for (int z = startz; z < endZ; z++)
                    {
                        pos.Set(x, y, z);

                        Block block = api.World.BlockAccessor.GetBlock(x, y, z);
                        if (block is BlockChisel)
                        {
                            BlockEntityChisel bechisel = api.World.BlockAccessor.GetBlockEntity(pos) as BlockEntityChisel;
                            if (bechisel.MaterialIds != null && bechisel.MaterialIds.Length > 0 && bechisel.MaterialIds[0] == graniteBlockId)
                            {

                                Block matblock = null;
                                if (blocksByName.TryGetValue(bechisel.BlockName, out matblock))
                                {
                                    bechisel.MaterialIds[0] = matblock.Id;
                                    bechisel.MarkDirty(true);
                                }
                            }
                        }

                        if (block is BlockPlantContainer)
                        {
                            FixOldPlantContainers(pos);
                        }
                    }
                }
            }
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
            if (block.Code == null) return;

            string[] parts =  block.Code.Path.Split(new char[] { '-' }, 3);
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
