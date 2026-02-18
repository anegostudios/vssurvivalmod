using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Vintagestory.API;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

#nullable disable

namespace Vintagestory.GameContent
{

    /// <summary>
    /// Used to create a liquid which distrubutes itself over an area, and has interaction with other liquids.
    /// Uses the "FiniteSpreadingLiquid" code.
    /// </summary>
    /// <example><code lang="json">
    ///"behaviors": [
	///	{
	///		"name": "FiniteSpreadingLiquid",
	///		"properties": {
	///			"spreadDelay": 125,
	///			"liquidCollisionSound": "sizzle",
	///			"sourceReplacementCode": "rock-obsidian",
	///			"flowingReplacementCode": "rock-basalt",
	///			"collidesWith": "lava"
	///		}
	///	}
	///]
    /// </code></example>
    [DocumentAsJson]
    public class BlockBehaviorFiniteSpreadingLiquid : BlockBehavior
    {
        private const int MAXLEVEL = 7;
        private const float MAXLEVEL_float = MAXLEVEL;
        public static Vec2i[] downPaths = ShapeUtil.GetSquarePointsSortedByMDist(3);

        public static SimpleParticleProperties steamParticles;

        public static int ReplacableThreshold = 5000;

        /// <summary>
        /// The sound to play when a liquid collision causes blocks to be replaced
        /// </summary>
        [DocumentAsJson]
        protected AssetLocation collisionReplaceSound;

        /// <summary>
        /// Controls how fast the liquid spreads
        /// </summary>
        [DocumentAsJson("Recommended", "150")]
        protected int spreadDelay = 150;

        /// <summary>
        /// The liquid this one can collide with
        /// </summary>
        [DocumentAsJson("Optional", "None")]
        protected string collidesWith;

        /// <summary>
        /// Block code to replace the block with when colliding with the source block of a different liquid
        /// </summary>
        [DocumentAsJson("Optional", "None")]
        protected AssetLocation liquidSourceCollisionReplacement;

        /// <summary>
        /// Block code to replace the block with when colliding with a flowing block of a different liquid
        /// </summary>
        [DocumentAsJson("Optional", "None")]
        protected AssetLocation liquidFlowingCollisionReplacement;

        /// <summary>
        /// If true, can spread in multiple directions at once. On by default
        /// </summary>
        [DocumentAsJson("Optional", "None")]
        public bool multiplySpread = true;

        protected bool carver;
        [ThreadStatic] protected static IBlockAccessor bulkBlockAccessorMinimal;    // Used by rivulets carver; ThreadStatic will automatically be disposed when the thread is disposed

        public BlockBehaviorFiniteSpreadingLiquid(Block block) : base(block)
        {
        }

        BlockFacing[] tmpFacings = null;
        public override void Initialize(JsonObject properties)
        {
            base.Initialize(properties);

            spreadDelay = properties["spreadDelay"].AsInt();
            collisionReplaceSound = CreateAssetLocation(properties, "sounds/", "liquidCollisionSound");
            liquidSourceCollisionReplacement = CreateAssetLocation(properties, "sourceReplacementCode");
            liquidFlowingCollisionReplacement = CreateAssetLocation(properties, "flowingReplacementCode");
            collidesWith = properties["collidesWith"]?.AsString();
            multiplySpread = properties["multiplySpread"].AsBool(true);
            if (!multiplySpread)
            {
                tmpFacings = (BlockFacing[])BlockFacing.HORIZONTALS.Clone();
            }

            carver = properties["carver"].AsBool(false);
        }

        public override bool DoPlaceBlock(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ItemStack byItemStack, ref EnumHandling handling)
        {
            if (world is IServerWorldAccessor)
            {
                world.RegisterCallbackUnique(OnDelayedWaterUpdateCheck, blockSel.Position, spreadDelay);
            }

            return base.DoPlaceBlock(world, byPlayer, blockSel, byItemStack, ref handling);
        }

        public override void OnNeighbourBlockChange(IWorldAccessor world, BlockPos pos, BlockPos neibpos, ref EnumHandling handled)
        {
            handled = EnumHandling.PreventDefault;

            if (world is IServerWorldAccessor)
            {
                world.RegisterCallbackUnique(OnDelayedWaterUpdateCheck, pos, spreadDelay);
            }
        }

        private void OnDelayedWaterUpdateCheck(IWorldAccessor world, BlockPos pos, float dt)
        {
            if (carver)
            {
                if (world.Side is EnumAppSide.Server)
                {
                    bulkBlockAccessorMinimal ??= world.GetBlockAccessorBulkMinimalUpdate(true, false);

                    var waterblock = world.GetBlock(AssetLocation.Create(this.block.Variant["type"] + "-still-7", this.block.Code.Domain));
                    bulkBlockAccessorMinimal.SetBlock(waterblock.Id, pos, BlockLayersAccess.Fluid); // Don't use BulkBlockAccessor here. Creates endless loops

                    waterblock.GetBehavior<BlockBehaviorFiniteSpreadingLiquid>().carveRivulet(world, bulkBlockAccessorMinimal, pos);
                    bulkBlockAccessorMinimal.Commit();
                }
                return;
            }

            SpreadAndUpdateLiquidLevels(world, pos);
            world.BulkBlockAccessor.Commit();

            Block block = world.BlockAccessor.GetBlock(pos, BlockLayersAccess.Fluid);
            if (block.HasBehavior<BlockBehaviorFiniteSpreadingLiquid>()) updateOwnFlowDir(block, world, pos);

            // Possibly overkill? But should ensure everything is correct
            BlockPos npos = pos.Copy();
            foreach (var val in Cardinal.ALL)
            {
                npos.Set(pos.X + val.Normali.X, pos.Y, pos.Z + val.Normali.Z);
                Block neib = world.BlockAccessor.GetBlock(npos, BlockLayersAccess.Fluid);
                if (neib.HasBehavior<BlockBehaviorFiniteSpreadingLiquid>()) updateOwnFlowDir(neib, world, npos);
            }
        }

