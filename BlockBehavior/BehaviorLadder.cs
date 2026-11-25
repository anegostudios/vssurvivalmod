using Vintagestory.API;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.GameContent
{

    /// <summary>
    /// Used to control ladder behavior, including horizontal orientation, placement, collection. Note that this does not control how entities react to ladders (see "climbable" in blocktype).
    /// Requires use of the 'horizontalorientation' variants.
    /// Uses the code "Ladder".
    /// </summary>
    /// <example><code lang="json">
    ///"behaviors": [
	///	{
	///		"name": "Ladder",
	///		"properties": {
	///			"isFlexibleByType": {
	///				"ladder-rope-*": true
	///			}
	///		}
	///	}
	///]
    /// </code>
    /// <code>
    ///"variantgroups": [
	///	{
	///		"code": "side",
	///		"loadFromProperties": "abstract/horizontalorientation"
	///	}
	///]
    /// </code></example>
    [DocumentAsJson]
    public class BlockBehaviorLadder : BlockBehavior
    {

        /// <summary>
        /// What face variant should this block drop when mined?
        /// </summary>
        [DocumentAsJson("Optional", "north")]
        string dropBlockFace = "north";

        /// <summary>
        /// Can the base of this ladder be collected with a right click? Flexible ladders also cannot be built upwards.
        /// </summary>
        [DocumentAsJson("Optional", "False")]
        public bool isFlexible;

        /// <summary>
        /// Crude ladders cannot stand on the floor or ceiling without some attachment to a wall behind some part of the ladder.
        /// </summary>
        [DocumentAsJson("Optional", "False")]
        public bool isCrude;

        public string LadderType => block.Variant["material"];

        public BlockBehaviorLadder(Block block) : base(block)
        {

        }

        public override void Initialize(JsonObject properties)
        {
            base.Initialize(properties);

            dropBlockFace = properties["dropBlockFace"].AsString("north");
            isFlexible = properties["isFlexible"].AsBool(false);
            isCrude = properties["isCrude"].AsBool(false);
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
            bool placed = false;

            // Am I aiming at the bottom half of the block?
            if (blockSel.HitPosition.Y < 0.5)
            {
                placed = TryStackDown(byPlayer, world, aimedAtPos, blockSel.Face, itemstack);
            }

            // Can I put ladder above block position?
            placed = placed || TryStackUp(byPlayer, world, aimedAtPos, blockSel.Face, itemstack);

            // Can I put ladder below block position?
            placed = placed || TryStackDown(byPlayer, world, aimedAtPos, blockSel.Face, itemstack);

            if (placed) return true;

            AssetLocation blockCode;

            if (blockSel.Face.IsVertical)
            {
                BlockFacing[] faces = Block.SuggestedHVOrientation(byPlayer, blockSel);
                blockCode = block.CodeWithParts(faces[0].Code);
            }
            else
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

            failureCode = "cantattachladder";
            return false;
        }



        protected bool TryStackUp(IPlayer byPlayer, IWorldAccessor world, BlockPos pos, BlockFacing face, ItemStack itemstack)
        {
            if (isFlexible) return false;

            Block ladderBlock = world.BlockAccessor.GetBlock(pos);
            string ladderType = ladderBlock.GetBehavior<BlockBehaviorLadder>()?.LadderType;
            ladderBlock = world.BlockAccessor.GetBlock(itemstack.Block?.CodeWithVariant("side", ladderBlock.Variant["side"]) ?? "") ?? ladderBlock;
            if (ladderType != LadderType) return false;

            BlockPos abovePos = pos.UpCopy();
            Block aboveBlock = null;

            while (abovePos.Y < world.BlockAccessor.MapSizeY)
            {
                aboveBlock = world.BlockAccessor.GetBlock(abovePos);
                if (aboveBlock.GetBehavior<BlockBehaviorLadder>()?.LadderType != LadderType) break;
                
                abovePos.Up();
            }

            string useless="";

            if (aboveBlock == null) return false;
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
            ladderBlock = world.BlockAccessor.GetBlock(itemstack.Block?.CodeWithVariant("side", ladderBlock.Variant["side"]) ?? "") ?? ladderBlock;
            if (ladderType != LadderType) return false;

            BlockPos belowPos = pos.DownCopy();
            Block belowBlock = null;

            while (belowPos.Y > 0)
            {
                belowBlock = world.BlockAccessor.GetBlock(belowPos);
                if (belowBlock.GetBehavior<BlockBehaviorLadder>()?.LadderType != LadderType) break;

                belowPos.Down();
            }

            string useless = "";

            if (belowBlock == null) return false;
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
            BlockPos belowPos = pos.DownCopy();
            Block belowBlock;

            while (belowPos.Y > 0)
            {
                belowBlock = world.BlockAccessor.GetBlock(belowPos);
                if (belowBlock.GetBehavior<BlockBehaviorLadder>()?.LadderType != LadderType) break;

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
            Block upBlock = pos.Y < blockAccess.MapSizeY - 1 ? blockAccess.GetBlock(upPos) : null;
            string upLadderType = upBlock?.GetBehavior<BlockBehaviorLadder>()?.LadderType;

            return
                SideSolid(blockAccess, pos, ownFacing)
                || (!isCrude && SideSolid(blockAccess, pos, BlockFacing.UP))
                || (upLadderType != null && upLadderType == forBlock.GetBehavior<BlockBehaviorLadder>()?.LadderType && HasSupportUp(upBlock, blockAccess, upPos))
            ;
        }


        public bool HasSupportDown(Block forBlock, IBlockAccessor blockAccess, BlockPos pos)
        {
            BlockFacing ownFacing = BlockFacing.FromCode(forBlock.LastCodePart());

            BlockPos downPos = pos.DownCopy();
            Block downBlock = pos.Y > 0 ? blockAccess.GetBlock(downPos) : null;
            string downLadderType = downBlock?.GetBehavior<BlockBehaviorLadder>()?.LadderType;

            return
                SideSolid(blockAccess, pos, ownFacing)
                || (!isCrude && SideSolid(blockAccess, pos, BlockFacing.DOWN))
                || (downLadderType != null && downLadderType == forBlock.GetBehavior<BlockBehaviorLadder>()?.LadderType && HasSupportDown(downBlock, blockAccess, downPos))
            ;
        }

        public bool HasSupport(Block forBlock, IBlockAccessor blockAccess, BlockPos pos)
        {
            BlockFacing ownFacing = BlockFacing.FromCode(forBlock.LastCodePart());
            string ladderType = forBlock.GetBehavior<BlockBehaviorLadder>()?.LadderType;

            BlockPos downPos = pos.DownCopy();
            Block downBlock = pos.Y > 0 ? blockAccess.GetBlock(downPos) : null;
            string downLadderType = downBlock?.GetBehavior<BlockBehaviorLadder>()?.LadderType;

            BlockPos upPos = pos.UpCopy();
            Block upBlock = pos.Y < blockAccess.MapSizeY - 1 ? blockAccess.GetBlock(upPos) : null;
            string upLadderType = upBlock?.GetBehavior<BlockBehaviorLadder>()?.LadderType;

            return
                SideSolid(blockAccess, pos, ownFacing)
                || (!isFlexible && !isCrude && SideSolid(blockAccess, pos, BlockFacing.DOWN))
                || (!isCrude && SideSolid(blockAccess, pos, BlockFacing.UP))
                || (upLadderType != null && upLadderType == ladderType && HasSupportUp(forBlock, blockAccess, upPos))
                || (!isFlexible && downLadderType != null && downLadderType == ladderType && HasSupportDown(downBlock, blockAccess, downPos))
            ;
        }


        // Calculate the top and bottom halves of blocks in voxels only once to reuse
        static readonly Cuboidi[] upHalf = [new(0, 0, 0, 14, 7, 1), new(13, 0, 0, 15, 7, 15), new(0, 0, 13, 15, 7, 15), new(0, 0, 0, 1, 7, 15)];
        static readonly Cuboidi[] downHalf = [new(0, 8, 0, 14, 15, 1), new(13, 8, 0, 15, 15, 15), new(0, 8, 13, 15, 15, 15), new(0, 8, 0, 1, 15, 15)];

        public bool SideSolid(IBlockAccessor blockAccess, BlockPos pos, BlockFacing facing)
        {
            BlockPos neibPos = pos.AddCopy(facing);
            Block neibBlock = blockAccess.GetBlock(neibPos);
            if (neibBlock.Id == 0) return false;

            // If it is a vertical face we were always checking the full block anyway,
            if (facing.IsVertical)
            {
                if (neibBlock.CanAttachBlockAt(blockAccess, neibBlock, neibPos, facing.Opposite)) return true;
            }
            else
            {
                if (neibBlock.CanAttachBlockAt(blockAccess, neibBlock, neibPos, facing.Opposite, upHalf[facing.Index])) return true;
                if (neibBlock.CanAttachBlockAt(blockAccess, neibBlock, neibPos, facing.Opposite, downHalf[facing.Index])) return true;
            }

            return false;
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
