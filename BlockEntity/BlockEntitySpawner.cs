using ProtoBuf;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public enum EnumSpawnRangeMode
    {
        IgnorePlayerRange,
        WhenInRange,
        WhenOutsideOfRange,
        WithinMinMaxRange
    }

    [ProtoContract]
    public class BESpawnerData
    {
        // Config data

        [ProtoMember(1)]
        public string[] EntityCodes;
        [ProtoMember(2)]
        public Cuboidi SpawnArea;
        [ProtoMember(3)]
        public float InGameHourInterval;
        /// <summary>
        /// Max entities this spawner should spawn
        /// </summary>
        [ProtoMember(4)]
        public int MaxCount;
        /// <summary>
        /// Amount of entities to spawn each interval
        /// </summary>
        [ProtoMember(5), DefaultValue(1)]
        public int GroupSize = 1;
        /// <summary>
        /// If nonzero the spanwer will only spawn this amount of entities and then self destruct
        /// </summary>
        [ProtoMember(6)]
        public int RemoveAfterSpawnCount;
        [ProtoMember(7), DefaultValue(0)]
        public int InitialSpawnQuantity = 0;
        [ProtoMember(8), DefaultValue(true)]
        public bool SpawnOnlyAfterImport = true;


        // Runtime data
        [ProtoMember(9), DefaultValue(false)]
        public bool WasImported = false;
        [ProtoMember(10), DefaultValue(0)]
        public int InitialQuantitySpawned = 0;
        [ProtoMember(11)]
        public int MinPlayerRange = 0;
        [ProtoMember(13)]
        public int InternalCapacity;
        [ProtoMember(14)]
        public double InternalCharge;
        [ProtoMember(15)]
        public double RechargePerHour;
        [ProtoMember(16)]
        public double LastChargeUpdateTotalHours;
        [ProtoMember(17)]
        public double LastSpawnTotalHours;
        [ProtoMember(18)]
        public EnumSpawnRangeMode SpawnRangeMode;
        [ProtoMember(19)]
        public int MaxPlayerRange = -1;

        [ProtoAfterDeserialization]
        void afterDeserialization()
        {
            initDefaults();
        }

        public BESpawnerData initDefaults()
        {
            if (SpawnArea == null)
            {
                SpawnArea = new Cuboidi(-3, 0, -3, 3, 3, 3);
            }
            return this;
        }


        public void ToTreeAttributes(ITreeAttribute tree)
        {
            tree.SetInt("maxCount", MaxCount);
            tree.SetFloat("intervalHours", InGameHourInterval);
            tree["entityCodes"] = new StringArrayAttribute(EntityCodes == null ? new string[0] : EntityCodes);
            tree.SetInt("x1", SpawnArea.X1);
            tree.SetInt("y1", SpawnArea.Y1);
            tree.SetInt("z1", SpawnArea.Z1);
            tree.SetInt("x2", SpawnArea.X2);
            tree.SetInt("y2", SpawnArea.Y2);
            tree.SetInt("z2", SpawnArea.Z2);

            tree.SetInt("minPlayerRange", MinPlayerRange);
            tree.SetInt("maxPlayerRange", MaxPlayerRange);
            tree.SetInt("spawnRangeMode", (int)SpawnRangeMode);

            tree.SetInt("spawnCount", RemoveAfterSpawnCount);
            tree.SetBool("spawnOnlyAfterImport", SpawnOnlyAfterImport);
            tree.SetInt("initialQuantitySpawned", InitialQuantitySpawned);
            tree.SetInt("initialSpawnQuantity", InitialSpawnQuantity);
            tree.SetInt("groupSize", GroupSize);
            tree.SetBool("wasImported", WasImported);
            tree.SetDouble("lastSpawnTotalHours", LastSpawnTotalHours);
            tree.SetInt("internalCapacity", InternalCapacity);
            tree.SetDouble("internalCharge", InternalCharge);
            tree.SetDouble("rechargePerHour", RechargePerHour);
            tree.SetDouble("lastChargeUpdateTotalHours", LastChargeUpdateTotalHours);
        }

        public void FromTreeAttributes(ITreeAttribute tree)
        {
            EntityCodes = (tree["entityCodes"] as StringArrayAttribute)?.value;
            MaxCount = tree.GetInt("maxCount");
            InGameHourInterval = tree.GetFloat("intervalHours");
            SpawnArea = new Cuboidi(
                tree.GetInt("x1"), tree.GetInt("y1"), tree.GetInt("z1"),
                tree.GetInt("x2"), tree.GetInt("y2"), tree.GetInt("z2")
            );

            MinPlayerRange = tree.GetInt("minPlayerRange");
            MaxPlayerRange = tree.GetInt("maxPlayerRange");
            SpawnRangeMode = (EnumSpawnRangeMode)tree.GetInt("spawnRangeMode");

            RemoveAfterSpawnCount = tree.GetInt("spawnCount");
            SpawnOnlyAfterImport = tree.GetBool("spawnOnlyAfterImport");
            GroupSize = tree.GetInt("groupSize");
            InitialQuantitySpawned = tree.GetInt("initialQuantitySpawned");
            InitialSpawnQuantity = tree.GetInt("initialSpawnQuantity");
            WasImported = tree.GetBool("wasImported");
            LastSpawnTotalHours = tree.GetDouble("lastSpawnTotalHours");
            InternalCapacity = tree.GetInt("internalCapacity");
            InternalCharge = tree.GetDouble("internalCharge");
            RechargePerHour = tree.GetDouble("rechargePerHour");
            LastChargeUpdateTotalHours = tree.GetDouble("lastChargeUpdateTotalHours");
        }
    }

    public class BlockEntitySpawner : BlockEntity
    {
        public BESpawnerData Data = new BESpawnerData().initDefaults();

        protected HashSet<long> spawnedEntities = new HashSet<long>();


        protected GuiDialogSpawner dlg;
        protected CollisionTester collisionTester = new CollisionTester();

        protected bool requireSpawnOnWallSide;


        public BlockEntitySpawner()
        {

        }

        protected virtual long GetNextHerdId()
        {
            return (Api as ICoreServerAPI).WorldManager.GetNextUniqueId();
        }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            if (api.Side == EnumAppSide.Server)
            {
                RegisterGameTickListener(OnGameTick, 2000);
                if (Data.InternalCapacity > 0)
                {
                    (api as ICoreServerAPI).Event.OnEntityDespawn += Event_OnEntityDespawn;
                }
            }
        }

        private void Event_OnEntityDespawn(Entity entity, EntityDespawnData despawnData)
        {
            bool alive = entity.GetBehavior<EntityBehaviorHealth>()?.Health > 0; // entity.Alive is false on despawn? o.o
            // Despawning entity recharges the internal capacity
            if (alive && (despawnData == null || despawnData.Reason == EnumDespawnReason.Unload || despawnData.Reason == EnumDespawnReason.Expire))
            {
                if (spawnedEntities.Contains(entity.EntityId))
                {
                    Data.InternalCharge++;
                }
            }
        }

        protected virtual void OnGameTick(float dt)
        {
            if (Data.EntityCodes == null || Data.EntityCodes.Length == 0)
            {
                Data.LastSpawnTotalHours = Api.World.Calendar.TotalHours;
                return;
            }

            if (Data.InternalCapacity > 0)
            {
                double hoursPassed = Api.World.Calendar.TotalHours - Data.LastChargeUpdateTotalHours;
                if (hoursPassed > 0.015)
                {
                    Data.InternalCharge = Math.Min(Data.InternalCapacity, Data.InternalCharge + hoursPassed * Data.RechargePerHour);
                    Data.LastChargeUpdateTotalHours = Api.World.Calendar.TotalHours;
                }
            }

            ICoreServerAPI sapi = Api as ICoreServerAPI;

            int rnd = sapi.World.Rand.Next(Data.EntityCodes.Length);
            EntityProperties type = Api.World.GetEntityType(new AssetLocation(Data.EntityCodes[rnd]));

            if (Data.InternalCapacity > 0 && Data.InternalCharge < 1) return;
            if (Data.LastSpawnTotalHours + Data.InGameHourInterval > Api.World.Calendar.TotalHours && Data.InitialSpawnQuantity <= 0) return;
            if (!IsAreaLoaded()) return;
            if (Data.SpawnOnlyAfterImport && !Data.WasImported) return;

            if (Data.SpawnRangeMode > 0)
            {
                IPlayer player = Api.World.NearestPlayer(Pos.X, Pos.Y, Pos.Z);
                if (player?.Entity?.ServerPos == null) return;
                double distanceSq = player.Entity.ServerPos.SquareDistanceTo(Pos.ToVec3d());

                if (Data.SpawnRangeMode == EnumSpawnRangeMode.WhenInRange && distanceSq > Data.MinPlayerRange * Data.MinPlayerRange) return;
                if (Data.SpawnRangeMode == EnumSpawnRangeMode.WhenOutsideOfRange && distanceSq < Data.MaxPlayerRange * Data.MaxPlayerRange) return;
                if (Data.SpawnRangeMode == EnumSpawnRangeMode.WithinMinMaxRange && (distanceSq < Data.MinPlayerRange * Data.MinPlayerRange || distanceSq > Data.MaxPlayerRange * Data.MaxPlayerRange)) return;
            }

            if (type == null) return;

            var entitylist = spawnedEntities.ToArray();
            for (int i = 0; i < entitylist.Length; i++)
            {
                sapi.World.LoadedEntities.TryGetValue(entitylist[i], out var entity);
                if (entity == null || !entity.Alive)
                {
                    spawnedEntities.Remove(entitylist[i]);
                }
            }

            if (spawnedEntities.Count >= Data.MaxCount)
            {
                Data.LastSpawnTotalHours = Api.World.Calendar.TotalHours;
                return;
            }

            Cuboidf collisionBox = new Cuboidf()
            {
                X1 = -type.CollisionBoxSize.X / 2,
                Z1 = -type.CollisionBoxSize.X / 2,
                X2 = type.CollisionBoxSize.X / 2,
                Z2 = type.CollisionBoxSize.X / 2,
                Y2 = type.CollisionBoxSize.Y
            }.OmniNotDownGrowBy(0.1f);

            Cuboidf collisionBox2 = new Cuboidf()
            {
                X1 = -type.CollisionBoxSize.X / 2,
                Z1 = -type.CollisionBoxSize.X / 2,
                X2 = type.CollisionBoxSize.X / 2,
                Z2 = type.CollisionBoxSize.X / 2,
                Y2 = type.CollisionBoxSize.Y
            };

            int q = Data.GroupSize;
            long herdId = 0;
            Vec3d spawnPos = new Vec3d();
            BlockPos spawnBlockPos = new BlockPos();

            while (q-- > 0)
            {
                for (int tries = 0; tries < 15; tries++)
                {
                    spawnPos.Set(Pos).Add(
                        0.5 + Data.SpawnArea.MinX + Api.World.Rand.NextDouble() * Data.SpawnArea.SizeX,
                        Data.SpawnArea.MinY + Api.World.Rand.NextDouble() * Data.SpawnArea.SizeY,
                        0.5 + Data.SpawnArea.MinZ + Api.World.Rand.NextDouble() * Data.SpawnArea.SizeZ
                    );

                    if (!collisionTester.IsColliding(Api.World.BlockAccessor, collisionBox, spawnPos, false))
                    {
                        if (requireSpawnOnWallSide)
                        {
                            bool haveWall = false;
                            for (int i = 0; !haveWall && i < BlockFacing.NumberOfFaces; i++)
                            {
                                BlockFacing face = BlockFacing.ALLFACES[i];

                                spawnBlockPos.Set(spawnPos).Add(face.Normali);
                                haveWall = Api.World.BlockAccessor.IsSideSolid(spawnBlockPos.X, spawnBlockPos.Y, spawnBlockPos.Z, face.Opposite);
                                if (haveWall)
                                {
                                    Cuboidd entityPos = collisionBox2.ToDouble().Translate(spawnPos);
                                    Cuboidd blockPos = Cuboidf.Default().ToDouble().Translate(spawnBlockPos);
                                    /// North: Negative Z
                                    /// East: Positive X
                                    /// South: Positive Z
                                    /// West: Negative X
                                    /// Up: Positive Y
                                    /// Down: Negative Y

                                    switch (face.Index)
                                    {
                                        case 0:// BlockFacing.NORTH.Index:
                                            spawnPos.Z -= blockPos.Z2 - entityPos.Z1 + 0.01f;
                                            break;
                                        case 1: // BlockFacing.EAST.Index:
                                            spawnPos.X += blockPos.X1 - entityPos.X2 - 0.01f;
                                            break;
                                        case 2: // BlockFacing.SOUTH.Index:
                                            spawnPos.Z += blockPos.Z1 - entityPos.Z2 - 0.01f;
                                            break;
                                        case 3: // BlockFacing.WEST.Index:
                                            spawnPos.X -= blockPos.X2 - entityPos.X1 + 0.01f;
                                            break;
                                        case 4: // BlockFacing.UP.Index:
                                            spawnPos.Y += blockPos.Y1 - entityPos.Y2 - 0.01f;
                                            break;
                                        case 5: // BlockFacing.DOWN.Index:
                                            spawnPos.Y -= blockPos.Y2 - entityPos.Y1 + 0.01f;
                                            break;
                                    }
                                }
                            }
                            if (!haveWall) continue;
                        }

                        if (herdId == 0) herdId = GetNextHerdId();

                        DoSpawn(type, spawnPos, herdId);
                        Data.LastSpawnTotalHours = Api.World.Calendar.TotalHours;

                        if (Data.InitialQuantitySpawned > 0)
                        {
                            Data.InitialQuantitySpawned--;
                        }

                        // Self destruct, if configured so
                        if (Data.RemoveAfterSpawnCount > 0)
                        {
                            Data.RemoveAfterSpawnCount--;
                            if (Data.RemoveAfterSpawnCount == 0)
                            {
                                Api.World.BlockAccessor.SetBlock(0, Pos);
                            }
                        }

                        return;
                    }
                }
            }
        }


        public override void OnBlockPlaced(ItemStack byItemStack = null)
        {
            base.OnBlockPlaced(byItemStack);

            Data.LastSpawnTotalHours = Api.World.Calendar.TotalHours;

            if (byItemStack == null)
            {
                Data.InternalCharge = Data.InternalCapacity;
                return; // Placed by worldgen
            }

            byte[] data = byItemStack.Attributes.GetBytes("spawnerData", null);
            if (data == null) return;

            try
            {
                this.Data = SerializerUtil.Deserialize<BESpawnerData>(data);
            }
            catch
            {
                this.Data = new BESpawnerData().initDefaults();
            }
        }


        public override void OnBlockRemoved()
        {
            base.OnBlockRemoved();
        }


        protected virtual void DoSpawn(EntityProperties entityType, Vec3d spawnPosition, long herdid)
        {
            if (Data.InternalCapacity > 0) Data.InternalCharge--;
            MarkDirty(true);
            Entity entity = Api.World.ClassRegistry.CreateEntity(entityType);

            EntityAgent agent = entity as EntityAgent;
            if (agent != null) agent.HerdId = herdid;

            entity.ServerPos.SetPos(spawnPosition);
            entity.ServerPos.SetYaw((float)Api.World.Rand.NextDouble() * GameMath.TWOPI);
            entity.Pos.SetFrom(entity.ServerPos);
            entity.Attributes.SetString("origin", "entityspawner");
            Api.World.SpawnEntity(entity);

            spawnedEntities.Add(entity.EntityId);
        }

        public bool IsAreaLoaded()
        {
            ICoreServerAPI sapi = Api as ICoreServerAPI;

            int chunksize = Api.World.BlockAccessor.ChunkSize;
            int sizeX = sapi.WorldManager.MapSizeX / chunksize;
            int sizeY = sapi.WorldManager.MapSizeY / chunksize;
            int sizeZ = sapi.WorldManager.MapSizeZ / chunksize;

            int mincx = GameMath.Clamp((Pos.X + Data.SpawnArea.MinX) / chunksize, 0, sizeX - 1);
            int maxcx = GameMath.Clamp((Pos.X + Data.SpawnArea.MaxX) / chunksize, 0, sizeX - 1);
            int mincy = GameMath.Clamp((Pos.Y + Data.SpawnArea.MinY) / chunksize, 0, sizeY - 1);
            int maxcy = GameMath.Clamp((Pos.Y + Data.SpawnArea.MaxY) / chunksize, 0, sizeY - 1);
            int mincz = GameMath.Clamp((Pos.Z + Data.SpawnArea.MinZ) / chunksize, 0, sizeZ - 1);
            int maxcz = GameMath.Clamp((Pos.Z + Data.SpawnArea.MaxZ) / chunksize, 0, sizeZ - 1);

            for (int cx = mincx; cx <= maxcx; cx++)
            {
                for (int cy = mincy; cy <= maxcy; cy++)
                {
                    for (int cz = mincz; cz <= maxcz; cz++)
                    {
                        if (sapi.WorldManager.GetChunk(cx, cy, cz) == null) return false;
                    }
                }
            }

            return true;
        }





        internal void OnInteract(IPlayer byPlayer)
        {
            if (byPlayer.WorldData.CurrentGameMode != EnumGameMode.Creative) return;

            if (Api.Side == EnumAppSide.Server)
            {
                ICoreServerAPI sapi = Api as ICoreServerAPI;
                sapi.Network.SendBlockEntityPacket(byPlayer as IServerPlayer, Pos.X, Pos.Y, Pos.Z, 1000, SerializerUtil.Serialize(Data));
                return;
            }

            dlg = new GuiDialogSpawner(Pos, Api as ICoreClientAPI);
            dlg.spawnerData = Data;
            dlg.TryOpen();
            dlg.OnClosed += () => { dlg?.Dispose(); dlg = null; };
        }

        public override void OnReceivedServerPacket(int packetid, byte[] bytes)
        {
            if (packetid == 1000)
            {
                Data = SerializerUtil.Deserialize<BESpawnerData>(bytes);
                if (dlg?.IsOpened() == true)
                {
                    dlg.UpdateFromServer(Data);
                }
            }
        }

        public override void OnReceivedClientPacket(IPlayer fromPlayer, int packetid, byte[] bytes)
        {
            if (packetid == 1001)
            {
                Data = SerializerUtil.Deserialize<BESpawnerData>(bytes);
                MarkDirty();
            }
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);

            Data.ToTreeAttributes(tree);
            tree["spawnedEntities"] = new LongArrayAttribute(spawnedEntities.ToArray());
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);

            Data = new BESpawnerData();
            Data.FromTreeAttributes(tree);

            long[] values = (tree["spawnedEntities"] as LongArrayAttribute)?.value;
            this.spawnedEntities = new HashSet<long>(values == null ? new long[0] : values);
        }

        public override void OnLoadCollectibleMappings(IWorldAccessor worldForNewMappings, Dictionary<int, AssetLocation> oldBlockIdMapping, Dictionary<int, AssetLocation> oldItemIdMapping, int schematicSeed)
        {
            base.OnLoadCollectibleMappings(worldForNewMappings, oldBlockIdMapping, oldItemIdMapping, schematicSeed);

            object dval;
            worldForNewMappings.Api.ObjectCache.TryGetValue("donotResolveImports", out dval);
            if (dval is bool && (bool)dval) return;

            Data.WasImported = true;
            Data.LastSpawnTotalHours = 0;
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            base.GetBlockInfo(forPlayer, dsc);

            if (Api is ICoreClientAPI capi)
            {
                if (capi.Settings.Bool["extendedDebugInfo"] == true)
                {
                    dsc.AppendLine("Charge capacity: " + Data.InternalCapacity);
                    dsc.AppendLine("Internal charge: " + Data.InternalCharge);
                }
            }
        }
    }
}