        public class PathPos : BlockPos
        {
            public BlockPos PrevPos;

            public PathPos(int x, int y, int z) : base(x, y, z) { }
            public PathPos(BlockPos pos) : base(pos.X, pos.Y, pos.Z) { }

            public override PathPos Copy()
            {
                return new PathPos(X, Y, Z)
                {
                    PrevPos = PrevPos
                };
            }

            public BlockPos PositionCopy => new BlockPos(X, Y, Z);
        }

        protected PathPos findCarvePath(IWorldAccessor world, BlockPos startPos, Block ourBlock)
        {
            Queue<PathPos> uncheckedPositions = new Queue<PathPos>();
            HashSet<PathPos> checkedPositions = new HashSet<PathPos>();
            uncheckedPositions.Enqueue(new PathPos(startPos.X, startPos.Y, startPos.Z));

            BlockPos npos = new BlockPos(startPos.dimension);
            PathPos pos;

            int maxDist = 7;
            while (uncheckedPositions.Count > 0)
            {
                pos = uncheckedPositions.Dequeue();

                checkedPositions.Add(pos);

                int dist = pos.ManhattanDistance(startPos);
                if (dist > maxDist) continue;

                npos.Set(pos);
                for (int i = 0; i < BlockFacing.HORIZONTALS.Length; i++)
                {
                    BlockFacing.HORIZONTALS[i].IterateThruFacingOffsets(npos);

                    Block block = world.BlockAccessor.GetMostSolidBlock(npos);
                    if (block.GetLiquidBarrierHeightOnSide(BlockFacing.HORIZONTALS[i].Opposite, npos) >= (ourBlock.LiquidLevel - pos.ManhattanDistance(startPos)) / MAXLEVEL_float) continue;

                    var ppos = new PathPos(npos) { PrevPos = pos.Copy() };

                    if (!(world as IServerWorldAccessor).IsFullyLoadedChunk(pos)) return null;
                    if (dist > 0 && world.BlockAccessor.GetMostSolidBlock(npos.UpCopy()).SideSolid.Any) continue;
                    if (!world.BlockAccessor.GetMostSolidBlock(npos.DownCopy()).SideSolid.Any) return ppos;

                    if (checkedPositions.Contains(npos)) continue;
                    uncheckedPositions.Enqueue(ppos);
                }
            }

            return null;
        }

        private void carveRivulet(IWorldAccessor world, IBlockAccessor blockAccessor, BlockPos pos)
        {
            var liquidLevel = block.LiquidLevel;
            if (liquidLevel == 0) return;

            var pathpos = findCarvePath(world, pos, block);
            if (pathpos == null) return;

            List<BlockPos> path = [pathpos];
            while (pathpos.PrevPos != null)
            {
                pathpos = (PathPos)pathpos.PrevPos;
                path.Add(pathpos);
            }
            path.Reverse();

            var lblock = block;
            for (int i = 1; i < path.Count; i++)
            {
                pos = path[i];
                if (i == 1)
                {
                    if (!placeRiver(world, blockAccessor, pos, ref lblock)) return;
                }

                pos.Down();
                var belowBlock = world.BlockAccessor.GetBlock(pos);
                while (pos.Y > 0 && !belowBlock.SideSolid.Any)
                {
                    blockAccessor.SetBlock(0, pos);
                    blockAccessor.SetBlock(world.GetBlock(block.CodeWithParts("d", "" + 6)).BlockId, pos, BlockLayersAccess.Fluid);
                    belowBlock = world.BlockAccessor.GetBlock(pos.Down());
                }
                
                lblock = world.Blocks[GetLiquidBlockId(world, pos, lblock, 6)];
                if (!placeRiver(world, blockAccessor, pos, ref lblock)) return;
            }

            lblock.GetBehavior<BlockBehaviorFiniteSpreadingLiquid>().carveRivulet(world, blockAccessor, pos);
        }

        private bool placeRiver(IWorldAccessor world, IBlockAccessor blockAccessor, BlockPos pos, ref Block lblock)
        {
            blockAccessor.SetBlock(0, pos);
            blockAccessor.SetBlock(0, pos.UpCopy());

            var belowPos = pos.DownCopy();
            var belowBlock = world.BlockAccessor.GetBlock(belowPos);
            while (belowPos.Y > 0 && !belowBlock.SideSolid.Any) belowBlock = world.BlockAccessor.GetBlock(belowPos.Down());
            blockAccessor.SetBlock(world.GetBlock(new AssetLocation("muddygravel")).Id, belowPos);

            lblock = world.Blocks[GetLessLiquidBlockId(world, pos, lblock)];
            if (lblock.Id == 0) return false;
            blockAccessor.SetBlock(lblock.Id, pos, BlockLayersAccess.Fluid);
            return true;
        }

