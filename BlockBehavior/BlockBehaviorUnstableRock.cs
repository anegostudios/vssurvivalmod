using System.Collections.Generic;
using System;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Common.Entities;

namespace Vintagestory.GameContent
{
    public class ModSystemExplosionAffectedStability : ModSystem
    {
        ICoreServerAPI sapi;

        public override bool ShouldLoad(EnumAppSide forSide) => forSide == EnumAppSide.Server;

        public override void StartServerSide(ICoreServerAPI api)
        {
            sapi = api;
            api.Event.RegisterEventBusListener(onExplosion, 0.5, "onexplosion");
        }

        private void onExplosion(string eventName, ref EnumHandling handling, IAttribute data)
        {
            var tree = data as ITreeAttribute;
            var pos = tree.GetBlockPos("pos");
            var radius = tree.GetDouble("destructionRadius");

            double cnt = radius * radius * radius;
            int radint = (int)Math.Round(radius) + 1;

            var rnd = sapi.World.Rand;
            BlockPos tmpPos = new BlockPos();

            while (cnt-- > 0) 
            {
                int dx = rnd.Next(2 * radint) - radint;
                int dy = rnd.Next(2 * radint) - radint;
                int dz = rnd.Next(2 * radint) - radint;

                tmpPos.Set(pos.X + dx, pos.Y + dy, pos.Z + dz);

                var block = sapi.World.BlockAccessor.GetBlock(tmpPos);
                var bh = block.GetBehavior<BlockBehaviorUnstableRock>();
                bh?.CheckCollapsible(sapi.World, tmpPos);
            }
        }
    }


    /*
Tyrons thought cloud on Support beams
    
- support beams would need to fall off if not supported by anything
- support beams: the supported block only gets as much stability as the stability of the supporting block 

for a given start or end position, we need vice versa.  Ideally: We can get the beam block by probing any block the beam traverses through.
	
how the fu do we look that up?
per chunk storage would not be sufficent. beams can reach into neighbouring chunks

=> but what if each chunk stores 2 reference per support beam: Its start pos and its end pos. 
=> but if a beam goes diagonally through a chunk then that chunk will not find the beam :(
    
max length of a beam is 20 blocks. We *could* just check all adjacent chunks, but we would need to somehow walk along each beam to see if position X is supported by a beam 

possible prefilter: start and end position form a cuboid. Check if position X is inside that cuboid. 



    Other idea:
    In the same process we add a "custom collisionboxes" api. The beams system registeres collisionboxes for each loaded beam. Each custom collisionbox has a reference back to its owner.
    OBB collision system, WHEN?????
     */
    public class BlockBehaviorUnstableRock : BlockBehavior, IConditionalChiselable
    {
        protected AssetLocation fallSound = new AssetLocation("effect/rockslide");
        protected float dustIntensity = 1f;
        protected float impactDamageMul = 1f;
        protected AssetLocation collapsedBlockLoc;
        protected Block collapsedBlock;
        protected float collapseChance = 0.25f;

        protected float maxSupportSearchDistanceSq = 6 * 6;
        protected float maxSupportDistance = 2;
        protected float maxCollapseDistance = 1;

        ICoreServerAPI sapi;
        ICoreAPI api;
        bool Enabled => sapi != null && sapi.World.Config.GetString("caveIns") == "on" && sapi.Server.Config.AllowFallingBlocks;

        public BlockBehaviorUnstableRock(Block block) : base(block)
        {
        }

        public override void Initialize(JsonObject properties)
        {
            base.Initialize(properties);
            dustIntensity = properties["dustIntensity"].AsFloat(1);
            collapseChance = properties["collapseChance"].AsFloat(0.25f);
            maxSupportDistance = (float)properties["maxSupportDistance"].AsFloat(2f);
            maxCollapseDistance = (float)properties["maxCollapseDistance"].AsFloat(1f);

            string sound = properties["fallSound"].AsString(null);
            if (sound != null)
            {
                fallSound = AssetLocation.Create(sound, block.Code.Domain);
            }

            impactDamageMul = properties["impactDamageMul"].AsFloat(1f);

            var str = properties["collapsedBlock"].AsString(null);
            if (str != null)
            {
                collapsedBlockLoc = AssetLocation.Create(str, block.Code.Domain);
            }
        }

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            sapi = api as ICoreServerAPI;
            this.api = api;
            collapsedBlock = collapsedBlockLoc == null ? block : (api.World.GetBlock(collapsedBlockLoc) ?? block);
        }


