using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class BlockChute : Block, IBlockItemFlow
    {
        string Type => Variant["type"];
        string Side => Variant["side"];
        string Vertical => Variant["vertical"];

        string[] PullFaces => Attributes["pullFaces"].AsArray<string>(new string[0]);
        string[] PushFaces => Attributes["pushFaces"].AsArray<string>(new string[0]);
        string[] AcceptFaces => Attributes["acceptFaces"].AsArray<string>(new string[0]);


        public bool HasItemFlowConnectorAt(BlockFacing facing)
        {
            return PullFaces.Contains(facing.Code) || PushFaces.Contains(facing.Code) || AcceptFaces.Contains(facing.Code);
        }


        public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel, ref string failureCode)
        {
            BlockChute blockToPlace = null;

            if (blockToPlace == null)
            {
                BlockFacing[] facings = SuggestedHVOrientation(byPlayer, blockSel);

                if (Type == "elbow" || Type == "3way")
                {
                    string vertical = blockSel.Face.IsVertical ? (blockSel.Face == BlockFacing.UP ? "up" : "down") : "down";
                    AssetLocation code = CodeWithVariants(new string[] { "vertical", "side" }, new string[] { vertical, facings[0].Code });
                    blockToPlace = api.World.GetBlock(code) as BlockChute;

                    int i = 0;
                    while (blockToPlace != null && !blockToPlace.CanStay(world, blockSel.Position))
                    {
                        if (i >= BlockFacing.ALLFACES.Length) break;
                        blockToPlace = api.World.GetBlock(CodeWithVariants(new string[] { "vertical", "side" }, new string[] { vertical, BlockFacing.ALLFACES[i++].Code })) as BlockChute;
                    }
                }

                if (Type == "t")
                {
                    string variant = facings[0].Axis == EnumAxis.X ? "ns" : "we";
                    if (!blockSel.Face.IsVertical) {
                        variant = "ud-" + facings[0].Code[0];
                    }

                    blockToPlace = api.World.GetBlock(CodeWithVariant("side", variant)) as BlockChute;

                    if (!blockToPlace.CanStay(world, blockSel.Position))
                    {
                        blockToPlace = api.World.GetBlock(CodeWithVariant("side", facings[0].Axis == EnumAxis.X ? "we" : "ns")) as BlockChute;
                    }
                }

                if (Type == "straight")
                {
                    string variant = facings[0].Axis == EnumAxis.X ? "we" : "ns";
                    if (blockSel.Face.IsVertical)
                    {
                        variant = "ud";
                    }
                    blockToPlace = api.World.GetBlock(CodeWithVariant("side", variant)) as BlockChute;
                }

                if (Type == "cross")
                {
                    string variant = facings[0].Axis != EnumAxis.X ? "ns" : "we";
                    if (blockSel.Face.IsVertical)
                    {
                        variant = "ground";
                    }
                    blockToPlace = api.World.GetBlock(CodeWithVariant("side", variant)) as BlockChute;
                }

            }

            
            if (blockToPlace != null && blockToPlace.CanPlaceBlock(world, byPlayer, blockSel, ref failureCode) && (blockToPlace as BlockChute).CanStay(world, blockSel.Position))
            {
                world.BlockAccessor.SetBlock(blockToPlace.BlockId, blockSel.Position);
                return true;
            }

            if (Type == "cross")
            {
                blockToPlace = api.World.GetBlock(CodeWithVariant("side", "ground")) as BlockChute;
            }

            if (blockToPlace != null && blockToPlace.CanPlaceBlock(world, byPlayer, blockSel, ref failureCode) && (blockToPlace as BlockChute).CanStay(world, blockSel.Position))
            {
                world.BlockAccessor.SetBlock(blockToPlace.BlockId, blockSel.Position);
                return true;
            }

            return false;
        }

        public override void OnNeighbourBlockChange(IWorldAccessor world, BlockPos pos, BlockPos neibpos)
        {
            if (!CanStay(world, pos))
            {
                world.BlockAccessor.BreakBlock(pos, null);
            }
        }


        bool CanStay(IWorldAccessor world, BlockPos pos)
        {
            BlockPos npos = new BlockPos();

            if (PullFaces != null)
            {
                foreach (var val in PullFaces)
                {
                    BlockFacing face = BlockFacing.FromCode(val);
                    Block block = world.BlockAccessor.GetBlock(npos.Set(pos).Add(face));
                    if (block.CanAttachBlockAt(world.BlockAccessor, this, pos, face) || (block as IBlockItemFlow)?.HasItemFlowConnectorAt(face.GetOpposite()) == true || world.BlockAccessor.GetBlockEntity(npos) is BlockEntityContainer) return true;
                }
            }

            if (PushFaces != null)
            {
                foreach (var val in PushFaces)
                {
                    BlockFacing face = BlockFacing.FromCode(val);
                    Block block = world.BlockAccessor.GetBlock(npos.Set(pos).Add(face));
                    if (block.CanAttachBlockAt(world.BlockAccessor, this, pos, face) || (block as IBlockItemFlow)?.HasItemFlowConnectorAt(face.GetOpposite()) == true || world.BlockAccessor.GetBlockEntity(npos) is BlockEntityContainer) return true;
                }
            }


            //if (world.BlockAccessor.GetBlock(pos.DownCopy()).Replaceable < 6000) return true;

            return false;
        }

        
        public override BlockDropItemStack[] GetDropsForHandbook(ItemStack handbookStack, IPlayer forPlayer)
        {
            return new BlockDropItemStack[] { new BlockDropItemStack(handbookStack) };
        }

        public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1f)
        {
            Block block = null;

            if (Type == "elbow" || Type == "3way")
            {
                block = api.World.GetBlock(CodeWithVariants(new string[] { "vertical", "side" }, new string[] { "down", "east" }));
            }

            if (Type == "t" || Type == "straight")
            {
                block = api.World.GetBlock(CodeWithVariant("side", "ns"));
            }

            if (Type == "cross")
            {
                block = api.World.GetBlock(CodeWithVariant("side", "ground"));
            }

            return new ItemStack[] { new ItemStack(block) };
        }

        public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos)
        {
            return GetDrops(world, pos, null)[0];
        }


        public override AssetLocation GetRotatedBlockCode(int angle)
        {
            return base.GetRotatedBlockCode(angle);
        }

        public override AssetLocation GetHorizontallyFlippedBlockCode(EnumAxis axis)
        {
            return base.GetHorizontallyFlippedBlockCode(axis);
        }

        public override AssetLocation GetVerticallyFlippedBlockCode()
        {
            return base.GetVerticallyFlippedBlockCode();
        }
    }
}