        private void SpreadAndUpdateLiquidLevels(IWorldAccessor world, BlockPos pos)
        {
            // Slightly weird hack 
            // 1. We call this method also for other blocks, so can't rely on this.block
            // 2. our own liquid level might have changed from other nearby liquid sources
            Block ourBlock = world.BlockAccessor.GetBlock(pos, BlockLayersAccess.Fluid);

            int liquidLevel = ourBlock.LiquidLevel;
            if (liquidLevel > 0)
            {
                // Lower liquid if not connected to source block
                if (!TryLoweringLiquidLevel(ourBlock, world, pos)) 
                {
                    // nasty slow check, but supports chiselled blocks for good physics
                    var dSolid = world.BlockAccessor.GetMostSolidBlock(pos.DownCopy());
                    Block ourSolid = world.BlockAccessor.GetBlock(pos, BlockLayersAccess.SolidBlocks);
                    
                    bool onSolidGround = dSolid.GetLiquidBarrierHeightOnSide(BlockFacing.UP, pos.DownCopy()) == 1.0 ||
                                         ourSolid.GetLiquidBarrierHeightOnSide(BlockFacing.DOWN, pos) == 1.0;

                    // First try spreading downwards, if not on solid ground
                    if ((onSolidGround || !TrySpreadDownwards(world, ourSolid, ourBlock, pos)) && liquidLevel > 1) // Can we still spread somewhere
                    {
                        List<PosAndDist> downwardPaths = FindDownwardPaths(world, pos, ourBlock);
                        if (downwardPaths.Count > 0) // Prefer flowing to downward paths rather than outward
                        {
                            FlowTowardDownwardPaths(downwardPaths, ourBlock, ourSolid, pos, world);
                        }
                        else
                        {
                            TrySpreadHorizontal(ourBlock, ourSolid, world, pos);

                            // Turn into water source block if surrounded by 3 other sources
                            if (!IsLiquidSourceBlock(ourBlock))
                            {
                                int nearbySourceBlockCount = CountNearbySourceBlocks(world.BlockAccessor, pos, ourBlock);

                                if (nearbySourceBlockCount >= 3 || (nearbySourceBlockCount == 2 && CountNearbyDiagonalSources(world.BlockAccessor, pos, ourBlock) >= 3))
                                {
                                    world.BlockAccessor.SetBlock(GetMoreLiquidBlockId(world, pos, ourBlock), pos, BlockLayersAccess.Fluid);

                                    BlockPos npos = pos.Copy();
                                    for (int i = 0; i < BlockFacing.HORIZONTALS.Length; i++)
                                    {
                                        BlockFacing.HORIZONTALS[i].IterateThruFacingOffsets(npos);
                                        Block nblock = world.BlockAccessor.GetBlock(npos, BlockLayersAccess.Fluid);
                                        if (nblock.HasBehavior<BlockBehaviorFiniteSpreadingLiquid>()) updateOwnFlowDir(nblock, world, npos);
                                    }
                                }
                            }
                        }
                    }
                }

                
            }
        }

        private int CountNearbySourceBlocks(IBlockAccessor blockAccessor, BlockPos pos, Block ourBlock)
        {
            BlockPos qpos = pos.Copy();
            int nearbySourceBlockCount = 0;
            for (int i = 0; i < BlockFacing.HORIZONTALS.Length; i++)
            {
                BlockFacing.HORIZONTALS[i].IterateThruFacingOffsets(qpos);
                Block nblock = blockAccessor.GetBlock(qpos, BlockLayersAccess.Fluid);
                if (IsSameLiquid(ourBlock, nblock) && IsLiquidSourceBlock(nblock)) nearbySourceBlockCount++;
            }
            return nearbySourceBlockCount;
        }

        private int CountNearbyDiagonalSources(IBlockAccessor blockAccessor, BlockPos pos, Block ourBlock)
        {
            BlockPos npos = pos.Copy();
            int nearbySourceBlockCount = 0;
            foreach (var val in Cardinal.ALL)
            {
                if (!val.IsDiagonal) continue;
                npos.Set(pos.X + val.Normali.X, pos.Y, pos.Z + val.Normali.Z);
                Block nblock = blockAccessor.GetBlock(npos, BlockLayersAccess.Fluid);
                if (IsSameLiquid(ourBlock, nblock) && IsLiquidSourceBlock(nblock)) nearbySourceBlockCount++;
            }
            return nearbySourceBlockCount;
        }

        private void FlowTowardDownwardPaths(List<PosAndDist> downwardPaths, Block liquidBlock, Block solidBlock, BlockPos pos, IWorldAccessor world)
        {
            if (!multiplySpread) GameMath.Shuffle(world.Rand, downwardPaths);

            foreach (PosAndDist pod in downwardPaths)
            {
                BlockFacing pathFacing = pod.pos.FacingFrom(pos);
                if (CanSpreadIntoBlock(liquidBlock, solidBlock, pos, pod.pos, pathFacing, world))
                {
                    Block neighborLiquid = world.BlockAccessor.GetBlock(pod.pos, BlockLayersAccess.Fluid);
                    if (CollidesWith(liquidBlock, neighborLiquid))
                    {
                        ReplaceLiquidBlock(neighborLiquid, pod.pos, world);
                    }
                    else
                    {
                        SpreadLiquid(GetLessLiquidBlockId(world, pod.pos, liquidBlock), pod.pos, world);
                    }

                    if (!multiplySpread)
                    {
                        RemoveOtherLowerNeighbours(liquidBlock, pos, pathFacing, world);
                        return;
                    }
                }
            }
        }