        public override void OnBlockPlaced(IWorldAccessor world, BlockPos blockPos, ref EnumHandling handling)
        {
            base.OnBlockPlaced(world, blockPos, ref handling);
            CheckCollapsible(world, blockPos);
        }


        public override void OnBlockBroken(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, ref EnumHandling handling)
        {
            base.OnBlockBroken(world, pos, byPlayer, ref handling);
            checkCollapsibleNeighbours(world, pos);
        }

        public override void OnBlockExploded(IWorldAccessor world, BlockPos pos, BlockPos explosionCenter, EnumBlastType blastType, ref EnumHandling handling)
        {
            base.OnBlockExploded(world, pos, explosionCenter, blastType, ref handling);
            checkCollapsibleNeighbours(world, pos);
        }

        protected void checkCollapsibleNeighbours(IWorldAccessor world, BlockPos pos)
        {
            if (!Enabled) return;

            var faces = (BlockFacing[])BlockFacing.ALLFACES.Clone();
            GameMath.Shuffle(world.Rand, faces);
            
            for (int i = 0; i < faces.Length; i++)
            {
                if (i >= 2) break;
                if (CheckCollapsible(world, pos.AddCopy(faces[i]))) break;
            }
        }

        public bool CheckCollapsible(IWorldAccessor world, BlockPos pos)
        {
            if (world.Side == EnumAppSide.Server)
            {
                if (!Enabled) return false;
                if (world.Rand.NextDouble() > collapseChance) return false;

                if (getNearestSupportDistance(pos, false, out var supportPositions) > maxSupportDistance)
                {
                    collapse(world, supportPositions, pos);
                    return true;
                }
            }

            return false;
        }


        private void collapse(IWorldAccessor world, List<Vec4i> supportPositions, BlockPos startPos)
        {
            var unstablePositions = getNearestUnstableBlocks(world, supportPositions, startPos);

            foreach (var pos in unstablePositions)
            {
                // Prevents duplication
                Entity entity = world.GetNearestEntity(pos.ToVec3d().Add(0.5, 0.5, 0.5), 1, 1.5f, (e) =>
                {
                    return e is EntityBlockFalling ebf && ebf.initialPos.Equals(pos);
                });

                if (entity == null)
                {
                    var block = world.BlockAccessor.GetBlock(pos);
                    var bh = block.GetBehavior<BlockBehaviorUnstableRock>();

                    EntityBlockFalling entityblock = new EntityBlockFalling(bh.collapsedBlock, world.BlockAccessor.GetBlockEntity(pos), pos, fallSound, impactDamageMul, true, dustIntensity);
                    world.SpawnEntity(entityblock);
                }
            }
        }


        private float getNearestSupportDistance(BlockPos startPos, bool ignoreBeams, out List<Vec4i> supportPositions)
        {
            supportPositions = getNearestVerticalSupports(api.World, startPos);

            float nearestDist = 9999f;

            foreach (var pos in supportPositions) 
            {
                nearestDist = Math.Min(nearestDist, GameMath.Sqrt(Math.Max(0, pos.HorDistanceSqTo(startPos.X, startPos.Z) - (pos.W - 1))));
            }

            if (ignoreBeams) return nearestDist;

            var sbp = api.ModLoader.GetModSystem<ModSystemSupportBeamPlacer>();
            double beamDist = sbp.GetStableMostBeam(startPos, out var startend);

            return (float)Math.Min(nearestDist, beamDist);
        }

        private float getNearestSupportDistance(List<Vec4i> supportPositions, BlockPos startPos)
        {
            float nearestDist = 99f;
            if (supportPositions.Count == 0) return nearestDist;

            foreach (var pos in supportPositions)
            {
                nearestDist = Math.Min(nearestDist, pos.HorDistanceSqTo(startPos.X, startPos.Z) - (pos.W - 1));
            }

            return GameMath.Sqrt(nearestDist);
        }


