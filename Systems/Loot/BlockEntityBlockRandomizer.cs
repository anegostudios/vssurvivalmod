using System;
using System.Collections.Generic;
using System.IO;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class BlockEntityBlockRandomizer : BlockEntityContainer
    {
        const int quantitySlots = 10;
        ICoreClientAPI capi;
        public float[] Chances = new float[quantitySlots];

        InventoryGeneric inventory;

        public BlockEntityBlockRandomizer()
        {
            inventory = new InventoryGeneric(quantitySlots, null, null);
        }

        public override InventoryBase Inventory => inventory;

        public override string InventoryClassName => "randomizer";

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            capi = api as ICoreClientAPI;

            bool isNewlyplaced = inventory == null;
            if (isNewlyplaced)
            {
                InitInventory(Block);
            }
        }

        protected virtual void InitInventory(Block block)
        {
            inventory = new InventoryGeneric(quantitySlots, null, null, null);
            inventory.BaseWeight = 1f;
        }

        public override void OnBlockPlaced(ItemStack byItemStack = null)
        {
            if (byItemStack == null || !byItemStack.Attributes.HasAttribute("chances")) return;

            Chances = (byItemStack.Attributes["chances"] as FloatArrayAttribute).value;
            inventory.FromTreeAttributes(byItemStack.Attributes);
        }

        protected override bool HasTransitionables()
        {
            return false;
        }
        protected override void OnTick(float dt)
        {
            // Dont execute base code
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            var block = worldForResolving.GetBlock(new AssetLocation(tree.GetString("blockCode"))) as BlockCrate;

            if (inventory == null)
            {
                if (tree.HasAttribute("blockCode"))
                {
                    InitInventory(block);
                }
                else
                {
                    InitInventory(null);
                }
            }

            Chances = (tree["chances"] as FloatArrayAttribute).value;
            if (Chances == null) Chances = new float[quantitySlots];
            if (Chances.Length < quantitySlots) Chances = Chances.Append(ArrayUtil.CreateFilled<float>(quantitySlots - Chances.Length, (i) => 0f));

            base.FromTreeAttributes(tree, worldForResolving);
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            tree["chances"] = new FloatArrayAttribute(Chances);
        }

        public void OnInteract(IPlayer byPlayer)
        {
            if (byPlayer.WorldData.CurrentGameMode != EnumGameMode.Creative) return;

            if (Api.Side == EnumAppSide.Client)
            {
                var dlg = new GuiDialogItemLootRandomizer(inventory, Chances, capi, "Block randomizer");
                dlg.TryOpen();
                dlg.OnClosed += () => DidCloseLootRandomizer(dlg);
            }
        }

        private void DidCloseLootRandomizer(GuiDialogItemLootRandomizer dialog)
        {
            var attr = dialog.Attributes;
            if (attr.GetInt("save") == 0) return;

            using (MemoryStream ms = new MemoryStream())
            {
                BinaryWriter writer = new BinaryWriter(ms);
                attr.ToBytes(writer);

                capi.Network.SendBlockEntityPacket(Pos, 1130, ms.ToArray());
            }
        }

        public override void OnReceivedClientPacket(IPlayer fromPlayer, int packetid, byte[] data)
        {
            base.OnReceivedClientPacket(fromPlayer, packetid, data);

            if (packetid == 1130)
            {
                TreeAttribute tree = new TreeAttribute();
                tree.FromBytes(data);
                for (int i = 0; i < 10; i++)
                {
                    var stree = tree["stack" + i] as TreeAttribute;
                    if (stree == null) continue;

                    Chances[i] = stree.GetFloat("chance");

                    var stack = stree.GetItemstack("stack");
                    stack.ResolveBlockOrItem(Api.World);
                    inventory[i].Itemstack = stack;
                }

                MarkDirty();
            }
        }


        static AssetLocation airFillerblockCode = new AssetLocation("meta-filler");

        public override void OnPlacementBySchematic(ICoreServerAPI api, IBlockAccessor blockAccessor, BlockPos pos, Dictionary<int, Dictionary<int, int>> replaceBlocks, int centerrockblockid, Block layerBlock, bool resolveImports)
        {
            base.OnPlacementBySchematic(api, blockAccessor, pos, replaceBlocks, centerrockblockid, layerBlock, resolveImports);

            if(!resolveImports) return;

            var ba = blockAccessor is IBlockAccessorRevertable ? api.World.BlockAccessor : blockAccessor;

            float sumchance = 0;
            for (int i = 0; i < 10; i++)
            {
                sumchance += Chances[i];
            }

            double rnd = api.World.Rand.NextDouble() * Math.Max(100, sumchance);
            for (int i = 0; i < 10; i++)
            {
                var block = inventory[i].Itemstack?.Block;
                rnd -= Chances[i];
                if (rnd <= 0 && block != null)
                {
                    if (block.Code == airFillerblockCode) ba.SetBlock(0, pos);
                    else if (block.Id == BlockMicroBlock.BlockLayerMetaBlockId)
                    {
                        ba.SetBlock(layerBlock?.Id ?? 0, pos);
                    }
                    else
                    {
                        if (replaceBlocks != null)
                        {
                            Dictionary<int, int> replaceByBlock;
                            if (replaceBlocks.TryGetValue(block.Id, out replaceByBlock))
                            {
                                int newBlockId;
                                if (replaceByBlock.TryGetValue(centerrockblockid, out newBlockId))
                                {
                                    block = blockAccessor.GetBlock(newBlockId);
                                }
                            }
                        }

                        var beHa = block.GetBehavior<BlockBehaviorHorizontalAttachable>();

                        if (beHa != null)
                        {
                            var offset = api.World.Rand.Next(BlockFacing.HORIZONTALS.Length);
                            for (var index = 0; index < BlockFacing.HORIZONTALS.Length; index++)
                            {
                                var facing = BlockFacing.HORIZONTALS[(index + offset) % BlockFacing.HORIZONTALS.Length];
                                var newBlock = ba.GetBlock(block.CodeWithParts(facing.Code));
                                var attachingBlockPos = pos.AddCopy(facing);
                                var attachingBlock = ba.GetBlock(attachingBlockPos);
                                if (!attachingBlock.CanAttachBlockAt(ba, newBlock, pos, facing)) continue;

                                ba.SetBlock(newBlock.Id, pos);
                                break;
                            }
                        }
                        else
                        {
                            ba.SetBlock(block.Id, pos);
                            BlockEntity be;
                            if (blockAccessor is IWorldGenBlockAccessor && block.EntityClass != null)
                            {
                                blockAccessor.SpawnBlockEntity(block.EntityClass, pos);
                            }
                            be = blockAccessor.GetBlockEntity(pos);
                            be?.Initialize(api);
                            be?.OnBlockPlaced(inventory[i].Itemstack);
                        }

                    }
                    return;
                }
            }

            // At below 100% chance and miss: Turn to air block
            ba.SetBlock(0, pos);
        }

        public override void OnBlockBroken(IPlayer byPlayer = null)
        {
            // Dont drop contents
        }
    }
}