        private void RemoveOtherLowerNeighbours(Block liquidBlock, BlockPos pos, BlockFacing pathFacing, IWorldAccessor world)
        {
            for (int i = 0; i < 4; i++)
            {
                pos.IterateHorizontalOffsets(i);
                BlockFacing facing = BlockFacing.HORIZONTALS[i];
                if (facing == pathFacing) continue;
                Block neighborLiquid = world.BlockAccessor.GetBlock(pos, BlockLayersAccess.Fluid);
                if (neighborLiquid.IsLiquid() && neighborLiquid.LiquidLevel < liquidBlock.LiquidLevel && IsSameLiquid(liquidBlock, neighborLiquid))
                {
                    // Remove previous path
                    world.BulkBlockAccessor.SetBlock(0, pos, BlockLayersAccess.Fluid);
                    UpdateNeighbouringLiquids(pos, world);
                }
            }
            pos.East();  // Needed to finish the iteration and restore pos to initial value, as we end effectively with pos.West()
        }

        private bool TrySpreadDownwards(IWorldAccessor world, Block ourSolid, Block ourBlock, BlockPos pos)
        {
            BlockPos npos = pos.DownCopy();

            Block belowLiquid = world.BlockAccessor.GetBlock(npos, BlockLayersAccess.Fluid);
            if (CanSpreadIntoBlock(ourBlock, ourSolid, pos, npos, BlockFacing.DOWN, world))
            {
                if (CollidesWith(ourBlock, belowLiquid))
                {
                    ReplaceLiquidBlock(belowLiquid, npos, world);
                    TryFindSourceAndSpread(npos, world);
                }
                else
                {
                    bool fillWithSource = false;
                    // If the block above is a source, and either this has at least 1 horizontal neighbour which is a source, or the block above has at least 2 source neighbours
                    // and the block below here is solid ground or a source, then heal - we are in the middle of a lake or similar!)
                    if (IsLiquidSourceBlock(ourBlock))
                    {
                        if (CountNearbySourceBlocks(world.BlockAccessor, npos, ourBlock) > 1) fillWithSource = true;
                        else
                        {
                            npos.Down();
                            Block blockBelow = world.BlockAccessor.GetBlock(npos, BlockLayersAccess.MostSolid);
                            bool onSolidGround = blockBelow.GetLiquidBarrierHeightOnSide(BlockFacing.UP, npos) == 1.0 ||
                                                 ourSolid.GetLiquidBarrierHeightOnSide(BlockFacing.DOWN, pos) == 1.0;
                            if (onSolidGround || IsLiquidSourceBlock(world.BlockAccessor.GetBlock(npos, BlockLayersAccess.Fluid)))
                            {
                                int count = CountNearbySourceBlocks(world.BlockAccessor, pos, ourBlock);
                                fillWithSource = count >= 2;
                            }
                            npos.Up();
                        }
                    }
                    SpreadLiquid(fillWithSource ? ourBlock.BlockId : GetFallingLiquidBlockId(ourBlock, world), npos, world);
                    if (!multiplySpread)
                    {
                        RemoveOtherLowerNeighbours(ourBlock, pos, BlockFacing.DOWN, world);
                    }
                }

                return true;
            }

            return !IsLiquidSourceBlock(ourBlock) || !IsLiquidSourceBlock(belowLiquid);  // return false if this is water source above water source (then surface blocks of (>1 deep) lakes can spread sideways)
        }


        
        private void TrySpreadHorizontal(Block ourblock, Block ourSolid, IWorldAccessor world, BlockPos pos)
        {
            if (!multiplySpread)
            {
                GameMath.Shuffle(world.Rand, tmpFacings);
                foreach (BlockFacing facing in BlockFacing.HORIZONTALS)
                {
                    if (TrySpreadIntoBlock(ourblock, ourSolid, pos, pos.AddCopy(facing), facing, world))
                    {
                        return;
                    }
                }

                return;
            }           

            foreach (BlockFacing facing in BlockFacing.HORIZONTALS)
            {
                TrySpreadIntoBlock(ourblock, ourSolid, pos, pos.AddCopy(facing), facing, world);
            }
        }

        private void ReplaceLiquidBlock(Block liquidBlock, BlockPos pos, IWorldAccessor world)
        {
            Block replacementBlock = GetReplacementBlock(liquidBlock, world);
            if (replacementBlock != null)
            {
                world.BulkBlockAccessor.SetBlock(replacementBlock.BlockId, pos);
                world.BulkBlockAccessor.SetBlock(0, pos, BlockLayersAccess.Fluid);
                
                BlockBehaviorBreakIfFloating bh = replacementBlock.GetBehavior<BlockBehaviorBreakIfFloating>();
                if (bh != null && bh.IsSurroundedByNonSolid(world, pos))
                {
                    world.BulkBlockAccessor.SetBlock(replacementBlock.BlockId, pos.DownCopy());
                }

                UpdateNeighbouringLiquids(pos, world);
                GenerateSteamParticles(pos, world);
                world.PlaySoundAt(collisionReplaceSound, pos, 0, null, true, 16);
            }
        }

        private void SpreadLiquid(int blockId, BlockPos pos, IWorldAccessor world)
        {
            world.BulkBlockAccessor.SetBlock(blockId, pos, BlockLayersAccess.Fluid);
            if (blockId == 0) return;
            world.RegisterCallbackUnique(OnDelayedWaterUpdateCheck, pos, spreadDelay);

            Block ourBlock = world.GetBlock(blockId);
            TryReplaceNearbyLiquidBlocks(ourBlock, pos, world);
        }

        private void updateOwnFlowDir(Block block, IWorldAccessor world, BlockPos pos)
        {
            int blockId = GetLiquidBlockId(world, pos, block, block.LiquidLevel);
            if (block.BlockId != blockId)
            {
                world.BlockAccessor.SetBlock(blockId, pos, BlockLayersAccess.Fluid);
            }
        }