        private List<BlockPos> getNearestUnstableBlocks(IWorldAccessor world, List<Vec4i> supportPositions, BlockPos startPos)
        {
            Queue<BlockPos> bfsQueue = new Queue<BlockPos>();
            bfsQueue.Enqueue(startPos);
            HashSet<BlockPos> visited = new HashSet<BlockPos>();

            List<BlockPos> unstableBlocks = new List<BlockPos>();

            int blocksToCollapse = 2 + world.Rand.Next(30);

            while (bfsQueue.Count > 0)
            {
                var ipos = bfsQueue.Dequeue();
                if (visited.Contains(ipos)) continue;
                visited.Add(ipos);

                for (int i = 0; i < BlockFacing.ALLFACES.Length; i++)
                {
                    var npos = ipos.AddCopy(BlockFacing.ALLFACES[i]);
                    float distSq = npos.HorDistanceSqTo(startPos.X, startPos.Z);
                    if (distSq > maxSupportSearchDistanceSq) continue;

                    float dist = getNearestSupportDistance(supportPositions, npos);
                    if (dist > maxCollapseDistance)
                    {
                        var block = world.BlockAccessor.GetBlock(npos);
                        if (block.HasBehavior<BlockBehaviorUnstableRock>())
                        {
                            unstableBlocks.Add(npos);

                            for (int dy = 1; dy < 4; dy++)
                            {
                                block = world.BlockAccessor.GetBlock(npos.X, npos.Y - dy, npos.Z);
                                if (block.HasBehavior<BlockBehaviorUnstableRock>())
                                {
                                    unstableBlocks.Add(npos.DownCopy(dy));
                                }
                            }
                        }
                    }

                    if (unstableBlocks.Count > blocksToCollapse) return unstableBlocks;

                    bfsQueue.Enqueue(npos);
                }
            }

            return unstableBlocks;
        }

        List<Vec4i> getNearestVerticalSupports(IWorldAccessor world, BlockPos startpos)
        {
            Queue<BlockPos> bfsQueue = new Queue<BlockPos>();
            bfsQueue.Enqueue(startpos);
            HashSet<BlockPos> visited = new HashSet<BlockPos>();

            List<Vec4i> verticalSupports = new List<Vec4i>();

             int str;
             if ((str = getVerticalSupportStrength(world, startpos)) > 0)
             {
                 verticalSupports.Add(new Vec4i(startpos, str));
                 return verticalSupports;
             }

            while (bfsQueue.Count > 0)
            {
                var ipos = bfsQueue.Dequeue();

                if (visited.Contains(ipos)) continue;
                visited.Add(ipos);

                for (int i = 0; i < BlockFacing.HORIZONTALS.Length; i++)
                {
                    var npos = ipos.AddCopy(BlockFacing.HORIZONTALS[i]);
                    float distSq = npos.HorDistanceSqTo(startpos.X, startpos.Z);
                    if (distSq > maxSupportSearchDistanceSq) continue;

                    var block = world.BlockAccessor.GetBlock(npos);

                    // Stability cannot propagate through non-solid blocks
                    if (!block.SideSolid.Horizontals)
                    {
                        continue;
                    }

                    if ((str = getVerticalSupportStrength(world, npos)) > 0)
                    {
                        verticalSupports.Add(new Vec4i(npos, str));
                        continue;
                    }

                    bfsQueue.Enqueue(npos);
                }
            }

            return verticalSupports;
        }

        // Lets define: A block vertically suppported, if it has 4 or more solid blocks below it (or has a support beam below)
        public static int getVerticalSupportStrength(IWorldAccessor world, BlockPos npos)
        {
            for (int i = 1; i < 5; i++)
            {
                var block = world.BlockAccessor.GetBlock(npos.X, npos.Y - i, npos.Z);
                int stab = block.Attributes?["unstableRockStabilization"].AsInt(0) ?? 0;
                if (stab > 0) return stab;

                if (!block.SideSolid.Verticals)
                {
                    return 0;
                }
            }

            return 1;
        }

        public override string GetPlacedBlockInfo(IWorldAccessor world, BlockPos pos, IPlayer forPlayer)
        {
            return string.Format("Instability: {0:0.#}%", getInstability(world, pos)*100);
        }

        public double getInstability(IWorldAccessor world, BlockPos pos)
        {
            return Math.Clamp(getNearestSupportDistance(pos, false, out _)*2, 0, 10) / 10;
        }

        public bool CanChisel(IWorldAccessor world, BlockPos pos, IPlayer player, out string errorCode)
        {
            errorCode = null;

            if (getInstability(world, pos) >= 1 && player.WorldData.CurrentGameMode != EnumGameMode.Creative)
            {
                errorCode = "cantchisel-toounstable";
                return false;
            }
            return true;
        }
    }
}
