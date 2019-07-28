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
    public class TeleportingEntity
    {
        public Entity Entity;
        public long LastCollideMs;
        public float SecondsPassed;
    }

    public class BlockEntityTeleporter : BlockEntity
    {
        TeleporterLocation tpLocation;
        Dictionary<long, TeleportingEntity> tpingEntities = new Dictionary<long, TeleportingEntity>();

        BlockTeleporter block;
        Vec3d posvec;
        long lastCollideMsOwnPlayer;

        TeleporterManager manager;

        public BlockEntityTeleporter()
        {
        }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            manager = api.ModLoader.GetModSystem<TeleporterManager>();

            if (api.Side == EnumAppSide.Server)
            {
                ICoreServerAPI sapi = api as ICoreServerAPI;

                tpLocation = manager.GetOrCreateLocation(pos);

                RegisterGameTickListener(OnServerGameTick, 50);
            } else
            {
                RegisterGameTickListener(OnClientGameTick, 50);
            }

            block = api.World.BlockAccessor.GetBlock(pos) as BlockTeleporter;
            posvec = new Vec3d(pos.X, pos.Y + 1, pos.Z);
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
                    manager.lastTeleCollideMsOwnPlayer = lastCollideMsOwnPlayer;
                }
            }
        }

        
        private void OnClientGameTick(float dt)
        {
            if (block == null || api?.World == null) return;

            SimpleParticleProperties currentParticles = (api.World.ElapsedMilliseconds > 100 && api.World.ElapsedMilliseconds - lastCollideMsOwnPlayer < 100) ? 
                block.insideParticles : 
                block.idleParticles
            ;
            
            currentParticles.minPos = posvec;
            api.World.SpawnParticles(currentParticles);

        }

        List<long> toremove = new List<long>();
        private void OnServerGameTick(float dt)
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

                if (val.Value.SecondsPassed > 1.5 && tpLocation?.TargetPos != null)
                {
                    // Preload the chunk
                    (api as ICoreServerAPI).WorldManager.LoadChunkColumnFast((int)tpLocation.TargetPos.X / api.World.BlockAccessor.ChunkSize, (int)tpLocation.TargetPos.Z / api.World.BlockAccessor.ChunkSize);
                }

                if (val.Value.SecondsPassed > 3 && tpLocation?.TargetPos != null)
                {
                    val.Value.Entity.TeleportTo(tpLocation.TargetPos.AddCopy(0,1,0));

                    Entity e = val.Value.Entity;
                    if (e is EntityPlayer)
                    {
                        api.World.Logger.Debug("Teleporting player {0} to {1}", (e as EntityPlayer).GetBehavior<EntityBehaviorNameTag>().DisplayName, tpLocation.TargetPos);
                    } else
                    {
                        api.World.Logger.Debug("Teleporting entity {0} to {1}", e.Code, tpLocation.TargetPos);
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

            if (tpLocation == null || api?.Side == EnumAppSide.Client)
            {
                tpLocation = new TeleporterLocation()
                {
                    SourceName = tree.GetString("sourceName"),
                    TargetName = tree.GetString("targetName"),
                };
            }
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);

            if (tpLocation != null)
            {
                tree.SetString("sourceName", tpLocation.SourceName == null ? "" : tpLocation.SourceName);
                tree.SetString("targetName", tpLocation.TargetName == null ? "" : tpLocation.TargetName);
            }
        }

        public override string GetBlockInfo(IPlayer forPlayer)
        {
            if (tpLocation == null) return null;

            return Lang.Get("This is {0}\nTeleports to {1}", tpLocation.SourceName, tpLocation.TargetName);
        }
    }
}
