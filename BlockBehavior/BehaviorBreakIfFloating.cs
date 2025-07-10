﻿using Vintagestory.API;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

#nullable disable

namespace Vintagestory.GameContent
{
    /// <summary>
    /// Forces the Block to drop as an item when surrounded by air blocks. It will override drops returned by the Block
    /// when this happens.
    /// Uses the code "BreakIfFloating". This behavior doesn't use any properties.
    /// </summary>
    /// <example>
    /// <code lang="json">
    ///"behaviors": [
	///	{ "name": "BreakIfFloating" }
	///]
    ///</code></example>
    [DocumentAsJson]
    public class BlockBehaviorBreakIfFloating : BlockBehavior
    {
        public bool AllowFallingBlocks;

        public BlockBehaviorBreakIfFloating(Block block) : base(block)
        {
        }

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            AllowFallingBlocks = api.World.Config.GetBool("allowFallingBlocks");
        }

        public override void OnNeighbourBlockChange(IWorldAccessor world, BlockPos pos, BlockPos neibpos, ref EnumHandling handled)
        {
            if (world.Side == EnumAppSide.Client) return;
            if (!AllowFallingBlocks) return;

            handled = EnumHandling.PassThrough;

            if (IsSurroundedByNonSolid(world, pos))
            {
                world.BlockAccessor.BreakBlock(pos, null);
            }
            base.OnNeighbourBlockChange(world, pos, neibpos, ref handled);
        }

        public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, ref float dropQuantityMultiplier, ref EnumHandling handled)
        {
            if(IsSurroundedByNonSolid(world, pos))
            {
                handled = EnumHandling.PreventSubsequent;
                return new ItemStack[] { new ItemStack(block) };
            }
            else
            {
                handled = EnumHandling.PassThrough;
                return null;
            }
        }

        public override void OnBlockBroken(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, ref EnumHandling handling)
        {
            handling = EnumHandling.PassThrough;
            // Whats this good for? it prevents breaking of stone blocks o.o
            /*if (IsSurroundedByNonSolid(world, pos) && byPlayer != null && byPlayer.WorldData.CurrentGameMode != EnumGameMode.Creative)
            {
                handling = EnumHandling.PreventSubsequent;
            }*/
        }

        public bool IsSurroundedByNonSolid(IWorldAccessor world, BlockPos pos)
        {
            foreach(BlockFacing facing in BlockFacing.ALLFACES)
            {
                if (world.BlockAccessor.IsSideSolid(pos.X + facing.Normali.X, pos.InternalY + facing.Normali.Y, pos.Z + facing.Normali.Z, facing.Opposite)) return false;
            }
            return true;
        }
    }
}
