using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class BlockBehaviorLadder : BlockBehavior
    {
        string dropBlockFace = "north";
        string ownFirstCodePart;
        public bool isFlexible;

        public string LadderType => block.Variant["material"];

        public BlockBehaviorLadder(Block block) : base(block)
        {
            ownFirstCodePart = block.FirstCodePart();
        }

        public override void Initialize(JsonObject properties)
        {
            base.Initialize(properties);
            if (properties["dropBlockFace"].Exists)
            {
                dropBlockFace = properties["dropBlockFace"].AsString();
            }

            isFlexible = properties["isFlexible"].AsBool(false);
        }



        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ref EnumHandling handling)
        {
            if (isFlexible && !byPlayer.Entity.Controls.ShiftKey)
            {
                TryCollectLowest(byPlayer, world, blockSel.Position);
                handling = EnumHandling.PreventDefault;
                return true;
            }

            return base.OnBlockInteractStart(world, byPlayer, blockSel, ref handling);
        }

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling, ref EnumHandling handling)
        {
            base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handHandling, ref handling);
        }

        public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel, ref EnumHandling handled, ref string failureCode)
        {
            handled = EnumHandling.PreventDefault;

            if (isFlexible && !byPlayer.Entity.Controls.ShiftKey)
            {
                failureCode = "sneaktoplace";
                return false;
            }

            BlockPos pos = blockSel.Position;
            BlockPos aimedAtPos = blockSel.DidOffset ? pos.AddCopy(blockSel.Face.Opposite) : blockSel.Position;
            
            
                
            // Has ladder above at aimed position?
            Block aboveBlock = world.BlockAccessor.GetBlock(pos.UpCopy());
            string aboveLadderType = aboveBlock.GetBehavior<BlockBehaviorLadder>()?.LadderType;

            if (!isFlexible && aboveLadderType == LadderType && HasSupport(aboveBlock, world.BlockAccessor, pos) && aboveBlock.CanPlaceBlock(world, byPlayer, blockSel, ref failureCode))
            {
                aboveBlock.DoPlaceBlock(world, byPlayer, blockSel, itemstack);
                return true;
            }

            // Has ladder below at aimed position?
            Block belowBlock = world.BlockAccessor.GetBlock(pos.DownCopy());
            string belowLadderType = belowBlock.GetBehavior<BlockBehaviorLadder>()?.LadderType;

            if (belowLadderType == LadderType && HasSupport(belowBlock, world.BlockAccessor, pos) && belowBlock.CanPlaceBlock(world, byPlayer, blockSel, ref failureCode))
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


            AssetLocation blockCode;

            if (isFlexible && blockSel.Face.IsVertical)
            {
                failureCode = "cantattachladder";
                return false;
            }

            if (blockSel.Face.IsVertical)
            {
                BlockFacing[] faces = Block.SuggestedHVOrientation(byPlayer, blockSel);
                blockCode = block.CodeWithParts(faces[0].Code);
            } else
            {
                blockCode = block.CodeWithParts(blockSel.Face.Opposite.Code);
            }

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



        protected bool TryStackUp(IPlayer byPlayer, IWorldAccessor world, BlockPos pos, BlockFacing face, ItemStack itemstack)
        {
            if (isFlexible) return false;

            Block ladderBlock = world.BlockAccessor.GetBlock(pos);
            string ladderType = ladderBlock.GetBehavior<BlockBehaviorLadder>()?.LadderType;
            if (ladderType != LadderType) return false;

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

            BlockPos neibPos = new BlockPos(pos.dimension);
            foreach (BlockFacing facing in BlockFacing.ALLFACES)
            {
                neibPos.Set(abovePos).Offset(facing);
                world.BlockAccessor.GetBlock(neibPos).OnNeighbourBlockChange(world, neibPos, abovePos);
            }

            return true;
        }

        protected bool TryStackDown(IPlayer byPlayer, IWorldAccessor world, BlockPos pos, BlockFacing face, ItemStack itemstack)
        {
            Block ladderBlock = world.BlockAccessor.GetBlock(pos);
            string ladderType = ladderBlock.GetBehavior<BlockBehaviorLadder>()?.LadderType;
            if (ladderType != LadderType) return false;

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

            BlockPos neibPos = new BlockPos(pos.dimension);
            foreach (BlockFacing facing in BlockFacing.ALLFACES)
            {
                neibPos.Set(belowPos).Offset(facing);
                world.BlockAccessor.GetBlock(neibPos).OnNeighbourBlockChange(world, neibPos, belowPos);
            }

            return true;
        }
        
        protected bool TryCollectLowest(IPlayer byPlayer, IWorldAccessor world, BlockPos pos)
        {
            Block ladderBlock = world.BlockAccessor.GetBlock(pos);
            if (ladderBlock.FirstCodePart() != ownFirstCodePart) return false;

            BlockPos belowPos = pos.DownCopy();
            Block belowBlock;

            while (belowPos.Y > 0)
            {
                belowBlock = world.BlockAccessor.GetBlock(belowPos);
                if (belowBlock.FirstCodePart() != ownFirstCodePart) break;

                belowPos.Down();
            }

            belowPos.Up();
            Block collectBlock = world.BlockAccessor.GetBlock(belowPos);

            var bh = collectBlock.GetBehavior<BlockBehaviorLadder>();
            if (bh == null || !bh.isFlexible) return false;

            if (!world.Claims.TryAccess(byPlayer, belowPos, EnumBlockAccessFlags.BuildOrBreak))
            {
                return false;
            }

            ItemStack[] stacks = collectBlock.GetDrops(world, pos, byPlayer);

            world.BlockAccessor.SetBlock(0, belowPos);
            world.PlaySoundAt(collectBlock.Sounds.Break, pos, 0, byPlayer);

            if (stacks.Length > 0)
            {
                if (!byPlayer.InventoryManager.TryGiveItemstack(stacks[0], true))
                {
                    world.SpawnItemEntity(stacks[0], byPlayer.Entity.Pos.XYZ);
                }
            }
            

            return true;
        }



        public override void OnNeighbourBlockChange(IWorldAccessor world, BlockPos pos, BlockPos neibpos, ref EnumHandling handling)
        {
            if (!HasSupport(block, world.BlockAccessor, pos))
            {
                handling = EnumHandling.PreventSubsequent;
                world.BlockAccessor.BreakBlock(pos, null);
                world.BlockAccessor.TriggerNeighbourBlockUpdate(pos.Copy());
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
                || (!isFlexible && SideSolid(blockAccess, downPos, BlockFacing.DOWN))
                || SideSolid(blockAccess, upPos, BlockFacing.UP)
                || (pos.Y < blockAccess.MapSizeY - 1 && blockAccess.GetBlock(upPos) == forBlock && HasSupportUp(forBlock, blockAccess, upPos))
                || (!isFlexible && pos.Y > 0 && blockAccess.GetBlock(downPos) == forBlock && HasSupportDown(forBlock, blockAccess, downPos))
            ;
        }

        public bool SideSolid(IBlockAccessor blockAccess, BlockPos pos, BlockFacing facing)
        {
            //public static readonly BlockFacing[] HORIZONTALS_ANGLEORDER = new BlockFacing[] { EAST, NORTH, WEST, SOUTH };
            // North: Negative Z
            // East: Positive X
            // South: Positive Z
            // West: Negative X
            // Up: Positive Y
            // Down: Negative Y

            Block neibBlock = blockAccess.GetBlock(pos.X + facing.Normali.X, pos.InternalY, pos.Z + facing.Normali.Z);

            Cuboidi upHalf = new Cuboidi(14, 0, 0, 15, 7, 15).RotatedCopy(0, 90 * facing.HorizontalAngleIndex, 0, new Vec3d(7.5, 0, 7.5));
            Cuboidi downHalf = new Cuboidi(14, 8, 0, 15, 15, 15).RotatedCopy(0, 90 * facing.HorizontalAngleIndex, 0, new Vec3d(7.5, 0, 7.5));

            return 
                neibBlock.CanAttachBlockAt(blockAccess, neibBlock, pos.AddCopy(facing), facing.Opposite, upHalf) ||
                neibBlock.CanAttachBlockAt(blockAccess, neibBlock, pos.AddCopy(facing), facing.Opposite, downHalf)
            ;
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
