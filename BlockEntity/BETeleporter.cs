using System;
using System.Collections.Generic;
using System.Text;
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

    public abstract class BlockEntityTeleporterBase : BlockEntity
    {
        protected TeleporterManager manager;
        protected Dictionary<long, TeleportingEntity> tpingEntities = new Dictionary<long, TeleportingEntity>();
        protected float TeleportWarmupSec = 3;
        public abstract Vec3d GetTarget(Entity forEntity);

        protected bool somebodyIsTeleporting;
        protected bool somebodyDidTeleport;

        List<long> toremove = new List<long>();
        public long lastEntityCollideMs = 0;
        public long lastOwnPlayerCollideMs = 0;

        
        public bool tpLocationIsOffset;




        public BlockEntityTeleporterBase()
        {
        }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            manager = api.ModLoader.GetModSystem<TeleporterManager>();
        }

        public virtual void OnEntityCollide(Entity entity)
        {
            TeleportingEntity tpe;
            if (!tpingEntities.TryGetValue(entity.EntityId, out tpe))
            {
                tpingEntities[entity.EntityId] = tpe = new TeleportingEntity()
                {
                    Entity = entity
                };
            }

            tpe.LastCollideMs = Api.World.ElapsedMilliseconds;


            if (Api.Side == EnumAppSide.Client)
            {
                lastEntityCollideMs = Api.World.ElapsedMilliseconds;

                if ((Api as ICoreClientAPI).World.Player.Entity == entity)
                {
                    lastOwnPlayerCollideMs = Api.World.ElapsedMilliseconds;
                }
            }
        }

        

        protected virtual void HandleTeleportingServer(float dt)
        {
            if (toremove == null) throw new Exception("BETeleporterBase: toremove is null, it shouldn't be!");
            if (tpingEntities == null) throw new Exception("BETeleporterBase: tpingEntities is null, it shouldn't be!");
            if (Api == null) throw new Exception("BETeleporterBase: Api is null, it shouldn't be!");
            toremove.Clear();

            bool wasTeleporting = somebodyIsTeleporting;

            somebodyIsTeleporting &= tpingEntities.Count > 0;
            var sapi = Api as ICoreServerAPI;

            foreach (var val in tpingEntities)
            {
                if (val.Value == null) throw new Exception("BETeleporterBase: val.Value is null, it shouldn't be!");
                if (val.Value.Entity == null) throw new Exception("BETeleporterBase: val.Value.Entity is null, it shouldn't be!");
                if (val.Value.Entity.Teleporting) continue;

                val.Value.SecondsPassed += Math.Min(0.5f, dt);

                if (Api.World.ElapsedMilliseconds - val.Value.LastCollideMs > 100)
                {
                    // Make sure its not just server lag
                    Block block = Api.World.CollisionTester.GetCollidingBlock(Api.World.BlockAccessor, val.Value.Entity.SelectionBox, val.Value.Entity.Pos.XYZ, true);
                    if (!(block is BlockStaticTranslocator) && !(block is BlockTeleporter))
                    {
                        toremove.Add(val.Key);
                        continue;
                    }
                }

                if (val.Value.SecondsPassed > 0.1 && !somebodyIsTeleporting)
                {
                    somebodyIsTeleporting = true;
                    MarkDirty();
                }

                var tpTarget = GetTarget(val.Value.Entity);

                if (val.Value.SecondsPassed > 1.5 && tpTarget != null)
                {
                    var targetPos = tpTarget.Clone();
                    if (tpLocationIsOffset) targetPos.Add(Pos.X, Pos.Y, Pos.Z);

                    // Preload the chunk
                    IWorldChunk chunk = Api.World.BlockAccessor.GetChunkAtBlockPos(targetPos.AsBlockPos);
                    if (chunk != null)
                    {
                        chunk.MapChunk.MarkFresh();
                    }
                    else
                    {
                        sapi.WorldManager.LoadChunkColumnPriority((int)targetPos.X / GlobalConstants.ChunkSize, (int)targetPos.Z / GlobalConstants.ChunkSize, new ChunkLoadOptions()
                        {
                            KeepLoaded = false
                        });
                    }
                }

                if (val.Value.SecondsPassed > TeleportWarmupSec && tpTarget != null)
                {
                    var targetPos = tpTarget.Clone();
                    if (tpLocationIsOffset) targetPos.Add(Pos.X, Pos.Y, Pos.Z);
                    val.Value.Entity.TeleportTo(targetPos); // Fugly, need some better exit pos thing
                    toremove.Add(val.Key);

                    Entity e = val.Value.Entity;
                    if (e is EntityPlayer)
                    {
                        Api.World.Logger.Audit("Teleporting player {0} from {1} to {2}", (e as EntityPlayer).GetBehavior<EntityBehaviorNameTag>().DisplayName, e.Pos.AsBlockPos, tpTarget);
                    }
                    else
                    {
                        Api.World.Logger.Audit("Teleporting entity {0} from {1} to {2}", e.Code, e.Pos.AsBlockPos, tpTarget);
                    }

                    didTeleport(val.Value.Entity);

                    somebodyIsTeleporting = false;
                    somebodyDidTeleport = true;


                    MarkDirty();
                }
            }

            foreach (long entityid in toremove)
            {
                tpingEntities.Remove(entityid);
            }

            if (wasTeleporting && !somebodyIsTeleporting)
            {
                MarkDirty();
            }
        }








        protected virtual void didTeleport(Entity entity)
        {
            
        }


        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);

            somebodyIsTeleporting = tree.GetBool("somebodyIsTeleporting");
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);

            tree.SetBool("somebodyIsTeleporting", somebodyIsTeleporting);
            tree.SetBool("somebodyDidTeleport", somebodyDidTeleport);
            somebodyDidTeleport = false;
        }
    }

    public class BlockEntityTeleporter : BlockEntityTeleporterBase
    {
        BlockTeleporter ownBlock;
        Vec3d posvec;
        TeleporterLocation tpLocation;


        public override Vec3d GetTarget(Entity forEntity)
        {
            return tpLocation?.TargetPos?.ToVec3d().Add(-0.3, 1, -0.3);
        }

        public ILoadedSound teleportingSound;
        float teleSoundVolume = 0;
        float teleSoundPitch = 0.7f;


        public BlockEntityTeleporter()
        {
        }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            

            if (api.Side == EnumAppSide.Server)
            {
                ICoreServerAPI sapi = api as ICoreServerAPI;

                tpLocation = manager.GetOrCreateLocation(Pos);

                RegisterGameTickListener(OnServerGameTick, 50);
            } else
            {
                RegisterGameTickListener(OnClientGameTick, 50);

                teleportingSound = (api as ICoreClientAPI).World.LoadSound(new SoundParams()
                {
                    Location = new AssetLocation("sounds/block/teleporter.ogg"),
                    ShouldLoop = true,
                    Position = Pos.ToVec3f(),
                    RelativePosition = false,
                    DisposeOnFinish = false,
                    Volume = 0.5f
                });
            }

            ownBlock = Block as BlockTeleporter;
            posvec = new Vec3d(Pos.X, Pos.Y + 1, Pos.Z);

            
        }



        
        private void OnClientGameTick(float dt)
        {
            if (ownBlock == null || Api?.World == null) return;

            HandleSoundClient(dt);

            SimpleParticleProperties currentParticles = (Api.World.ElapsedMilliseconds > 100 && Api.World.ElapsedMilliseconds - lastOwnPlayerCollideMs < 100) ? 
                ownBlock.insideParticles : 
                ownBlock.idleParticles
            ;
            
            currentParticles.MinPos = posvec;
            Api.World.SpawnParticles(currentParticles);

        }

        protected virtual void HandleSoundClient(float dt)
        {
            var capi = Api as ICoreClientAPI;

            long msEllapsed = capi.World.ElapsedMilliseconds - lastEntityCollideMs;

            if (msEllapsed > 100)
            {
                teleSoundVolume = Math.Max(0, teleSoundVolume - 2 * dt);
                teleSoundPitch = Math.Max(0.7f, teleSoundPitch - 2 * dt);
            }
            else
            {
                teleSoundVolume = Math.Min(0.5f, teleSoundVolume + dt / 3);
                teleSoundPitch = Math.Min(6, teleSoundPitch + dt);
            }

            if (teleportingSound != null)
            {
                teleportingSound.SetVolume(teleSoundVolume);
                teleportingSound.SetPitch(teleSoundPitch);

                if (teleportingSound.IsPlaying)
                {
                    if (teleSoundVolume <= 0) teleportingSound.Stop();
                }
                else
                {
                    if (teleSoundVolume > 0) teleportingSound.Start();
                }
            }

        }

        protected override void didTeleport(Entity entity)
        {
            if (entity is EntityPlayer)
            {
                manager.DidTranslocateServer((entity as EntityPlayer).Player as IServerPlayer);
            }
        }


        private void OnServerGameTick(float dt)
        {
            try
            {
                HandleTeleportingServer(dt);
            }
            catch (Exception e)
            {
                Api.Logger.Warning("Exception when ticking Teleporter at {0}", Pos);
                Api.Logger.Error(e);
            }
        }



        public override void OnBlockRemoved()
        {
            base.OnBlockRemoved();

            if (Api.Side == EnumAppSide.Server)
            {
                ICoreServerAPI sapi = Api as ICoreServerAPI;
                sapi.ModLoader.GetModSystem<TeleporterManager>().DeleteLocation(Pos);
            }

            teleportingSound?.Dispose();
        }

        public override void OnBlockUnloaded()
        {
            base.OnBlockUnloaded();

            teleportingSound?.Dispose();
        }


        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);

            if (tpLocation == null || Api?.Side == EnumAppSide.Client)
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

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            if (tpLocation == null) return;

            dsc.AppendLine(Lang.Get("teleporter-info", tpLocation.SourceName, tpLocation.TargetName));
        }
    }
}