        /// <summary>
        /// Replaces nearby liquid if it's not the same as this liquid. Prevents lava and water from being adjacent blocks
        /// </summary>
        /// <param name="ourBlock"></param>
        /// <param name="pos"></param>
        /// <param name="world"></param>
        private void TryReplaceNearbyLiquidBlocks(Block ourBlock, BlockPos pos, IWorldAccessor world)
        {
            BlockPos npos = pos.Copy();
            foreach (BlockFacing facing in BlockFacing.HORIZONTALS)
            {
                facing.IterateThruFacingOffsets(npos);
                Block neib = world.BlockAccessor.GetBlock(npos, BlockLayersAccess.Fluid);
                if (CollidesWith(ourBlock, neib))
                {
                    ReplaceLiquidBlock(ourBlock, npos, world);
                }
            }
        }

        /// <summary>
        /// Traverses upward until the source liquid block is found and spreads outward from there.
        /// </summary>
        /// <param name="startingPos"></param>
        /// <param name="world"></param>
        /// <returns>True if the source liquid block was found, false otherwise</returns>
        private bool TryFindSourceAndSpread(BlockPos startingPos, IWorldAccessor world)
        {
            BlockPos sourceBlockPos = startingPos.UpCopy();
            Block sourceBlock = world.BlockAccessor.GetBlock(sourceBlockPos, BlockLayersAccess.Fluid);
            while (sourceBlock.IsLiquid())
            {
                if (IsLiquidSourceBlock(sourceBlock))
                {
                    Block ourSolid = world.BlockAccessor.GetBlock(sourceBlockPos, BlockLayersAccess.SolidBlocks);
                    TrySpreadHorizontal(sourceBlock, ourSolid, world, sourceBlockPos);
                    return true;
                }
                sourceBlockPos.Add(0, 1, 0);
                sourceBlock = world.BlockAccessor.GetBlock(sourceBlockPos, BlockLayersAccess.Fluid);
            }
            return false;
        }

        private void GenerateSteamParticles(BlockPos pos, IWorldAccessor world)
        {
            float minQuantity = 50;
            float maxQuantity = 100;
            int color = ColorUtil.ToRgba(100, 225, 225, 225);
            Vec3d minPos = new Vec3d();
            Vec3d addPos = new Vec3d();
            Vec3f minVelocity = new Vec3f(-0.25f, 0.1f, -0.25f);
            Vec3f maxVelocity = new Vec3f(0.25f, 0.1f, 0.25f);
            float lifeLength = 2.0f;
            float gravityEffect = -0.015f;
            float minSize = 0.1f;
            float maxSize = 0.1f;

            SimpleParticleProperties steamParticles = new SimpleParticleProperties(
                minQuantity, maxQuantity,
                color,
                minPos, addPos,
                minVelocity, maxVelocity,
                lifeLength,
                gravityEffect,
                minSize, maxSize,
                EnumParticleModel.Quad
            );
            steamParticles.Async = true;
            steamParticles.MinPos.Set(pos.ToVec3d().AddCopy(0.5, 1.1, 0.5));
            steamParticles.AddPos.Set(new Vec3d(0.5, 1.0, 0.5));
            steamParticles.SizeEvolve = new EvolvingNatFloat(EnumTransformFunction.LINEARINCREASE, 1.0f);
            world.SpawnParticles(steamParticles);
        }

        /// <summary>
        /// If any neighbours (up, down, and all horizontal neighbours including diagonals) are liquids, register a callback to update them, similar to triggering OnNeighbourBlockChange()
        /// </summary>
        private void UpdateNeighbouringLiquids(BlockPos pos, IWorldAccessor world)
        {
            world.BulkBlockAccessor.ReadFromStagedByDefault = true;
            // First do down and up, as they are not included in Cardinals
            BlockPos npos = pos.DownCopy();
            Block neib = world.BulkBlockAccessor.GetBlock(npos, BlockLayersAccess.Fluid);

            var bbfsl = neib.GetBehavior<BlockBehaviorFiniteSpreadingLiquid>();
            if (bbfsl != null) world.RegisterCallbackUnique(bbfsl.OnDelayedWaterUpdateCheck, npos.Copy(), spreadDelay);

            npos.Up(2);
            neib = world.BulkBlockAccessor.GetBlock(npos, BlockLayersAccess.Fluid);

            bbfsl = neib.GetBehavior<BlockBehaviorFiniteSpreadingLiquid>();
            if (bbfsl != null) world.RegisterCallbackUnique(bbfsl.OnDelayedWaterUpdateCheck, npos.Copy(), spreadDelay);
            npos.Down();

            // Now do all horizontal neighbours including the diagonals, because water blocks can have diagonal flow
            foreach (var val in Cardinal.ALL)
            {
                npos.Set(pos.X + val.Normali.X, pos.Y, pos.Z + val.Normali.Z);
                neib = world.BulkBlockAccessor.GetBlock(npos, BlockLayersAccess.Fluid);

                bbfsl = neib.GetBehavior<BlockBehaviorFiniteSpreadingLiquid>();
                if (bbfsl != null) world.RegisterCallbackUnique(bbfsl.OnDelayedWaterUpdateCheck, npos.Copy(), spreadDelay);
            }
        }

        private Block GetReplacementBlock(Block neighborBlock, IWorldAccessor world)
        {
            AssetLocation replacementLocation = liquidFlowingCollisionReplacement;
            if (IsLiquidSourceBlock(neighborBlock))
            {
                replacementLocation = liquidSourceCollisionReplacement;
            }
            return replacementLocation == null ? null : world.GetBlock(replacementLocation);
        }

