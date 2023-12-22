using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace Vintagestory.GameContent
{
    public class BlockBehaviorFiniteSpreadingLiquid : BlockBehavior
    {
        private const int MAXLEVEL = 7;
        private const float MAXLEVEL_float = MAXLEVEL;
        public static Vec2i[] downPaths = ShapeUtil.GetSquarePointsSortedByMDist(3);

        public static SimpleParticleProperties steamParticles;

        public static int ReplacableThreshold = 5000;

        //The sound to play when a liquid collision causes blocks to be replaced
        private AssetLocation collisionReplaceSound;

        //Controls how fast the liquid spreads
        private int spreadDelay = 150;

        //The liquid this one can collide with
        private string collidesWith;

        //Block code to use when colliding with the source block of a different liquid

        AssetLocation liquidSourceCollisionReplacement;

        //Block code to use when colliding with a flowing block of a different liquid
        AssetLocation liquidFlowingCollisionReplacement;

        public BlockBehaviorFiniteSpreadingLiquid(Block block) : base(block)
        {
        }

        public override void Initialize(JsonObject properties)
        {
            base.Initialize(properties);

            spreadDelay = properties["spreadDelay"].AsInt();

            collisionReplaceSound = CreateAssetLocation(properties, "sounds/", "liquidCollisionSound");
            liquidSourceCollisionReplacement = CreateAssetLocation(properties, "sourceReplacementCode");
            liquidFlowingCollisionReplacement = CreateAssetLocation(properties, "flowingReplacementCode");
            collidesWith = properties["collidesWith"]?.AsString();
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
                    pos.Y--;
                    // nasty slow check, but supports chiselled blocks for good physics
                    var block = world.BlockAccessor.GetMostSolidBlock(pos.X, pos.Y, pos.Z);
                    
                    bool onSolidGround = block.CanAttachBlockAt(world.BlockAccessor, ourBlock, pos, BlockFacing.UP);
                    pos.Y++;
                    Block ourSolid = world.BlockAccessor.GetBlock(pos, BlockLayersAccess.SolidBlocks);

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
                if (!val.IsDiagnoal) continue;
                npos.Set(pos.X + val.Normali.X, pos.Y, pos.Z + val.Normali.Z);
                Block nblock = blockAccessor.GetBlock(npos, BlockLayersAccess.Fluid);
                if (IsSameLiquid(ourBlock, nblock) && IsLiquidSourceBlock(nblock)) nearbySourceBlockCount++;
            }
            return nearbySourceBlockCount;
        }

        private void FlowTowardDownwardPaths(List<PosAndDist> downwardPaths, Block liquidBlock, Block solidBlock, BlockPos pos, IWorldAccessor world)
        {
            foreach (PosAndDist pod in downwardPaths)
            {
                if (CanSpreadIntoBlock(liquidBlock, solidBlock, pos, pod.pos, pod.pos.FacingFrom(pos), world))
                {
                    Block neighborLiquid = world.BlockAccessor.GetBlock(pod.pos, BlockLayersAccess.Fluid);
                    if (IsDifferentCollidableLiquid(liquidBlock, neighborLiquid))
                    {
                        ReplaceLiquidBlock(neighborLiquid, pod.pos, world);
                    }
                    else
                    {
                        SpreadLiquid(GetLessLiquidBlockId(world, pod.pos, liquidBlock), pod.pos, world);
                    }
                }
            }
        }

        private bool TrySpreadDownwards(IWorldAccessor world, Block ourSolid, Block ourBlock, BlockPos pos)
        {
            BlockPos npos = pos.DownCopy();

            Block belowLiquid = world.BlockAccessor.GetBlock(npos, BlockLayersAccess.Fluid);
            if (CanSpreadIntoBlock(ourBlock, ourSolid, pos, npos, BlockFacing.DOWN, world))
            {
                if (IsDifferentCollidableLiquid(ourBlock, belowLiquid))
                {
                    ReplaceLiquidBlock(belowLiquid, npos, world);
                    TryFindSourceAndSpread(npos, world);
                }
                else
                {
                    bool fillWithSource = false;
                    // If the block above is a source, and either this has at least 1 horizontal neighbour which is a source, or the block above has at least 2 source neighbours and the block below here is solid ground or a source, then heal - we are in the middle of a lake or similar!)
                    if (IsLiquidSourceBlock(ourBlock))
                    {
                        if (CountNearbySourceBlocks(world.BlockAccessor, npos, ourBlock) > 1) fillWithSource = true;
                        else
                        {
                            npos.Y--;
                            Block blockBelow = world.BlockAccessor.GetBlock(npos, BlockLayersAccess.MostSolid);
                            bool onSolidGround = blockBelow.CanAttachBlockAt(world.BlockAccessor, ourBlock, npos, BlockFacing.UP);
                            if (onSolidGround || IsLiquidSourceBlock(world.BlockAccessor.GetBlock(npos, BlockLayersAccess.Fluid)))
                            {
                                int count = CountNearbySourceBlocks(world.BlockAccessor, pos, ourBlock);
                                fillWithSource = count >= 2;
                            }
                            npos.Y++;
                        }
                    }
                    SpreadLiquid(fillWithSource ? ourBlock.BlockId : GetFallingLiquidBlockId(ourBlock, world), npos, world);
                }

                return true;
            }

            return !IsLiquidSourceBlock(ourBlock) || !IsLiquidSourceBlock(belowLiquid);  // return false if this is water source above water source (then surface blocks of (>1 deep) lakes can spread sideways)
        }

        private void TrySpreadHorizontal(Block ourblock, Block ourSolid, IWorldAccessor world, BlockPos pos)
        {
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
                
                BlockBehaviorBreakIfFloating bh = replacementBlock.GetBehavior<BlockBehaviorBreakIfFloating>();
                if (bh != null && bh.IsSurroundedByNonSolid(world, pos))
                {
                    world.BulkBlockAccessor.SetBlock(replacementBlock.BlockId, pos.DownCopy());
                }

                UpdateNeighbouringLiquids(pos, world);
                GenerateSteamParticles(pos, world);
                world.PlaySoundAt(collisionReplaceSound, pos.X, pos.Y, pos.Z, null, true, 16);
            }
        }

        private void SpreadLiquid(int blockId, BlockPos pos, IWorldAccessor world)
        {
            world.BulkBlockAccessor.SetBlock(blockId, pos, BlockLayersAccess.Fluid);
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
                if (IsDifferentCollidableLiquid(ourBlock, neib))
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
            // First do down and up, as they are not included in Cardinals
            BlockPos npos = pos.DownCopy();
            Block neib = world.BlockAccessor.GetBlock(npos, BlockLayersAccess.Fluid);
            if (neib.HasBehavior<BlockBehaviorFiniteSpreadingLiquid>()) world.RegisterCallbackUnique(OnDelayedWaterUpdateCheck, npos.Copy(), spreadDelay);
            npos.Up(2);
            neib = world.BlockAccessor.GetBlock(npos, BlockLayersAccess.Fluid);
            if (neib.HasBehavior<BlockBehaviorFiniteSpreadingLiquid>()) world.RegisterCallbackUnique(OnDelayedWaterUpdateCheck, npos.Copy(), spreadDelay);

            // Now do all horizontal neighbours including the diagonals, because water blocks can have diagonal flow
            foreach (var val in Cardinal.ALL)
            {
                npos.Set(pos.X + val.Normali.X, pos.Y, pos.Z + val.Normali.Z);
                neib = world.BlockAccessor.GetBlock(npos, BlockLayersAccess.Fluid);
                if (neib.HasBehavior<BlockBehaviorFiniteSpreadingLiquid>()) world.RegisterCallbackUnique(OnDelayedWaterUpdateCheck, npos.Copy(), spreadDelay);
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
        /// Returns true when this block and the other block are different types of liquids
        /// </summary>
        /// <param name="block">The block owning this behavior</param>
        /// <param name="other">The block we are colliding with</param>
        /// <returns>True if the two blocks are different liquids that can collide, false otherwise</returns>
        private bool IsDifferentCollidableLiquid(Block block, Block other)
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

        private void TrySpreadIntoBlock(Block ourblock, Block ourSolid, BlockPos pos, BlockPos npos, BlockFacing facing, IWorldAccessor world)
        {
            if (CanSpreadIntoBlock(ourblock, ourSolid, pos, npos, facing, world))
            {
                Block neighborLiquid = world.BlockAccessor.GetBlock(npos, BlockLayersAccess.Fluid);   // A bit inefficient because CanSpreadIntoBlock is already calling .GetBlock(npos, EnumBlockLayersAccess.LiquidOnly)
                if (IsDifferentCollidableLiquid(ourblock, neighborLiquid))
                {
                    ReplaceLiquidBlock(neighborLiquid, npos, world);
                }
                else
                {
                    SpreadLiquid(GetLessLiquidBlockId(world, npos, ourblock), npos, world);
                }
            }
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
            foreach (var val in Cardinal.ALL)
            {
                npos.Set(pos.X + val.Normali.X, pos.Y, pos.Z + val.Normali.Z);
                Block nblock = world.BlockAccessor.GetBlock(npos, BlockLayersAccess.Fluid);
                if (nblock.LiquidLevel == liquidLevel || nblock.Replaceable < 6000 || !nblock.IsLiquid()) continue;

                Vec3i normal = nblock.LiquidLevel < liquidLevel ? val.Normali : val.Opposite.Normali;

                if (!val.IsDiagnoal)
                {
                    nblock = world.BlockAccessor.GetBlock(npos, BlockLayersAccess.Solid);
                    anySideFree |= !nblock.SideIsSolid(npos, val.Opposite.Index / 2);
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
                pos.Y--;
                Block downBlock = world.BlockAccessor.GetBlock(pos, BlockLayersAccess.Fluid);
                pos.Y += 2;
                Block upBlock = world.BlockAccessor.GetBlock(pos, BlockLayersAccess.Fluid);
                pos.Y--;
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

            BlockPos npos = pos.Copy();
            npos.Y++;
            Block ublock = world.BlockAccessor.GetBlock(npos, BlockLayersAccess.Fluid);
            npos.Y--;
            if (IsSameLiquid(ourblock, ublock) && ourSolid.GetLiquidBarrierHeightOnSide(BlockFacing.UP, pos) == 0.0)
            {
                return MAXLEVEL;
            }
            else
            {
                int level = 0;
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
            bool isSameLiquid = neighborBlock.LiquidCode == ourblock.LiquidCode;

            return
                // Either neighbour liquid at a lower level
                (isSameLiquid && neighborBlock.LiquidLevel < ourblock.LiquidLevel) ||
                // Or the neighbour block can hold liquid and neighbour is below or we are on solid ground
                (!isSameLiquid && neighborBlock.Replaceable >= ReplacableThreshold)
            ;
        }

        public bool CanSpreadIntoBlock(Block ourblock, Block ourSolid, BlockPos pos, BlockPos npos, BlockFacing facing, IWorldAccessor world)
        {
            if (ourSolid.GetLiquidBarrierHeightOnSide(facing, pos) >= ourblock.LiquidLevel / MAXLEVEL_float) return false;
            Block neighborSolid = world.BlockAccessor.GetBlock(npos, BlockLayersAccess.SolidBlocks);
            if (neighborSolid.GetLiquidBarrierHeightOnSide(facing.Opposite, npos) >= ourblock.LiquidLevel / MAXLEVEL_float) return false;

            Block neighborLiquid = world.BlockAccessor.GetBlock(npos, BlockLayersAccess.Fluid);

            // If the same liquids, we can replace if the neighbour liquid is at a lower level
            if (neighborLiquid.LiquidCode == ourblock.LiquidCode) return neighborLiquid.LiquidLevel < ourblock.LiquidLevel;

            // This is a special case intended for sea water / freshwater boundaries (until we have Brackish water blocks or another solution) - don't try to replace fresh water source blocks
            if (neighborLiquid.LiquidLevel == MAXLEVEL && !IsDifferentCollidableLiquid(ourblock, neighborLiquid)) return false;

            if (neighborLiquid.BlockId != 0) return neighborLiquid.Replaceable >= ourblock.Replaceable;    // New physics: the more replaceable liquid can be overcome by the less replaceable

            return ourblock.LiquidLevel > 1 || facing.Index == BlockFacing.DOWN.Index;
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

            BlockPos npos = new BlockPos();
            for (int i = 0; i < downPaths.Length; i++)
            {
                Vec2i offset = downPaths[i];

                npos.Set(pos.X + offset.X, pos.Y - 1, pos.Z + offset.Y);
                Block block = world.BlockAccessor.GetBlock(npos);
                npos.Y++;
                Block aboveliquid = world.BlockAccessor.GetBlock(npos, BlockLayersAccess.Fluid);
                Block aboveblock = world.BlockAccessor.GetBlock(npos, BlockLayersAccess.SolidBlocks);

                // This needs rewriting :<
                if (aboveliquid.LiquidLevel < ourBlock.LiquidLevel && block.Replaceable >= ReplacableThreshold && aboveblock.Replaceable >= ReplacableThreshold)
                {
                    uncheckedPositions.Enqueue(new BlockPos(pos.X + offset.X, pos.Y, pos.Z + offset.Y));

                    BlockPos foundPos = BfsSearchPath(world, uncheckedPositions, pos, ourBlock);
                    if (foundPos != null)
                    {
                        PosAndDist pad = new PosAndDist() { pos = foundPos, dist = pos.ManhattenDistance(pos.X + offset.X, pos.Y, pos.Z + offset.Y) };

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
            BlockPos npos = new BlockPos();
            BlockPos pos;
            BlockPos origin = null;
            while (uncheckedPositions.Count > 0)
            {
                pos = uncheckedPositions.Dequeue();
                if (origin == null) origin = pos;
                int curDist = pos.ManhattenDistance(target);

                npos.Set(pos);
                for (int i = 0; i < BlockFacing.HORIZONTALS.Length; i++)
                {
                    BlockFacing.HORIZONTALS[i].IterateThruFacingOffsets(npos);
                    if (npos.ManhattenDistance(target) > curDist) continue;

                    if (npos.Equals(target)) return pos;

                    Block b = world.BlockAccessor.GetMostSolidBlock(npos.X, npos.Y, npos.Z);
                    if (b.GetLiquidBarrierHeightOnSide(BlockFacing.HORIZONTALS[i].Opposite, npos) >= (ourBlock.LiquidLevel - pos.ManhattenDistance(origin)) / MAXLEVEL_float) continue;
                    // TODO:  check this adjustment to liquidLevel is appropriate

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
                int r = world.BlockAccessor.GetBlock(pos.X, pos.Y + 1, pos.Z).Replaceable;
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
