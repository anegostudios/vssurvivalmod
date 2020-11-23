using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class BlockBehaviorLadder : BlockBehavior
    {
        string dropBlockFace = "north";
        string ownFirstCodePart;

        public BlockBehaviorLadder(Block block) : base(block)
        {
            ownFirstCodePart = block.FirstCodePart();
        }

        public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel, ref EnumHandling handled, ref string failureCode)
        {
            handled = EnumHandling.PreventDefault;

            BlockPos pos = blockSel.Position;
            Block blockAtPos = world.BlockAccessor.GetBlock(pos);

            BlockPos aimedAtPos = blockSel.DidOffset ? pos.AddCopy(blockSel.Face.Opposite) : blockSel.Position;
            Block aimedAtBlock = blockSel.DidOffset ? world.BlockAccessor.GetBlock(pos.AddCopy(blockSel.Face.Opposite)) : blockAtPos;
            
            BlockFacing[] horVer = Block.SuggestedHVOrientation(byPlayer, blockSel);
            AssetLocation blockCode = block.CodeWithParts(horVer[0].Code);

                
            // Has ladder above at aimed position?
            Block aboveBlock = world.BlockAccessor.GetBlock(pos.X, pos.Y + 1, pos.Z);
            if (aboveBlock.FirstCodePart() == ownFirstCodePart && HasSupport(aboveBlock, world.BlockAccessor, pos) && aboveBlock.CanPlaceBlock(world, byPlayer, blockSel, ref failureCode))
            {
                aboveBlock.DoPlaceBlock(world, byPlayer, blockSel, itemstack);
                return true;
            }

            // Has ladder below at aimed position?
            Block belowBlock = world.BlockAccessor.GetBlock(pos.X, pos.Y - 1, pos.Z);
            if (belowBlock.FirstCodePart() == ownFirstCodePart && HasSupport(belowBlock, world.BlockAccessor, pos) && belowBlock.CanPlaceBlock(world, byPlayer, blockSel, ref failureCode))
            {
                belowBlock.DoPlaceBlock(world, byPlayer, blockSel, itemstack);
                return true;
            }


            // Can I put ladder below aimed position?
            if (blockSel.HitPosition.Y < 0.5)
            {
                if (TryStackDown(byPlayer, world, aimedAtPos, blockSel.Face, itemstack)) return true;
            }


            // Can I put ladder above aimed position?
            if (TryStackUp(byPlayer, world, aimedAtPos, blockSel.Face, itemstack)) return true;

            // Can I put ladder below aimed position?
            if (TryStackDown(byPlayer, world, aimedAtPos, blockSel.Face, itemstack)) return true;

                


            Block orientedBlock = world.BlockAccessor.GetBlock(blockCode);
            // Otherwise place if we have support for it
            if (HasSupport(orientedBlock, world.BlockAccessor, pos) && orientedBlock.CanPlaceBlock(world, byPlayer, blockSel, ref failureCode))
            {
                orientedBlock.DoPlaceBlock(world, byPlayer, blockSel, itemstack);
                return true;
            }


            // Otherwise maybe on the other side?
            blockCode = block.CodeWithParts(blockSel.Face.Opposite.Code);
            orientedBlock = world.BlockAccessor.GetBlock(blockCode);
            if (orientedBlock != null && HasSupport(orientedBlock, world.BlockAccessor, pos) && orientedBlock.CanPlaceBlock(world, byPlayer, blockSel, ref failureCode))
            {
                orientedBlock.DoPlaceBlock(world, byPlayer, blockSel, itemstack);
                return true;
            }

            failureCode = "cantattachladder";



            return false;
        }



        private bool TryStackUp(IPlayer byPlayer, IWorldAccessor world, BlockPos pos, BlockFacing face, ItemStack itemstack)
        {
            Block ladderBlock = world.BlockAccessor.GetBlock(pos);
            if (ladderBlock.FirstCodePart() != ownFirstCodePart) return false;

            BlockPos abovePos = pos.UpCopy();
            Block aboveBlock = null;

            while (abovePos.Y < world.BlockAccessor.MapSizeY)
            {
                aboveBlock = world.BlockAccessor.GetBlock(abovePos);
                if (aboveBlock.FirstCodePart() != ownFirstCodePart) break;
                
                abovePos.Up();
            }

            string useless="";

            if (aboveBlock == null || aboveBlock.FirstCodePart() == ownFirstCodePart) return false;
            if (!ladderBlock.CanPlaceBlock(world, byPlayer, new BlockSelection() { Position = abovePos, Face = face }, ref useless)) return false;

            ladderBlock.DoPlaceBlock(world, byPlayer, new BlockSelection() { Position = abovePos, Face = face }, itemstack);
            return true;
        }

        private bool TryStackDown(IPlayer byPlayer, IWorldAccessor world, BlockPos pos, BlockFacing face, ItemStack itemstack)
        {
            Block ladderBlock = world.BlockAccessor.GetBlock(pos);
            if (ladderBlock.FirstCodePart() != ownFirstCodePart) return false;

            BlockPos belowPos = pos.DownCopy();
            Block belowBlock = null;

            while (belowPos.Y > 0)
            {
                belowBlock = world.BlockAccessor.GetBlock(belowPos);
                if (belowBlock.FirstCodePart() != ownFirstCodePart) break;
                
                belowPos.Down();
            }

            string useless = "";

            if (belowBlock == null || belowBlock.FirstCodePart() == ownFirstCodePart) return false;
            if (!belowBlock.IsReplacableBy(block)) return false;
            if (!ladderBlock.CanPlaceBlock(world, byPlayer, new BlockSelection() { Position = belowPos, Face = face }, ref useless)) return false;

            ladderBlock.DoPlaceBlock(world, byPlayer, new BlockSelection() { Position = belowPos, Face = face }, itemstack);
            return true;
        }
        



        public override void OnNeighbourBlockChange(IWorldAccessor world, BlockPos pos, BlockPos neibpos, ref EnumHandling handling)
        {
            if (!HasSupport(block, world.BlockAccessor, pos))
            {
                handling = EnumHandling.PreventSubsequent;
                world.BlockAccessor.BreakBlock(pos, null);
                world.BlockAccessor.TriggerNeighbourBlockUpdate(pos);
                return;
            }

            base.OnNeighbourBlockChange(world, pos, neibpos, ref handling);
        }



        public bool HasSupportUp(Block forBlock, IBlockAccessor blockAccess, BlockPos pos)
        {
            BlockFacing ownFacing = BlockFacing.FromCode(forBlock.LastCodePart());

            BlockPos upPos = pos.UpCopy();

            return
                SideSolid(blockAccess, pos, ownFacing)
                || SideSolid(blockAccess, upPos, BlockFacing.UP)
                || (pos.Y < blockAccess.MapSizeY - 1 && blockAccess.GetBlock(upPos) == forBlock && HasSupportUp(forBlock, blockAccess, upPos))
            ;
        }


        public bool HasSupportDown(Block forBlock, IBlockAccessor blockAccess, BlockPos pos)
        {
            BlockFacing ownFacing = BlockFacing.FromCode(forBlock.LastCodePart());

            BlockPos downPos = pos.DownCopy();

            return
                SideSolid(blockAccess, pos, ownFacing)
                || SideSolid(blockAccess, downPos, BlockFacing.DOWN)
                || (pos.Y > 0 && blockAccess.GetBlock(downPos) == forBlock && HasSupportDown(forBlock, blockAccess, downPos))
            ;
        }

        public bool HasSupport(Block forBlock, IBlockAccessor blockAccess, BlockPos pos)
        {
            BlockFacing ownFacing = BlockFacing.FromCode(forBlock.LastCodePart());

            BlockPos downPos = pos.DownCopy();
            BlockPos upPos = pos.UpCopy();

            return
                SideSolid(blockAccess, pos, ownFacing)
                || SideSolid(blockAccess, downPos, BlockFacing.DOWN)
                || SideSolid(blockAccess, upPos, BlockFacing.UP)
                || (pos.Y < blockAccess.MapSizeY - 1 && blockAccess.GetBlock(upPos) == forBlock && HasSupportUp(forBlock, blockAccess, upPos))
                || (pos.Y > 0 && blockAccess.GetBlock(downPos) == forBlock && HasSupportDown(forBlock, blockAccess, downPos))
            ;
        }

        public bool SideSolid(IBlockAccessor blockAccess, BlockPos pos, BlockFacing facing)
        {
            return blockAccess.GetBlock(pos.X + facing.Normali.X, pos.Y, pos.Z + facing.Normali.Z).CanAttachBlockAt(blockAccess, block, pos.AddCopy(facing), facing.Opposite);
        }



        

        public override void Initialize(JsonObject properties)
        {
            base.Initialize(properties);
            if (properties["dropBlockFace"].Exists)
            {
                dropBlockFace = properties["dropBlockFace"].AsString();
            }
        }
        

        public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, ref float dropQuantityMultiplier, ref EnumHandling handled)
        {
            handled = EnumHandling.PreventDefault;
            return new ItemStack[] { new ItemStack(world.BlockAccessor.GetBlock(block.CodeWithParts(dropBlockFace))) };
        }

        public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos, ref EnumHandling handled)
        {
            handled = EnumHandling.PreventDefault;

            return new ItemStack(world.BlockAccessor.GetBlock(block.CodeWithParts(dropBlockFace)));
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
                return block.CodeWithParts(facing.Opposite.Code);
            }
            return block.Code;
        }


    }
}