        /// <summary>
        /// Returns true when this block and the other are colliding and replacement blocks need to be placed instead
        /// </summary>
        /// <param name="block">The block owning this behavior</param>
        /// <param name="other">The block we are colliding with</param>
        /// <returns>True if the two blocks are different liquids that can collide, false otherwise</returns>
        private bool CollidesWith(Block block, Block other)
        {
            return
                other.IsLiquid() && block.IsLiquid() &&
                other.LiquidCode == collidesWith
            ;
        }

        private bool IsSameLiquid(Block block, Block other)
        {
            return block.LiquidCode == other.LiquidCode;
        }

        private bool IsLiquidSourceBlock(Block block)
        {
            return block.LiquidLevel == MAXLEVEL;
        }

        /// <summary>
        /// Tries to lower the liquid level at the given position if the liquid is not connected to a source.
        /// 
        /// </summary>
        /// <param name="ourBlock"></param>
        /// <param name="world"></param>
        /// <param name="pos"></param>
        /// <returns>True if the liquid was lowered at the given position, false otherwise</returns>
        private bool TryLoweringLiquidLevel(Block ourBlock, IWorldAccessor world, BlockPos pos)
        {
            if (IsLiquidSourceBlock(ourBlock) == false)
            {
                int nlevel = GetMaxNeighbourLiquidLevel(ourBlock, world, pos);
                if (nlevel <= ourBlock.LiquidLevel)
                {
                    LowerLiquidLevelAndNotifyNeighbors(ourBlock, pos, world);
                    return true;
                }
            }
            return false;
        }

        private void LowerLiquidLevelAndNotifyNeighbors(Block block, BlockPos pos, IWorldAccessor world)
        {
            SpreadLiquid(GetLessLiquidBlockId(world, pos, block), pos, world);

            BlockPos npos = pos.Copy();
            for (int i = 0; i < BlockFacing.NumberOfFaces; i++)
            {
                BlockFacing.ALLFACES[i].IterateThruFacingOffsets(npos);
                Block liquidBlock = world.BlockAccessor.GetBlock(npos, BlockLayersAccess.Fluid);
                if (liquidBlock.BlockId != 0) liquidBlock.OnNeighbourBlockChange(world, npos, pos);
            }
        }

        private bool TrySpreadIntoBlock(Block ourblock, Block ourSolid, BlockPos pos, BlockPos npos, BlockFacing facing, IWorldAccessor world)
        {
            if (CanSpreadIntoBlock(ourblock, ourSolid, pos, npos, facing, world))
            {
                Block neighborLiquid = world.BlockAccessor.GetBlock(npos, BlockLayersAccess.Fluid);   // A bit inefficient because CanSpreadIntoBlock is already calling .GetBlock(npos, EnumBlockLayersAccess.LiquidOnly)
                if (CollidesWith(ourblock, neighborLiquid))
                {
                    ReplaceLiquidBlock(neighborLiquid, npos, world);
                }
                else
                {
                    SpreadLiquid(GetLessLiquidBlockId(world, npos, ourblock), npos, world);
                }
                return true;
            }

            return false;
        }

        public int GetLessLiquidBlockId(IWorldAccessor world, BlockPos pos, Block block)
        {
            return GetLiquidBlockId(world, pos, block, block.LiquidLevel - 1);
        }

        public int GetMoreLiquidBlockId(IWorldAccessor world, BlockPos pos, Block block)
        {
            return GetLiquidBlockId(world, pos, block, Math.Min(MAXLEVEL, block.LiquidLevel + 1));
        }

        public int GetLiquidBlockId(IWorldAccessor world, BlockPos pos, Block block, int liquidLevel)
        {
            if (liquidLevel < 1) return 0;

            Vec3i dir = new Vec3i();
            bool anySideFree = false;

            BlockPos npos = pos.Copy();
            IBlockAccessor blockAccessor = world.BlockAccessor;
            foreach (var val in Cardinal.ALL)
            {
                npos.Set(pos.X + val.Normali.X, pos.Y, pos.Z + val.Normali.Z);
                Block nblock = blockAccessor.GetBlock(npos, BlockLayersAccess.Fluid);
                if (nblock.LiquidLevel == liquidLevel || nblock.Replaceable < 6000 || !nblock.IsLiquid()) continue;

                Vec3i normal = nblock.LiquidLevel < liquidLevel ? val.Normali : val.Opposite.Normali;

                if (!val.IsDiagonal)
                {
                    nblock = blockAccessor.GetBlock(npos, BlockLayersAccess.Solid);
                    anySideFree |= nblock.GetLiquidBarrierHeightOnSide(BlockFacing.ALLFACES[val.Opposite.Index / 2], npos) != 1.0;
                    nblock = blockAccessor.GetBlock(pos, BlockLayersAccess.Solid);
                    anySideFree |= nblock.GetLiquidBarrierHeightOnSide(BlockFacing.ALLFACES[val.Index / 2], pos) != 1.0;
                }

                dir.X += normal.X;
                dir.Z += normal.Z;
            }

            if (Math.Abs(dir.X) > Math.Abs(dir.Z)) dir.Z = 0;  // e.g. if both W and NW flow, the non-diagonal (W) takes priority
            else if (Math.Abs(dir.Z) > Math.Abs(dir.X)) dir.X = 0;
            dir.X = Math.Sign(dir.X);
            dir.Z = Math.Sign(dir.Z);

            Cardinal flowDir = Cardinal.FromNormali(dir);

            if (flowDir == null)
            {
                Block downBlock = blockAccessor.GetBlock(pos.DownCopy(), BlockLayersAccess.Fluid);
                Block upBlock = blockAccessor.GetBlock(pos.UpCopy(), BlockLayersAccess.Fluid);
                bool downLiquid = IsSameLiquid(downBlock, block);
                bool upLiquid = IsSameLiquid(upBlock, block);

                if ((downLiquid && downBlock.Variant["flow"] == "d") || (upLiquid && upBlock.Variant["flow"] == "d"))
                {
                    return world.GetBlock(block.CodeWithParts("d", "" + liquidLevel)).BlockId;
                }

                if (anySideFree)
                {
                    return world.GetBlock(block.CodeWithParts("d", "" + liquidLevel)).BlockId;
                }

                return world.GetBlock(block.CodeWithParts("still", "" + liquidLevel)).BlockId;
            }

            return world.GetBlock(block.CodeWithParts(flowDir.Initial, "" + liquidLevel)).BlockId;
        }

