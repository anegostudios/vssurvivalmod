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
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class BlockEntityStaticTranslocator : BlockEntity
    {
        public int MinTeleporterRangeInBlocks = 400;
        public int MaxTeleporterRangeInBlocks = 8000;

        public BlockPos tpLocation;
        Dictionary<long, TeleportingEntity> tpingEntities = new Dictionary<long, TeleportingEntity>();

        BlockStaticTranslocator ownBlock;
        Vec3d posvec;
        long lastCollideMsOwnPlayer;

        TeleporterManager manager;

        ICoreServerAPI sapi;

        int repairState = 0;
        bool activated;
        bool canTeleport = false;
        bool findNextChunk = true;

        ItemStack temporalGearStack;
        NatFloat rndPos = NatFloat.create(EnumDistribution.INVERSEGAUSSIAN, 0, 0.5f);

        public bool Activated
        {
            get { return true; }
        }

        public BlockPos TargetLocation
        {
            get { return tpLocation; }
        }

        public int RepairInteractionsRequired = 4;

        public bool FullyRepaired
        {
            get { return repairState >= RepairInteractionsRequired; }
        }

        BlockEntityAnimationUtil animUtil
        {
            get { return GetBehavior<BEBehaviorAnimatable>().animUtil; }
        }

        public BlockEntityStaticTranslocator()
        {
        }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            manager = api.ModLoader.GetModSystem<TeleporterManager>();

            if (FullyRepaired) setupGameTickers();

            ownBlock = Block as BlockStaticTranslocator;
            posvec = new Vec3d(Pos.X + 0.5, Pos.Y, Pos.Z + 0.5);
            
            if (api.World.Side == EnumAppSide.Client)
            {
                float rotY = Block.Shape.rotateY;
                animUtil.InitializeAnimator("translocator", new Vec3f(0, rotY, 0));

                temporalGearStack = new ItemStack(api.World.GetItem(new AssetLocation("gear-temporal")));
            }
        }

        public void DoActivate()
        {
            activated = true;
            MarkDirty(true);
        }

        public void DoRepair(IPlayer byPlayer)
        {
            if (FullyRepaired) return;

            // Ok. Metal parts are in
            if (repairState == 1)
            {
                int tlgearCostTrait = GameMath.RoundRandom(Api.World.Rand, byPlayer.Entity.Stats.GetBlended("temporalGearTLRepairCost") - 1);

                if (tlgearCostTrait < 0)
                {
                    repairState += -tlgearCostTrait;
                    RepairInteractionsRequired = 4;
                }

                RepairInteractionsRequired += Math.Max(0, tlgearCostTrait);
            }

            repairState++;
            MarkDirty(true);

            if (FullyRepaired)
            {
                activated = true;
                setupGameTickers();
            }
        }
        
        public void setupGameTickers()
        {
            if (Api.Side == EnumAppSide.Server)
            {
                sapi = Api as ICoreServerAPI;
                RegisterGameTickListener(OnServerGameTick, 250);
            }
            else
            {
                RegisterGameTickListener(OnClientGameTick, 50);
            }
        }

        internal void OnEntityCollide(Entity entity)
        {
            if (!FullyRepaired || !Activated || !canTeleport) return;

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
                if ((Api as ICoreClientAPI).World.Player.Entity == entity)
                {
                    lastCollideMsOwnPlayer = Api.World.ElapsedMilliseconds;
                    manager.lastTranslocateCollideMsOwnPlayer = lastCollideMsOwnPlayer;
                }
            }
        }


        float particleAngle = 0;

        private void OnClientGameTick(float dt)
        {
            if (ownBlock == null || Api?.World == null || !canTeleport || !Activated) return;

            if (Api.World.ElapsedMilliseconds - somebodyIsTeleportingReceivedTotalMs > 6000)
            {
                somebodyIsTeleporting = false;
            }


            bool selfInside = (Api.World.ElapsedMilliseconds > 100 && Api.World.ElapsedMilliseconds - lastCollideMsOwnPlayer < 100);
            bool playerInside = selfInside || somebodyIsTeleporting;
            bool active = animUtil.activeAnimationsByAnimCode.ContainsKey("teleport");

            if (!selfInside && playerInside)
            {
                manager.lastTranslocateCollideMsOtherPlayer = Api.World.ElapsedMilliseconds;
            }

            SimpleParticleProperties currentParticles = active ? 
                ownBlock.insideParticles : 
                ownBlock.idleParticles
            ;

            if (playerInside)
            {
                var meta = new AnimationMetaData() { Animation = "teleport", Code = "teleport", AnimationSpeed = 1, EaseInSpeed = 1, EaseOutSpeed = 2, Weight = 1, BlendMode = EnumAnimationBlendMode.Add };
                animUtil.StartAnimation(meta);
                animUtil.StartAnimation(new AnimationMetaData() { Animation = "idle", Code = "idle", AnimationSpeed = 1, EaseInSpeed = 1, EaseOutSpeed = 1, Weight = 1, BlendMode = EnumAnimationBlendMode.Average });
            }
            else
            {
                animUtil.StopAnimation("teleport");
            }


            if (animUtil.activeAnimationsByAnimCode.Count > 0 && Api.World.ElapsedMilliseconds - lastCollideMsOwnPlayer > 10000 && Api.World.ElapsedMilliseconds - manager.lastTranslocateCollideMsOtherPlayer > 10000)
            {
                animUtil.StopAnimation("idle");
            }


            //int color = temporalGearStack.Collectible.GetRandomColor(api as ICoreClientAPI, temporalGearStack); - not working o.O

            int r = 53;
            int g = 221;
            int b = 172;
            currentParticles.Color = (r << 16) | (g << 8) | (b << 0) | (50 << 24);
            
            currentParticles.AddPos.Set(0, 0, 0);
            currentParticles.BlueEvolve = null;
            currentParticles.RedEvolve = null;
            currentParticles.GreenEvolve = null;
            currentParticles.MinSize = 0.1f;
            currentParticles.MaxSize = 0.2f;
            currentParticles.SizeEvolve = null;
            currentParticles.OpacityEvolve = EvolvingNatFloat.create(EnumTransformFunction.LINEAR, 100f);


            bool rot = Block.Variant["side"] == "east" || Block.Variant["side"] == "west";

            particleAngle = active ? particleAngle + 5 * dt : 0;


            double dx = GameMath.Cos(particleAngle + (rot ? GameMath.PIHALF : 0)) * 0.35f;
            double dy = 1.9 + Api.World.Rand.NextDouble() * 0.2;
            double dz = GameMath.Sin(particleAngle + (rot ? GameMath.PIHALF : 0)) * 0.35f;

            currentParticles.LifeLength = GameMath.Sqrt(dx*dx + dz*dz) / 10;
            currentParticles.MinPos.Set(posvec.X + dx, posvec.Y + dy, posvec.Z + dz);
            currentParticles.MinVelocity.Set(-(float)dx/2, -1 - (float)Api.World.Rand.NextDouble()/2, -(float)dz/2);
            currentParticles.MinQuantity = active ? 3 : 0.25f;
            currentParticles.AddVelocity.Set(0, 0, 0);
            currentParticles.AddQuantity = 0.5f;

            Api.World.SpawnParticles(currentParticles);

            currentParticles.MinPos.Set(posvec.X - dx, posvec.Y + dy, posvec.Z - dz);
            currentParticles.MinVelocity.Set((float)dx / 2, -1 - (float)Api.World.Rand.NextDouble() / 2, (float)dz / 2);
            Api.World.SpawnParticles(currentParticles);
        }


        private void OnServerGameTick(float dt)
        {            
            if (findNextChunk)
            {
                findNextChunk = false;

                int addrange = MaxTeleporterRangeInBlocks - MinTeleporterRangeInBlocks;

                int dx = (int)(MinTeleporterRangeInBlocks + sapi.World.Rand.NextDouble() * addrange) * (2 * sapi.World.Rand.Next(2) - 1);
                int dz = (int)(MinTeleporterRangeInBlocks + sapi.World.Rand.NextDouble() * addrange) * (2 * sapi.World.Rand.Next(2) - 1);

                int chunkX = (Pos.X + dx) / sapi.World.BlockAccessor.ChunkSize;
                int chunkZ = (Pos.Z + dz) / sapi.World.BlockAccessor.ChunkSize;
                
                if (!sapi.World.BlockAccessor.IsValidPos(Pos.X + dx, 1, Pos.Z + dz))
                {
                    findNextChunk = true;
                    return;
                }

                ChunkPeekOptions opts = new ChunkPeekOptions()
                {
                    OnGenerated = (chunks) => TestForExitPoint(chunks, chunkX, chunkZ),
                    UntilPass = EnumWorldGenPass.TerrainFeatures,
                    ChunkGenParams = chunkGenParams()
                };

                sapi.WorldManager.PeekChunkColumn(chunkX, chunkZ, opts);
            }

            if (canTeleport && Activated)
            {
                HandleTeleporting(dt);
            }
        }


        ITreeAttribute chunkGenParams()
        {
            TreeAttribute tree = new TreeAttribute();
            TreeAttribute subtree;
            tree["structureChanceModifier"] = subtree = new TreeAttribute();
            subtree.SetFloat("gates", 10);

            tree["structureMaxCount"] = subtree = new TreeAttribute();
            subtree.SetInt("gates", 1);

            return tree;
        }


        private void TestForExitPoint(Dictionary<Vec2i, IServerChunk[]> columnsByChunkCoordinate, int centerCx, int centerCz)
        {
            BlockPos pos = HasExitPoint(columnsByChunkCoordinate, centerCx, centerCz);

            if (pos == null)
            {
                findNextChunk = true;
            }
            else
            {
                sapi.WorldManager.LoadChunkColumnPriority(centerCx, centerCz, new ChunkLoadOptions() {
                    ChunkGenParams = chunkGenParams(),
                    OnLoaded = () => exitChunkLoaded(pos)
                });
            }             
        }

        private void exitChunkLoaded(BlockPos exitPos)
        {
            BlockStaticTranslocator exitBlock = Api.World.BlockAccessor.GetBlock(exitPos) as BlockStaticTranslocator;

            if (exitBlock == null)
            {
                // Cheap hax: Pre v1.10 chunks do not have translocators at the same location and maybe future versions will also have a changed location
                // So let's still try to find something useful in the chunk we generated, with any luck we come across an old one. 
                exitPos = HasExitPoint(exitPos);
                if (exitPos != null)
                {
                    exitBlock = Api.World.BlockAccessor.GetBlock(exitPos) as BlockStaticTranslocator;
                }
            }
            
            if (exitBlock != null && !exitBlock.Repaired)
            {
                // Repair it
                Api.World.BlockAccessor.SetBlock(ownBlock.Id, exitPos);
                BlockEntityStaticTranslocator beExit = Api.World.BlockAccessor.GetBlockEntity(exitPos) as BlockEntityStaticTranslocator;

                // Connect remote
                beExit.tpLocation = Pos.Copy();
                beExit.canTeleport = true;
                beExit.findNextChunk = false;
                beExit.activated = true;
                if (!beExit.FullyRepaired)
                {
                    beExit.repairState = 4;
                    beExit.setupGameTickers();
                }
                Api.World.BlockAccessor.MarkBlockEntityDirty(exitPos);
                Api.World.BlockAccessor.MarkBlockDirty(exitPos);

                Api.World.Logger.Debug("Connected translocator at {0} (chunkpos: {2}) to my location: {1}", exitPos, Pos, exitPos / 32);

                // Connect self
                MarkDirty(true);
                tpLocation = exitPos;
                canTeleport = true;
            } else
            {
                Api.World.Logger.Warning("Translocator: Regen chunk but broken translocator is gone. Structure generation perhaps seed not consistent? May also just be pre-v1.10 chunk, so probably nothing to worry about. Searching again...");
                findNextChunk = true;
            }
        }

        private BlockPos HasExitPoint(BlockPos nearpos)
        {
            IServerChunk chunk = Api.World.BlockAccessor.GetChunkAtBlockPos(nearpos) as IServerChunk;          
            List<GeneratedStructure> structures = chunk?.MapChunk?.MapRegion?.GeneratedStructures;

            if (structures == null) return null;

            foreach (var structure in structures)
            {
                if (structure.Code.Contains("gates"))
                {
                    Cuboidi loc = structure.Location;
                    BlockPos foundPos = null;
                    Api.World.BlockAccessor.SearchBlocks(loc.Start.AsBlockPos, loc.End.AsBlockPos, (block, pos) =>
                    {
                        BlockStaticTranslocator transBlock = block as BlockStaticTranslocator;
                        
                        if (transBlock != null && !transBlock.Repaired)
                        {
                            foundPos = pos.Copy();
                            return false;
                        }

                        return true;
                    });

                    if (foundPos != null) return foundPos;
                }
            }

            return null;
        }

        private BlockPos HasExitPoint(Dictionary<Vec2i, IServerChunk[]> columnsByChunkCoordinate, int centerCx, int centerCz)
        {
            IMapRegion mapregion = columnsByChunkCoordinate[new Vec2i(centerCx, centerCz)][0].MapChunk.MapRegion;

            List<GeneratedStructure> structures = mapregion.GeneratedStructures;
            foreach (var structure in structures)
            {
                if (structure.Code.Contains("gates"))
                {
                    BlockPos pos = FindTranslocator(structure.Location, columnsByChunkCoordinate, centerCx, centerCz);
                    if (pos != null) return pos;
                }
            }

            return null;
        }

        private BlockPos FindTranslocator(Cuboidi location, Dictionary<Vec2i, IServerChunk[]> columnsByChunkCoordinate, int centerCx, int centerCz)
        {
            int chunksize = Api.World.BlockAccessor.ChunkSize;
            
            for (int x = location.X1; x < location.X2; x++)
            {
                for (int y = location.Y1; y < location.Y2; y++)
                {
                    for (int z = location.Z1; z < location.Z2; z++)
                    {
                        int cx = x / chunksize;
                        int cz = z / chunksize;

                        IServerChunk[] chunks;
                        if (!columnsByChunkCoordinate.TryGetValue(new Vec2i(cx, cz), out chunks))
                        {
                            continue;
                        }

                        IServerChunk chunk = chunks[y / chunksize];

                        int lx = x % chunksize;
                        int ly = y % chunksize;
                        int lz = z % chunksize;

                        int index3d = (ly * chunksize + lz) * chunksize + lx;
                        Block block = Api.World.Blocks[chunk.Blocks[index3d]];

                        BlockStaticTranslocator transBlock = block as BlockStaticTranslocator;
                        if (transBlock != null && !transBlock.Repaired)
                        {
                            return new BlockPos(x, y, z);
                        }
                    }
                }
            }

            return null;
        }

        public long MapRegionIndex2D(int regionX, int regionZ)
        {
            return ((long)regionZ) * (sapi.WorldManager.MapSizeX / sapi.WorldManager.RegionSize) + regionX;
        }



        List<long> toremove = new List<long>();
        bool somebodyIsTeleporting;
        bool somebodyDidTeleport;

        void HandleTeleporting(float dt)
        { 
            toremove.Clear();

            bool wasTeleporting = somebodyIsTeleporting;

            somebodyIsTeleporting &= tpingEntities.Count > 0;

            foreach (var val in tpingEntities)
            {
                if (val.Value.Entity.Teleporting) continue;

                val.Value.SecondsPassed += Math.Min(0.5f, dt);

                if (Api.World.ElapsedMilliseconds - val.Value.LastCollideMs > 100)
                {
                    // Make sure its not just server lag
                    Block block = Api.World.CollisionTester.GetCollidingBlock(Api.World.BlockAccessor, val.Value.Entity.CollisionBox, val.Value.Entity.Pos.XYZ, true);
                    if (!(block is BlockStaticTranslocator))
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

                if (val.Value.SecondsPassed > 1.5 && tpLocation != null)
                {
                    // Preload the chunk
                    IWorldChunk chunk = sapi.World.BlockAccessor.GetChunkAtBlockPos(tpLocation);
                    if (chunk != null)
                    {
                        chunk.MapChunk.MarkFresh();
                    }
                    else
                    {
                        sapi.WorldManager.LoadChunkColumnPriority((int)tpLocation.X / Api.World.BlockAccessor.ChunkSize, (int)tpLocation.Z / Api.World.BlockAccessor.ChunkSize, new ChunkLoadOptions()
                        {
                            KeepLoaded = false
                        });
                    }
                }

                if (val.Value.SecondsPassed > 4.4 && tpLocation != null)
                {
                    val.Value.Entity.TeleportTo(tpLocation.ToVec3d().Add(-0.3, 1, -0.3)); // Fugly, need some better exit pos thing

                    Entity e = val.Value.Entity;
                    if (e is EntityPlayer)
                    {
                        Api.World.Logger.Debug("Teleporting player {0} to {1}", (e as EntityPlayer).GetBehavior<EntityBehaviorNameTag>().DisplayName, tpLocation);
                        manager.DidTranslocateServer((e as EntityPlayer).Player as IServerPlayer);
                    } else
                    {
                        Api.World.Logger.Debug("Teleporting entity {0} to {1}", e.Code, tpLocation);
                    }

                    toremove.Add(val.Key);

                    activated = false;
                    somebodyIsTeleporting = false;
                    somebodyDidTeleport = true;

                    ownBlock.teleportParticles.MinPos.Set(Pos.X, Pos.Y, Pos.Z);
                    ownBlock.teleportParticles.AddPos.Set(1, 1.8, 1);
                    ownBlock.teleportParticles.MinVelocity.Set(-1, -1, -1);
                    ownBlock.teleportParticles.AddVelocity.Set(2, 2, 2);
                    ownBlock.teleportParticles.MinQuantity = 150;
                    ownBlock.teleportParticles.AddQuantity = 0.5f;
                    

                    int r = 53;
                    int g = 221;
                    int b = 172;
                    ownBlock.teleportParticles.Color = (r << 16) | (g << 8) | (b << 0) | (100 << 24);

                    ownBlock.teleportParticles.BlueEvolve = null;
                    ownBlock.teleportParticles.RedEvolve = null;
                    ownBlock.teleportParticles.GreenEvolve = null;
                    ownBlock.teleportParticles.MinSize = 0.1f;
                    ownBlock.teleportParticles.MaxSize = 0.2f;
                    ownBlock.teleportParticles.SizeEvolve = null;
                    ownBlock.teleportParticles.OpacityEvolve = EvolvingNatFloat.create(EnumTransformFunction.QUADRATIC, -10f);


                    Api.World.SpawnParticles(ownBlock.teleportParticles);


                    MarkDirty();
                }
            }

            foreach(long entityid in toremove)
            {
                tpingEntities.Remove(entityid);
            }

            if (wasTeleporting && !somebodyIsTeleporting)
            {
                MarkDirty();
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
        }

        long somebodyIsTeleportingReceivedTotalMs;

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);

            canTeleport = tree.GetBool("canTele");
            repairState = tree.GetInt("repairState");
            findNextChunk = tree.GetBool("findNextChunk", true);
            activated = tree.GetBool("activated");
            somebodyIsTeleporting = tree.GetBool("somebodyIsTeleporting");


            if (canTeleport) {
                tpLocation = new BlockPos(tree.GetInt("teleX"), tree.GetInt("teleY"), tree.GetInt("teleZ"));

                if (tpLocation.X == 0 && tpLocation.Z == 0) tpLocation = null; // For safety
            }

            if (worldAccessForResolve != null && worldAccessForResolve.Side == EnumAppSide.Client)
            {
                somebodyIsTeleportingReceivedTotalMs = worldAccessForResolve.ElapsedMilliseconds;

                if (tree.GetBool("somebodyDidTeleport"))
                {
                    // Might get called from the SystemNetworkProcess thread
                    worldAccessForResolve.Api.Event.EnqueueMainThreadTask(
                        () => worldAccessForResolve.PlaySoundAt(new AssetLocation("sounds/effect/translocate-breakdimension"), Pos.X + 0.5f, Pos.Y + 0.5f, Pos.Z + 0.5f, null, false, 16),
                        "playtelesound"
                    );
                }
            }

            /*if (worldAccessForResolve.Side == EnumAppSide.Server)
            {
                api.World.Logger.Debug("Translocator FromTreeAttributes. Pos {0} (chunkpos {1})    - tpLocation: {2}", pos, pos / 32, tpLocation);
            }*/
            
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);

            tree.SetBool("canTele", canTeleport);
            tree.SetInt("repairState", repairState);
            tree.SetBool("findNextChunk", findNextChunk);
            tree.SetBool("activated", activated);
            tree.SetBool("somebodyIsTeleporting", somebodyIsTeleporting);
            tree.SetBool("somebodyDidTeleport", somebodyDidTeleport);
            somebodyDidTeleport = false;
                

            if (tpLocation != null)
            {
                tree.SetInt("teleX", tpLocation.X);
                tree.SetInt("teleY", tpLocation.Y);
                tree.SetInt("teleZ", tpLocation.Z);
            }
        }


        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tessThreadTesselator)
        {
            if (animUtil.activeAnimationsByAnimCode.Count > 0 || (animUtil.animator != null && animUtil.animator.ActiveAnimationCount > 0))
            {
                return true;
            }

            if (!FullyRepaired)
            {
                MeshData mesh = ObjectCacheUtil.GetOrCreate(Api, "statictranslocator-" + repairState + "-" + ownBlock.Shape.rotateY, () =>
                {
                    float rotY = ownBlock.Shape.rotateY;

                    ICoreClientAPI capi = Api as ICoreClientAPI;

                    string shapeCode = "normal";
                    switch (repairState)
                    {
                        case 0: shapeCode = "broken"; break;
                        case 1: shapeCode = "repairstate1"; break;
                        case 2: shapeCode = "repairstate2"; break;
                        case 3: shapeCode = "repairstate3"; break;
                    }

                    MeshData meshdata;
                    IAsset asset = Api.Assets.TryGet(new AssetLocation("shapes/block/machine/statictranslocator/" + shapeCode + ".json"));
                    Shape shape = asset.ToObject<Shape>();

                    tessThreadTesselator.TesselateShape(ownBlock, shape, out meshdata, new Vec3f(0, rotY, 0));

                    return meshdata;
                });

                mesher.AddMeshData(mesh);
                return true;
            }


            return false;
        }



        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            if (!FullyRepaired)
            {
                dsc.AppendLine(Lang.Get("Seems to be missing a couple of gears. I think I've seen such gears before."));
                return;
            }
            else
            {
                if (tpLocation == null)
                {
                    string[] lines = new string[] { Lang.Get("Warping spacetime."), Lang.Get("Warping spacetime.."), Lang.Get("Warping spacetime...") };

                    dsc.AppendLine(lines[(int)(Api.World.ElapsedMilliseconds / 1000f) % 3]);
                    return;
                }
            }

            if (forPlayer.WorldData.CurrentGameMode == EnumGameMode.Creative)
            {
                BlockPos pos = Api.World.DefaultSpawnPosition.AsBlockPos;
                dsc.AppendLine(Lang.Get("Teleports to {0}", tpLocation.Copy().Sub(pos.X, 0, pos.Z)));
            }
            else
            {
                dsc.AppendLine(Lang.Get("Spacetime subduction completed."));
            }
        }
    }
}
