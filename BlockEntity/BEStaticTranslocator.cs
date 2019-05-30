using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace Vintagestory.GameContent
{
    public class BlockEntityStaticTranslocator : BlockEntityAnimatable
    {
        public int MinTeleporterRangeInBlocks = 400;
        public int MaxTeleporterRangeInBlocks = 6000;

        BlockPos tpLocation;
        Dictionary<long, TeleportingEntity> tpingEntities = new Dictionary<long, TeleportingEntity>();

        BlockStaticTranslocator block;
        Vec3d posvec;
        long lastCollideMsOwnPlayer;

        TeleporterManager manager;

        ICoreServerAPI sapi;

        public BlockEntityStaticTranslocator()
        {
        }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            manager = api.ModLoader.GetModSystem<TeleporterManager>();

            if (api.Side == EnumAppSide.Server)
            {
                sapi = api as ICoreServerAPI;
                //RegisterGameTickListener(OnServerGameTick, 250);
            } else
            {
                //RegisterGameTickListener(OnClientGameTick, 50);
            }

            block = api.World.BlockAccessor.GetBlock(pos) as BlockStaticTranslocator;
            posvec = new Vec3d(pos.X, pos.Y + 1, pos.Z);

            if (api.World.Side == EnumAppSide.Client)
            {
                float rotY = block.Shape.rotateY;
                InitializeAnimator("translocator", new Vec3f(0, rotY, 0));
            }
        }

        


        internal void OnEntityCollide(Entity entity)
        {
            TeleportingEntity tpe = null;
            if (!tpingEntities.TryGetValue(entity.EntityId, out tpe))
            {
                tpingEntities[entity.EntityId] = tpe = new TeleportingEntity()
                {
                    Entity = entity
                };
            }

            tpe.LastCollideMs = api.World.ElapsedMilliseconds;
            
            
            if (api.Side == EnumAppSide.Client)
            {
                if ((api as ICoreClientAPI).World.Player.Entity == entity)
                {
                    lastCollideMsOwnPlayer = api.World.ElapsedMilliseconds;
                    manager.lastCollideMsOwnPlayer = lastCollideMsOwnPlayer;
                }
            }
        }

        
        private void OnClientGameTick(float dt)
        {
            if (block == null || api?.World == null || !canTeleport) return;

            SimpleParticleProperties currentParticles = (api.World.ElapsedMilliseconds > 100 && api.World.ElapsedMilliseconds - lastCollideMsOwnPlayer < 100) ? 
                block.insideParticles : 
                block.idleParticles
            ;
            
            currentParticles.minPos = posvec;
            api.World.SpawnParticles(currentParticles);

        }

        bool canTeleport = false;
        bool findNextChunk = true;

        private void OnServerGameTick(float dt)
        {            
            if (findNextChunk)
            {
                findNextChunk = false;

                int dx = (int)(MinTeleporterRangeInBlocks + sapi.World.Rand.NextDouble() * (MaxTeleporterRangeInBlocks - MinTeleporterRangeInBlocks));
                int dz = (int)(MinTeleporterRangeInBlocks + sapi.World.Rand.NextDouble() * (MaxTeleporterRangeInBlocks - MinTeleporterRangeInBlocks));

                int chunkX = (pos.X + dx) / sapi.World.BlockAccessor.ChunkSize;
                int chunkZ = (pos.Z + dz) / sapi.World.BlockAccessor.ChunkSize;

                TreeAttribute tree = new TreeAttribute();
                tree.SetBool("mustructureChanceModifier-Translocator", true);

                ChunkLoadOptions opts = new ChunkLoadOptions()
                {
                    KeepLoaded = false,
                    OnLoaded = () => TestForExitPoint(chunkX - 1, chunkZ - 1, chunkX + 1, chunkZ + 1),
                    ChunkLoadConfig = tree
                };

                sapi.WorldManager.LoadChunkColumnFast(chunkX - 1, chunkZ - 1, chunkX + 1, chunkZ + 1, opts);
            }

            if (canTeleport)
            {
                HandleTeleporting(dt);
            }
        }

        private void TestForExitPoint(int chunkX1, int chunkZ1, int chunkX2, int chunkZ2)
        {
            BlockPos pos = FindExitPoint(chunkX1, chunkZ1, chunkX2, chunkZ2);
            if (pos == null)
            {
                findNextChunk = true;
            } else
            {
                BlockEntityStaticTranslocator exitBe = api.World.BlockAccessor.GetBlockEntity(pos) as BlockEntityStaticTranslocator;
                if (exitBe != null)
                {
                    exitBe.tpLocation = pos.Copy();
                    exitBe.canTeleport = true;

                    tpLocation = pos;
                    canTeleport = true;
                }
            }
        }

        private BlockPos FindExitPoint(int chunkX1, int chunkZ1, int chunkX2, int chunkZ2)
        {
            Dictionary<long, IMapRegion> regions = new Dictionary<long, IMapRegion>();

            int regionSize = sapi.WorldManager.RegionSize;
            int chunksize = sapi.WorldManager.ChunkSize;

            for (int chunkx = chunkX1; chunkx <= chunkX2; chunkx++)
            {
                for (int chunkz = chunkZ1; chunkz <= chunkZ2; chunkz++)
                {
                    int regionX = chunkx * chunksize / regionSize;
                    int regionZ = chunkz * chunksize / regionSize;
                    IMapRegion region = sapi.WorldManager.GetMapRegion(regionX, regionZ);
                    if (region == null) continue;
                    regions[MapRegionIndex2D(regionX, regionZ)] = region;
                }
            }

            foreach (var val in regions)
            {
                List<GeneratedStructure> structures = val.Value.GeneratedStructures;
                foreach (var structure in structures)
                {
                    if (structure.Code.Contains("translocator"))
                    {
                        BlockPos pos = FindTranslocator(structure.Location);
                        if (pos != null) return pos;
                    }
                }
            }

            return null;
        }

        private BlockPos FindTranslocator(Cuboidi location)
        {
            BlockPos foundPos = null;

            sapi.World.BlockAccessor.WalkBlocks(location.Start.AsBlockPos, location.End.AsBlockPos, (block, pos) =>
            {
                BlockStaticTranslocator transBlock = block as BlockStaticTranslocator;
                if (transBlock != null && !transBlock.On)
                {
                    foundPos = pos.Copy();
                }
            });

            return foundPos;
        }

        public long MapRegionIndex2D(int regionX, int regionZ)
        {
            return ((long)regionZ) * (sapi.WorldManager.MapSizeX / sapi.WorldManager.RegionSize) + regionX;
        }



        List<long> toremove = new List<long>();
        void HandleTeleporting(float dt)
        { 
            toremove.Clear();

            foreach (var val in tpingEntities)
            {
                if (val.Value.Entity.Teleporting) continue;

                val.Value.SecondsPassed += Math.Min(0.5f, dt);

                if (api.World.ElapsedMilliseconds - val.Value.LastCollideMs > 100)
                {
                    toremove.Add(val.Key);
                    continue;
                }

                if (val.Value.SecondsPassed > 3 && tpLocation != null)
                {
                    val.Value.Entity.TeleportTo(tpLocation.AddCopy(0,1,0));

                    Entity e = val.Value.Entity;
                    if (e is EntityPlayer)
                    {
                        api.World.Logger.Debug("Teleporting player {0} to {1}", (e as EntityPlayer).GetBehavior<EntityBehaviorNameTag>().DisplayName, tpLocation);
                    } else
                    {
                        api.World.Logger.Debug("Teleporting entity {0} to {1}", e.Code, tpLocation);
                    }
                    

                    toremove.Add(val.Key);
                }
            }

            foreach(long entityid in toremove)
            {
                tpingEntities.Remove(entityid);
            }
        }


        public override void OnBlockRemoved()
        {
            base.OnBlockRemoved();

            if (api.Side == EnumAppSide.Server)
            {
                ICoreServerAPI sapi = api as ICoreServerAPI;
                sapi.ModLoader.GetModSystem<TeleporterManager>().DeleteLocation(pos);
            }
        }


        public override void FromTreeAtributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAtributes(tree, worldAccessForResolve);

            canTeleport = tree.GetBool("canTele");

            if (canTeleport) {
                tpLocation = new BlockPos(tree.GetInt("teleX"), tree.GetInt("teleY"), tree.GetInt("teleZ"));

                if (tpLocation.X == 0 && tpLocation.Z == 0) tpLocation = null; // For safety
            } 

        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);

            tree.SetBool("canTele", canTeleport);
            if (tpLocation != null)
            {
                tree.SetInt("teleX", tpLocation.X);
                tree.SetInt("teleY", tpLocation.Y);
                tree.SetInt("teleZ", tpLocation.Z);
            }
        }

        public override string GetBlockInfo(IPlayer forPlayer)
        {
            if (tpLocation == null)
            {
                return Lang.Get("Warming up...");
            }

            return Lang.Get("Teleports to {1}", tpLocation);
        }
    }
}
