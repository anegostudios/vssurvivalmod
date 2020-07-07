using Vintagestory.API;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class BlockBehaviorHorizontalAttachable : BlockBehavior
    {
        bool handleDrops = true;
        string dropBlockFace = "north";
        string dropBlock = null;

        public BlockBehaviorHorizontalAttachable(Block block) : base(block)
        {
        }

        public override void Initialize(JsonObject properties)
        {
            base.Initialize(properties);

            handleDrops = properties["handleDrops"].AsBool(true);

            if (properties["dropBlockFace"].Exists)
            {
                dropBlockFace = properties["dropBlockFace"].AsString();
            }
            if (properties["dropBlock"].Exists)
            {
                dropBlock = properties["dropBlock"].AsString();
            }
        }

        public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel, ref EnumHandling handling, ref string failureCode)
        {
            handling = EnumHandling.PreventDefault;

            // Prefer selected block face
            if (blockSel.Face.IsHorizontal)
            {
                if (TryAttachTo(world, byPlayer, blockSel, itemstack, ref failureCode)) return true;
            }

            // Otherwise attach to any possible face
            BlockFacing[] faces = BlockFacing.HORIZONTALS;
            blockSel = blockSel.Clone();
            for (int i = 0; i < faces.Length; i++)
            {
                blockSel.Face = faces[i];
                if (TryAttachTo(world, byPlayer, blockSel, itemstack, ref failureCode)) return true;
            }

            failureCode = "requirehorizontalattachable";

            return false;
        }

        public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier, ref EnumHandling handled)
        {
            if (handleDrops)
            {
                handled = EnumHandling.PreventDefault;
                if (dropBlock != null)
                {
                    return new ItemStack[] { new ItemStack(world.BlockAccessor.GetBlock(new AssetLocation(dropBlock))) };
                }
                return new ItemStack[] { new ItemStack(world.BlockAccessor.GetBlock(block.CodeWithParts(dropBlockFace))) };

            } else
            {
                handled = EnumHandling.PassThrough;
                return null;
            }
            
        }

        public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos, ref EnumHandling handled)
        {
            handled = EnumHandling.PreventDefault;

            if (dropBlock != null)
            {
                return new ItemStack(world.BlockAccessor.GetBlock(new AssetLocation(dropBlock)));
            }

            return new ItemStack(world.BlockAccessor.GetBlock(block.CodeWithParts(dropBlockFace)));
        }


        public override void OnNeighbourBlockChange(IWorldAccessor world, BlockPos pos, BlockPos neibpos, ref EnumHandling handled)
        {
            handled = EnumHandling.PreventDefault;

            if (!CanBlockStay(world, pos))
            {
                world.BlockAccessor.BreakBlock(pos, null);
            }
        }


        bool TryAttachTo(IWorldAccessor world, IPlayer player, BlockSelection blockSel, ItemStack itemstack, ref string failureCode)
        {
            BlockFacing oppositeFace = blockSel.Face.GetOpposite();

            BlockPos attachingBlockPos = blockSel.Position.AddCopy(oppositeFace);
            Block attachingBlock = world.BlockAccessor.GetBlock(world.BlockAccessor.GetBlockId(attachingBlockPos));
            Block orientedBlock = world.BlockAccessor.GetBlock(block.CodeWithParts(oppositeFace.Code));

            if (attachingBlock.CanAttachBlockAt(world.BlockAccessor, block, attachingBlockPos, blockSel.Face) && orientedBlock.CanPlaceBlock(world, player, blockSel, ref failureCode))
            {
                orientedBlock.DoPlaceBlock(world, player, blockSel, itemstack);
                return true;
            }

            return false;
        }

        bool CanBlockStay(IWorldAccessor world, BlockPos pos)
        {
            string[] parts = block.Code.Path.Split('-');
            BlockFacing facing = BlockFacing.FromCode(parts[parts.Length - 1]);
            int blockId = world.BlockAccessor.GetBlockId(pos.AddCopy(facing));

            Block attachingblock = world.BlockAccessor.GetBlock(blockId);

            return attachingblock.CanAttachBlockAt(world.BlockAccessor, block, pos.AddCopy(facing), facing.GetOpposite());
        }


        public override bool CanAttachBlockAt(IBlockAccessor world, Block block, BlockPos pos, BlockFacing blockFace, ref EnumHandling handled)
        {
            handled = EnumHandling.PreventDefault;
            return false;
        }


        public override AssetLocation GetRotatedBlockCode(int angle, ref EnumHandling handled)
        {
            handled = EnumHandling.PreventDefault;

            BlockFacing beforeFacing = BlockFacing.FromCode(block.LastCodePart());
            int rotatedIndex = GameMath.Mod(beforeFacing.HorizontalAngleIndex - angle / 90, 4);
            BlockFacing nowFacing = BlockFacing.HORIZONTALS_ANGLEORDER[rotatedIndex];

            return block.CodeWithParts(nowFacing.Code);
        }

        public override AssetLocation GetHorizontallyFlippedBlockCode(EnumAxis axis, ref EnumHandling handling)
        {
            handling = EnumHandling.PreventDefault;

            BlockFacing facing = BlockFacing.FromCode(block.LastCodePart());
            if (facing.Axis == axis)
            {
                return block.CodeWithParts(facing.GetOpposite().Code);
            }
            return block.Code;
        }
    }
}
