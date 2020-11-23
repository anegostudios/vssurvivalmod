using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using System.Linq;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class BlockSign : Block
    {
        WorldInteraction[] interactions;

        public override void OnLoaded(ICoreAPI api)
        {
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

        public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection bs, ref string failureCode)
        {
            BlockPos supportingPos = bs.Position.AddCopy(bs.Face.Opposite);
            Block supportingBlock = world.BlockAccessor.GetBlock(supportingPos);

            if (bs.Face.IsHorizontal && (supportingBlock.CanAttachBlockAt(world.BlockAccessor, this, bs.Position, bs.Face) || supportingBlock.Attributes?.IsTrue("partialAttachable") == true))
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
    }
}