        private int GetFallingLiquidBlockId(Block ourBlock, IWorldAccessor world)
        {
            return world.GetBlock(ourBlock.CodeWithParts("d", "6")).BlockId;
        }

        public int GetMaxNeighbourLiquidLevel(Block ourblock, IWorldAccessor world, BlockPos pos)
        {
            Block ourSolid = world.BlockAccessor.GetBlock(pos, BlockLayersAccess.SolidBlocks);

            BlockPos npos = pos.UpCopy();
            Block ublock = world.BlockAccessor.GetBlock(npos, BlockLayersAccess.Fluid);
            Block uSolid = world.BlockAccessor.GetBlock(npos, BlockLayersAccess.SolidBlocks);
            if (IsSameLiquid(ourblock, ublock) && ourSolid.GetLiquidBarrierHeightOnSide(BlockFacing.UP, pos) == 0.0 && uSolid.GetLiquidBarrierHeightOnSide(BlockFacing.DOWN, npos) == 0.0)
            {
                return MAXLEVEL;
            }
            else
            {
                int level = 0;
                npos.Down();
                for (int i = 0; i < BlockFacing.HORIZONTALS.Length; i++)
                {
                    BlockFacing.HORIZONTALS[i].IterateThruFacingOffsets(npos);
                    Block nblock = world.BlockAccessor.GetBlock(npos, BlockLayersAccess.Fluid);
                    if (IsSameLiquid(ourblock, nblock))
                    {
                        int nLevel = nblock.LiquidLevel;
                        if (ourSolid.GetLiquidBarrierHeightOnSide(BlockFacing.HORIZONTALS[i], pos) >= nLevel / MAXLEVEL_float) continue;
                        Block neibSolid = world.BlockAccessor.GetBlock(npos, BlockLayersAccess.SolidBlocks);
                        if (neibSolid.GetLiquidBarrierHeightOnSide(BlockFacing.HORIZONTALS[i].Opposite, npos) >= nLevel / MAXLEVEL_float) continue;
                        level = Math.Max(level, nLevel);
                    }
                }
                return level;
            }
        }

        [Obsolete("Instead Use CanSpreadIntoBlock(Block, BlockPos, IWorldAccessor) to read from the liquid layer correctly, as well as the block layer")]
        public bool CanSpreadIntoBlock(Block ourblock, Block neighborBlock, IWorldAccessor world)
        {
            return
                // Either neighbour liquid at a lower level
                (IsSameLiquid(ourblock, neighborBlock) && neighborBlock.LiquidLevel < ourblock.LiquidLevel) ||
                // Or the neighbour block can hold liquid and neighbour is below or we are on solid ground
                (!IsSameLiquid(ourblock, neighborBlock) && neighborBlock.Replaceable >= ReplacableThreshold)
            ;
        }

        public bool CanSpreadIntoBlock(Block ourblock, Block ourSolid, BlockPos pos, BlockPos npos, BlockFacing facing, IWorldAccessor world)
        {
            if (ourSolid.GetLiquidBarrierHeightOnSide(facing, pos) >= ourblock.LiquidLevel / MAXLEVEL_float) return false;
            Block neighborSolid = world.BlockAccessor.GetBlock(npos, BlockLayersAccess.SolidBlocks);
            if (neighborSolid.GetLiquidBarrierHeightOnSide(facing.Opposite, npos) >= ourblock.LiquidLevel / MAXLEVEL_float) return false;

            Block neighborLiquid = world.BlockAccessor.GetBlock(npos, BlockLayersAccess.Fluid);

            // If the same liquids, we can replace if the neighbour liquid is at a lower level
            if (IsSameLiquid(ourblock, neighborLiquid))
            {
                if (!IsLiquidSourceBlock(ourblock) && neighborLiquid is IBlockFlowing neighborFlowing && ourblock is IBlockFlowing ourFlowing)
                {
                    float neibFlow = neighborFlowing.FlowRate(npos);
                    float ourFlow = ourFlowing.FlowRate(pos);
                    if (neibFlow == ourFlow) return neighborLiquid.LiquidLevel < ourblock.LiquidLevel;
                    if (ourFlow < neibFlow)
                    {
                        // Standard water displaces/destroys rapid flowing water; rapid flowing water can never replace standard water  (otherwise rapid flow can be made to survive waterwheel passage)
                        if (neighborLiquid.LiquidLevel <= ourblock.LiquidLevel) return true;
                        if (npos.Y < pos.Y) return true;
                    }
                    return false;
                }
                else return neighborLiquid.LiquidLevel < ourblock.LiquidLevel;
            }

            // This is a special case intended for sea water / freshwater boundaries (until we have Brackish water blocks or another solution) - don't try to replace fresh water source blocks
            if (neighborLiquid.LiquidLevel == MAXLEVEL && !CollidesWith(ourblock, neighborLiquid)) return false;

            if (neighborLiquid.BlockId != 0) return neighborLiquid.Replaceable >= ourblock.Replaceable;    // New physics: the more replaceable liquid can be overcome by the less replaceable

            return ourblock.LiquidLevel > 1 || facing == BlockFacing.DOWN;
        }

