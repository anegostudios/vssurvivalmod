using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Vintagestory.API;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace Vintagestory.GameContent
{
    public class BlockBehaviorFiniteSpreadingLiquid : BlockBehavior
    {
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

            Block block = world.BlockAccessor.GetLiquidBlock(pos);
            if (block.HasBehavior<BlockBehaviorFiniteSpreadingLiquid>()) updateOwnFlowDir(block, world, pos);
        }

        private void SpreadAndUpdateLiquidLevels(IWorldAccessor world, BlockPos pos)
        {
            // Slightly weird hack 
            // 1. We call this method also for other blocks, so can't rely on this.block
            // 2. our own liquid level might have changed from other nearby liquid sources
            Block ourBlock = world.BlockAccessor.GetLiquidBlock(pos);

            int liquidLevel = ourBlock.LiquidLevel;
            if (liquidLevel > 0)
            {
                // Lower liquid if not connected to source block
                if (!TryLoweringLiquidLevel(ourBlock, world, pos)) 
                {
                    pos.Y--;
                    // nasty slow check, but supports chiselled blocks for good physics
                    bool onSolidGround = world.BlockAccessor.GetSolidBlock(pos.X, pos.Y, pos.Z).CanAttachBlockAt(world.BlockAccessor, ourBlock, pos, BlockFacing.UP);
                    pos.Y++;
                    if (!onSolidGround)
                    {
                        TrySpreadDownwards(world, pos);
                    }
                    else if (liquidLevel > 1) // Can we still spread somewhere
                    {
                        List<PosAndDist> downwardPaths = FindDownwardPaths(world, pos, ourBlock);
                        if (downwardPaths.Count > 0) // Prefer flowing to downward paths rather than outward
                        {
                            FlowTowardDownwardPaths(downwardPaths, ourBlock, pos, world);
                        }
                        else
                        {
                            TrySpreadHorizontal(ourBlock, world, pos);

                            // Turn into water source block if surrounded by 3 other sources
                            if (!IsLiquidSourceBlock(ourBlock))
                            {
                                int nearbySourceBlockCount = 0;
                                BlockPos qpos = pos.Copy();
                                for (int i = 0; i < BlockFacing.HORIZONTALS.Length; i++)
                                {
                                    BlockFacing.HORIZONTALS[i].IterateThruFacingOffsets(qpos);
                                    Block nblock = world.BlockAccessor.GetLiquidBlock(qpos);
                                    if (IsSameLiquid(ourBlock, nblock) && IsLiquidSourceBlock(nblock)) nearbySourceBlockCount++;
                                }

                                if (nearbySourceBlockCount >= 3)
                                {
                                    world.BlockAccessor.SetLiquidBlock(GetMoreLiquidBlockId(world, pos, ourBlock), pos);

                                    BlockPos npos = pos.Copy();
                                    for (int i = 0; i < BlockFacing.HORIZONTALS.Length; i++)
                                    {
                                        BlockFacing.HORIZONTALS[i].IterateThruFacingOffsets(npos);
                                        Block nblock = world.BlockAccessor.GetLiquidBlock(npos);
                                        if (nblock.HasBehavior<BlockBehaviorFiniteSpreadingLiquid>()) updateOwnFlowDir(nblock, world, npos);
                                    }
                                }
                            }
                        }
                    }
                }

                
            }
        }


        private void FlowTowardDownwardPaths(List<PosAndDist> downwardPaths, Block liquidBlock, BlockPos pos, IWorldAccessor world)
        {
            foreach (PosAndDist pod in downwardPaths)
            {
                if (CanSpreadIntoBlock(liquidBlock, pod.pos, pod.pos.FacingFrom(pos), world))
                {
                    Block neighborLiquid = world.BlockAccessor.GetLiquidBlock(pod.pos);
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

        private void TrySpreadDownwards(IWorldAccessor world, BlockPos pos)
        {
            BlockPos npos = pos.DownCopy();

            if (CanSpreadIntoBlock(block, npos, BlockFacing.DOWN, world))
            {
                Block neighborLiquid = world.BlockAccessor.GetLiquidBlock(npos);
                if (IsDifferentCollidableLiquid(block, neighborLiquid))
                {
                    ReplaceLiquidBlock(neighborLiquid, npos, world);
                    TryFindSourceAndSpread(npos, world);
                }
                else
                {
                    SpreadLiquid(GetFallingLiquidBlockId(block, world), npos, world);
                }
            }
        }

        private void TrySpreadHorizontal(Block ourblock, IWorldAccessor world, BlockPos pos)
        {
            foreach (BlockFacing facing in BlockFacing.HORIZONTALS)
            {
                TrySpreadIntoBlock(ourblock, pos.AddCopy(facing), facing, world);
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

                NotifyNeighborsOfBlockChange(pos, world);
                GenerateSteamParticles(pos, world);
                world.PlaySoundAt(collisionReplaceSound, pos.X, pos.Y, pos.Z);
                //world.RegisterCallbackUnique(OnDelayedWaterUpdateCheck, pos.UpCopy(), spreadDelay); - wtf is this here for? it creates infinite pillars of basalt
            }
        }

        private void SpreadLiquid(int blockId, BlockPos pos, IWorldAccessor world)
        {
            //                world.SpawnCubeParticles(pos, new Vec3d(pos.X + 0.5, pos.Y + 0.5, pos.Z + 0.5), 0.7f, 25, 0.75f);
            world.BulkBlockAccessor.SetLiquidBlock(blockId, pos);
            world.RegisterCallbackUnique(OnDelayedWaterUpdateCheck, pos, spreadDelay);

            Block ourBlock = world.GetBlock(blockId);
            TryReplaceNearbyLiquidBlocks(ourBlock, pos, world);
        }

        private void updateOwnFlowDir(Block block, IWorldAccessor world, BlockPos pos)
        {
            int blockId = GetLiquidBlockId(world, pos, block, block.LiquidLevel);
            if (block.BlockId != blockId)
            {
                world.BlockAccessor.SetLiquidBlock(blockId, pos);
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
                Block neib = world.BlockAccessor.GetLiquidBlock(npos);
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
            Block sourceBlock = world.BlockAccessor.GetLiquidBlock(sourceBlockPos);
            while (sourceBlock.IsLiquid())
            {
                if (IsLiquidSourceBlock(sourceBlock))
                {
                    TrySpreadHorizontal(sourceBlock, world, sourceBlockPos);
                    return true;
                }
                sourceBlockPos.Add(0, 1, 0);
                sourceBlock = world.BlockAccessor.GetLiquidBlock(sourceBlockPos);
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
            steamParticles.MinPos.Set(pos.ToVec3d().AddCopy(0.5, 1.1, 0.5));
            steamParticles.AddPos.Set(new Vec3d(0.5, 1.0, 0.5));
            steamParticles.SizeEvolve = new EvolvingNatFloat(EnumTransformFunction.LINEARINCREASE, 1.0f);
            world.SpawnParticles(steamParticles);
        }

        private void NotifyNeighborsOfBlockChange(BlockPos pos, IWorldAccessor world)
        {
            foreach (BlockFacing facing in BlockFacing.ALLFACES)
            {
                BlockPos npos = pos.AddCopy(facing);
                Block neib = world.BlockAccessor.GetLiquidBlock(npos);
                neib.OnNeighbourBlockChange(world, npos, pos);
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
            return block.LiquidLevel == 7;
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
                Block liquidBlock = world.BlockAccessor.GetLiquidBlock(npos);
                if (liquidBlock.BlockId != 0) liquidBlock.OnNeighbourBlockChange(world, npos, pos);
            }
        }

        private void TrySpreadIntoBlock(Block ourblock, BlockPos npos, BlockFacing facing, IWorldAccessor world)
        {
            //IBlockAccessor blockAccess = world.BulkBlockAccessor;
            if (CanSpreadIntoBlock(ourblock, npos, facing, world))
            {
                Block neighborLiquid = world.BlockAccessor.GetLiquidBlock(npos);   // A bit inefficient because CanSpreadIntoBlock is already calling .GetLiquidBlock(npos)
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
            return GetLiquidBlockId(world, pos, block, Math.Min(7, block.LiquidLevel + 1));
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
                Block nblock = world.BlockAccessor.GetLiquidBlock(npos);
                if (nblock.LiquidLevel == liquidLevel || nblock.Replaceable < 6000 || !nblock.IsLiquid()) continue;

                Vec3i normal = nblock.LiquidLevel < liquidLevel ? val.Normali : val.Opposite.Normali;

                anySideFree |= !val.IsDiagnoal && nblock.Replaceable >= 6000;

                dir.X += normal.X;
                dir.Z += normal.Z;
            }

            dir.X = Math.Sign(dir.X);
            dir.Z = Math.Sign(dir.Z);

            Cardinal flowDir = Cardinal.FromNormali(dir);

            if (flowDir == null)
            {
                pos.Y--;
                Block downBlock = world.BlockAccessor.GetLiquidBlock(pos);
                pos.Y += 2;
                Block upBlock = world.BlockAccessor.GetLiquidBlock(pos);
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
            BlockPos npos = pos.Copy();
            npos.Y++;
            Block ublock = world.BlockAccessor.GetLiquidBlock(npos);
            npos.Y--;
            if (IsSameLiquid(ourblock, ublock))
            {
                return 7;
            }
            else
            {
                int level = 0;
                for (int i = 0; i < BlockFacing.HORIZONTALS.Length; i++)
                {
                    BlockFacing.HORIZONTALS[i].IterateThruFacingOffsets(npos);
                    Block nblock = world.BlockAccessor.GetLiquidBlock(npos);
                    if (IsSameLiquid(ourblock, nblock)) level = Math.Max(level, nblock.LiquidLevel);
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

        public bool CanSpreadIntoBlock(Block ourblock, BlockPos npos, BlockFacing facing, IWorldAccessor world)
        {
            Block neighborLiquid = world.BlockAccessor.GetLiquidBlock(npos);

            bool isSameLiquid = ourblock.LiquidCode == neighborLiquid.LiquidCode;

            if (isSameLiquid) return neighborLiquid.LiquidLevel < ourblock.LiquidLevel;

            if (neighborLiquid.BlockId != 0) return neighborLiquid.Replaceable >= ourblock.Replaceable;    // New physics: the more replaceable liquid can be overcome by the less replaceable

            Block neighborBlock = world.BlockAccessor.GetBlock(npos);
            return !neighborBlock.SideSolid.Opposite(facing.Index);
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
                Block aboveliquid = world.BlockAccessor.GetLiquidBlock(npos);
                Block aboveblock = world.BlockAccessor.GetBlock(npos);

                // This needs rewriting :<
                if (aboveliquid.LiquidLevel < ourBlock.LiquidLevel && block.Replaceable >= ReplacableThreshold && aboveblock.Replaceable >= ReplacableThreshold)
                {
                    uncheckedPositions.Enqueue(new BlockPos(pos.X + offset.X, pos.Y, pos.Z + offset.Y));

                    BlockPos foundPos = BfsSearchPath(world, uncheckedPositions, pos, ourBlock);
                    if (foundPos != null)
                    {
                        PosAndDist pad = new PosAndDist() { pos = foundPos, dist = pos.ManhattenDistance(pos.X + offset.X, pos.Y, pos.Z + offset.Y) };

                        if (pad.dist == 1 && ourBlock.LiquidLevel < 7)
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
            BlockPos pos, npos = new BlockPos();
            while (uncheckedPositions.Count > 0)
            {
                pos = uncheckedPositions.Dequeue();
                int curDist = pos.ManhattenDistance(target);

                npos.Set(pos);
                for (int i = 0; i < BlockFacing.HORIZONTALS.Length; i++)
                {
                    BlockFacing.HORIZONTALS[i].IterateThruFacingOffsets(npos);
                    if (npos.ManhattenDistance(target) > curDist) continue;

                    if (npos.Equals(target)) return pos;

                    if (world.BlockAccessor.IsSideSolid(npos.X, npos.Y, npos.Z, BlockFacing.HORIZONTALS[i].Opposite)) continue;

                    uncheckedPositions.Enqueue(npos.Copy());
                }

            }

            return null;
        }


        public override bool ShouldReceiveClientGameTicks(IWorldAccessor world, IPlayer byPlayer, BlockPos pos, ref EnumHandling handled)
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
