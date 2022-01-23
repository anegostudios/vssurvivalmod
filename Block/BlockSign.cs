using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using System.Linq;
using Vintagestory.API.Util;
using System;

namespace Vintagestory.GameContent
{
    public class BlockSign : Block
    {
        WorldInteraction[] interactions;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            if (api.Side != EnumAppSide.Client) return;

            interactions = ObjectCacheUtil.GetOrCreate(api, "signBlockInteractions", () =>
            {
                List<ItemStack> stacksList = new List<ItemStack>();

                foreach (CollectibleObject collectible in api.World.Collectibles)
                {
                    if (collectible.Attributes?["pigment"].Exists == true)
                    {
                        stacksList.Add(new ItemStack(collectible));
                    }
                }

                return new WorldInteraction[] { new WorldInteraction()
                    {
                        ActionLangCode = "blockhelp-sign-write",
                        HotKeyCode = "sneak",
                        MouseButton = EnumMouseButton.Right,
                        Itemstacks = stacksList.ToArray()
                    }
                };
            });
        }

        public override Cuboidf[] GetCollisionBoxes(IBlockAccessor blockAccessor, BlockPos pos)
        {
            if (Variant["attachment"] == "wall") return base.GetCollisionBoxes(blockAccessor, pos);

            BlockEntitySign besign = blockAccessor.GetBlockEntity(pos) as BlockEntitySign;
            if (besign != null) return besign.colSelBox;
            return base.GetCollisionBoxes(blockAccessor, pos);
        }

        public override Cuboidf[] GetSelectionBoxes(IBlockAccessor blockAccessor, BlockPos pos)
        {
            if (Variant["attachment"] == "wall") return base.GetCollisionBoxes(blockAccessor, pos);

            BlockEntitySign besign = blockAccessor.GetBlockEntity(pos) as BlockEntitySign;
            if (besign != null) return besign.colSelBox;
            return base.GetSelectionBoxes(blockAccessor, pos);
        }

        public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection bs, ref string failureCode)
        {
            BlockPos supportingPos = bs.Position.AddCopy(bs.Face.Opposite);
            Block supportingBlock = world.BlockAccessor.GetBlock(supportingPos);

            if (bs.Face.IsHorizontal && (supportingBlock.CanAttachBlockAt(world.BlockAccessor, this, supportingPos, bs.Face) || supportingBlock.Attributes?.IsTrue("partialAttachable") == true))
            {
                Block wallblock = world.BlockAccessor.GetBlock(CodeWithParts("wall", bs.Face.Opposite.Code));

                if (!wallblock.CanPlaceBlock(world, byPlayer, bs, ref failureCode))
                {
                    return false;
                }

                world.BlockAccessor.SetBlock(wallblock.BlockId, bs.Position);
                return true;
            }



            if (!CanPlaceBlock(world, byPlayer, bs, ref failureCode))
            {
                return false;
            }

            BlockFacing[] horVer = SuggestedHVOrientation(byPlayer, bs);

            AssetLocation blockCode = CodeWithParts(horVer[0].Code);
            Block block = world.BlockAccessor.GetBlock(blockCode);
            world.BlockAccessor.SetBlock(block.BlockId, bs.Position);

            BlockEntitySign bect = world.BlockAccessor.GetBlockEntity(bs.Position) as BlockEntitySign;
            if (bect != null)
            {
                BlockPos targetPos = bs.DidOffset ? bs.Position.AddCopy(bs.Face.Opposite) : bs.Position;
                double dx = byPlayer.Entity.Pos.X - (targetPos.X + bs.HitPosition.X);
                double dz = (float)byPlayer.Entity.Pos.Z - (targetPos.Z + bs.HitPosition.Z);
                float angleHor = (float)Math.Atan2(dx, dz);

                float deg45 = GameMath.PIHALF / 2;
                float roundRad = ((int)Math.Round(angleHor / deg45)) * deg45;
                bect.MeshAngleRad = roundRad;
            }


            return true;
        }


        public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1f)
        {
            Block block = world.BlockAccessor.GetBlock(CodeWithParts("ground", "north"));
            return new ItemStack[] { new ItemStack(block) };
        }

        public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos)
        {
            Block block = world.BlockAccessor.GetBlock(CodeWithParts("ground", "north"));
            return new ItemStack(block);
        }



        public override void OnNeighbourBlockChange(IWorldAccessor world, BlockPos pos, BlockPos neibpos)
        {
            base.OnNeighbourBlockChange(world, pos, neibpos);
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            BlockEntity entity = world.BlockAccessor.GetBlockEntity(blockSel.Position);

            if (entity is BlockEntitySign)
            {
                BlockEntitySign besigh = (BlockEntitySign)entity;
                besigh.OnRightClick(byPlayer);
                return true;
            }

            return true;
        }

        public override AssetLocation GetHorizontallyFlippedBlockCode(EnumAxis axis)
        {
            BlockFacing facing = BlockFacing.FromCode(LastCodePart());
            if (facing.Axis == axis)
            {
                return CodeWithParts(facing.Opposite.Code);
            }
            return Code;
        }

        public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer)
        {
            return interactions.Append(base.GetPlacedBlockInteractionHelp(world, selection, forPlayer));
        }

        public override AssetLocation GetRotatedBlockCode(int angle)
        {
            BlockFacing beforeFacing = BlockFacing.FromCode(LastCodePart());
            int rotatedIndex = GameMath.Mod(beforeFacing.HorizontalAngleIndex - angle / 90, 4);
            BlockFacing nowFacing = BlockFacing.HORIZONTALS_ANGLEORDER[rotatedIndex];

            return CodeWithParts(nowFacing.Code);
        }
    }
}