        public override bool IsReplacableBy(Block byBlock, ref EnumHandling handled)
        {
            handled = EnumHandling.PreventDefault;
            return (block.IsLiquid() || block.Replaceable >= ReplacableThreshold) && byBlock.Replaceable <= block.Replaceable;
        }

        public List<PosAndDist> FindDownwardPaths(IWorldAccessor world, BlockPos pos, Block ourBlock)
        {
            List<PosAndDist> paths = new List<PosAndDist>();
            Queue<BlockPos> uncheckedPositions = new Queue<BlockPos>();
            int shortestPath = 99;

            BlockPos npos = new BlockPos(pos.dimension);
            for (int i = 0; i < downPaths.Length; i++)
            {
                Vec2i offset = downPaths[i];

                npos.Set(pos.X + offset.X, pos.Y - 1, pos.Z + offset.Y);
                Block blockBelow = world.BlockAccessor.GetBlock(npos);
                npos.Y++;
                Block neibLiquid = world.BlockAccessor.GetBlock(npos, BlockLayersAccess.Fluid);
                Block neibBlock = world.BlockAccessor.GetBlock(npos, BlockLayersAccess.SolidBlocks);

                if (neibLiquid.LiquidLevel < ourBlock.LiquidLevel && blockBelow.Replaceable >= ReplacableThreshold && neibBlock.Replaceable >= ReplacableThreshold && (!ourBlock.Code.PathStartsWith("rapidwater-") || !neibLiquid.Code.PathStartsWith("water-")))   // Hard-coding crime to prevent rapidwater pathing through standard water; does nothing except for that specific combination of blocks
                {
                    uncheckedPositions.Enqueue(new BlockPos(pos.X + offset.X, pos.Y, pos.Z + offset.Y, pos.dimension));

                    BlockPos foundPos = BfsSearchPath(world, uncheckedPositions, pos, ourBlock);
                    if (foundPos != null)
                    {
                        PosAndDist pad = new PosAndDist() { pos = foundPos, dist = pos.ManhattanDistance(pos.X + offset.X, pos.Y, pos.Z + offset.Y) };

                        if (pad.dist == 1 && ourBlock.LiquidLevel < MAXLEVEL)
                        {
                            paths.Clear();
                            paths.Add(pad);
                            return paths;
                        }

                        paths.Add(pad);
                        shortestPath = Math.Min(shortestPath, pad.dist);
                    }
                }
            }

            // Now we remove all suboptimal paths
            for (int i = 0; i < paths.Count; i++)
            {
                if (paths[i].dist > shortestPath)
                {
                    paths.RemoveAt(i);
                    i--;
                }
            }

            return paths;
        }


        private BlockPos BfsSearchPath(IWorldAccessor world, Queue<BlockPos> uncheckedPositions, BlockPos target, Block ourBlock)
        {
            BlockPos npos = new BlockPos(target.dimension);
            BlockPos pos;
            BlockPos origin = null;
            while (uncheckedPositions.Count > 0)
            {
                pos = uncheckedPositions.Dequeue();
                if (origin == null) origin = pos;
                int curDist = pos.ManhattanDistance(target);

                npos.Set(pos);
                for (int i = 0; i < BlockFacing.HORIZONTALS.Length; i++)
                {
                    BlockFacing.HORIZONTALS[i].IterateThruFacingOffsets(npos);
                    if (npos.ManhattanDistance(target) > curDist) continue;

                    if (npos.Equals(target)) return pos;

                    Block b = world.BlockAccessor.GetMostSolidBlock(npos);
                    if (b.GetLiquidBarrierHeightOnSide(BlockFacing.HORIZONTALS[i].Opposite, npos) >= (ourBlock.LiquidLevel - pos.ManhattanDistance(origin)) / MAXLEVEL_float) continue;
                    
                    uncheckedPositions.Enqueue(npos.Copy());
                }
            }

            return null;
        }


        public override bool ShouldReceiveClientParticleTicks(IWorldAccessor world, IPlayer byPlayer, BlockPos pos, ref EnumHandling handled)
        {
            handled = EnumHandling.PreventDefault;

            if (block.ParticleProperties == null || block.ParticleProperties.Length == 0) return false;

            // Would be better to configure with a property but since the client does not call Initialize, the properties are not honored
            // in this method. This will have to do until the properties are honored or this method becomes a separate client behavior.
            if (block.LiquidCode == "lava")
            {
                int r = world.BlockAccessor.GetBlockAbove(pos).Replaceable;
                return r > ReplacableThreshold;
            }
            else
            {
                // Water does its own handling
                handled = EnumHandling.PassThrough;
                return false;
            }
        }

        private static AssetLocation CreateAssetLocation(JsonObject properties, string propertyName)
        {
            return CreateAssetLocation(properties, null, propertyName);
        }

        private static AssetLocation CreateAssetLocation(JsonObject properties, string prefix, string propertyName)
        {
            string value = properties[propertyName]?.AsString();
            if (value == null)
            {
                return null;
            }
            else
            {
                return prefix == null ? new AssetLocation(value) : new AssetLocation(prefix + value);
            }
        }

    }


    public class PosAndDist
    {
        public BlockPos pos;
        public int dist;
    }

}
