using ProtoBuf;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
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
        public bool SpawnOnlyAfterImport=true;


        // Runtime data
        [ProtoMember(9), DefaultValue(false)]
        public bool WasImported = false;
        [ProtoMember(10), DefaultValue(0)]
        public int InitialQuantitySpawned = 0;


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
    }

    public class BlockEntitySpawner : BlockEntity
    {
        public BESpawnerData Data = new BESpawnerData().initDefaults();

        List<long> spawnedEntities = new List<long>();
        double lastSpawnTotalHours;

        GuiDialogSpawner dlg;
        CollisionTester collisionTester = new CollisionTester();

        public BlockEntitySpawner()
        {

        }

        protected virtual long GetNextHerdId()
        {
            return (api as ICoreServerAPI).WorldManager.GetNextUniqueId();
        }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            if (api.Side == EnumAppSide.Server)
            {
                RegisterGameTickListener(OnGameTick, 2000);
            }
        }

        private void OnGameTick(float dt)
        {
            if (Data.EntityCodes == null || Data.EntityCodes.Length == 0)
            {
                lastSpawnTotalHours = api.World.Calendar.TotalHours;
                return;
            }


            ICoreServerAPI sapi = api as ICoreServerAPI;

            int rnd = sapi.World.Rand.Next(Data.EntityCodes.Length);

            EntityProperties type = api.World.GetEntityType(new AssetLocation(Data.EntityCodes[rnd]));

            if (lastSpawnTotalHours + Data.InGameHourInterval > api.World.Calendar.TotalHours && Data.InitialSpawnQuantity <= 0) return;
            if (!IsAreaLoaded()) return;
            if (Data.SpawnOnlyAfterImport && !Data.WasImported) return;
           
            if (type == null) return;

            for (int i = 0; i < spawnedEntities.Count; i++)
            {
                if (!sapi.World.LoadedEntities.ContainsKey(spawnedEntities[i])) {
                    spawnedEntities.RemoveAt(i);
                    i--;
                }
            }

            if (spawnedEntities.Count >= Data.MaxCount)
            {
                lastSpawnTotalHours = api.World.Calendar.TotalHours;
                return;
            }

            Cuboidf collisionBox = new Cuboidf()
            {
                X1 = -type.HitBoxSize.X / 2,
                Z1 = -type.HitBoxSize.X / 2,
                X2 = type.HitBoxSize.X / 2,
                Z2 = type.HitBoxSize.X / 2,
                Y2 = type.HitBoxSize.Y
            }.OmniNotDownGrowBy(0.1f);

            int q = Data.GroupSize;
            long herdId = 0;

            while (q-- > 0)
            {

                Vec3d spawnPos = new Vec3d();
                for (int tries = 0; tries < 15; tries++)
                {
                    spawnPos.Set(pos).Add(
                        0.5 + Data.SpawnArea.MinX + api.World.Rand.NextDouble() * Data.SpawnArea.SizeX,
                        Data.SpawnArea.MinY + api.World.Rand.NextDouble() * Data.SpawnArea.SizeY,
                        0.5 + Data.SpawnArea.MinZ + api.World.Rand.NextDouble() * Data.SpawnArea.SizeZ
                    );

                    if (!collisionTester.IsColliding(api.World.BlockAccessor, collisionBox, spawnPos, false))
                    {
                        if (herdId == 0) herdId = GetNextHerdId();

                        DoSpawn(type, spawnPos, herdId);
                        lastSpawnTotalHours = api.World.Calendar.TotalHours;

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
                                api.World.BlockAccessor.SetBlock(0, pos);
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

            lastSpawnTotalHours = api.World.Calendar.TotalHours;

            if (byItemStack == null) return;
            byte[] data = byItemStack.Attributes.GetBytes("spawnerData", null);
            if (data == null) return;

            try
            {
                this.Data = SerializerUtil.Deserialize<BESpawnerData>(data);
            }
            catch {
                this.Data = new BESpawnerData().initDefaults();
            }    
        }


        public override void OnBlockRemoved()
        {
            base.OnBlockRemoved();
        }


        protected virtual void DoSpawn(EntityProperties entityType, Vec3d spawnPosition, long herdid)
        {
            Entity entity = api.World.ClassRegistry.CreateEntity(entityType);

            EntityAgent agent = entity as EntityAgent;
            if (agent != null) agent.HerdId = herdid;

            entity.ServerPos.SetPos(spawnPosition);
            entity.ServerPos.SetYaw((float)api.World.Rand.NextDouble() * GameMath.TWOPI);
            entity.Pos.SetFrom(entity.ServerPos);
            entity.Attributes.SetString("origin", "entityspawner");
            api.World.SpawnEntity(entity);

            spawnedEntities.Add(entity.EntityId);
        }

        public bool IsAreaLoaded()
        {
            ICoreServerAPI sapi = api as ICoreServerAPI;

            int chunksize = api.World.BlockAccessor.ChunkSize;
            int sizeX = sapi.WorldManager.MapSizeX / chunksize;
            int sizeY = sapi.WorldManager.MapSizeY / chunksize;
            int sizeZ = sapi.WorldManager.MapSizeZ / chunksize;

            int mincx = GameMath.Clamp((pos.X + Data.SpawnArea.MinX) / chunksize, 0, sizeX - 1);
            int maxcx = GameMath.Clamp((pos.Y + Data.SpawnArea.MaxX) / chunksize, 0, sizeX - 1);
            int mincy = GameMath.Clamp((pos.Z + Data.SpawnArea.MinY) / chunksize, 0, sizeY - 1);
            int maxcy = GameMath.Clamp((pos.X + Data.SpawnArea.MaxY) / chunksize, 0, sizeY - 1);
            int mincz = GameMath.Clamp((pos.Y + Data.SpawnArea.MinZ) / chunksize, 0, sizeZ - 1);
            int maxcz = GameMath.Clamp((pos.Z + Data.SpawnArea.MaxZ) / chunksize, 0, sizeZ - 1);
            
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

            if (api.Side == EnumAppSide.Server)
            {
                ICoreServerAPI sapi = api as ICoreServerAPI;
                sapi.Network.SendBlockEntityPacket(byPlayer as IServerPlayer, pos.X, pos.Y, pos.Z, 1000, SerializerUtil.Serialize(Data));
                return;
            }

            dlg = new GuiDialogSpawner(pos, api as ICoreClientAPI);
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

            tree.SetInt("maxCount", Data.MaxCount);
            tree.SetFloat("intervalHours", Data.InGameHourInterval);

            tree.SetDouble("lastSpawnTotalHours", lastSpawnTotalHours);

            tree["entityCodes"] = new StringArrayAttribute(Data.EntityCodes == null ? new string[0] : Data.EntityCodes);

            tree.SetInt("x1", Data.SpawnArea.X1);
            tree.SetInt("y1", Data.SpawnArea.Y1);
            tree.SetInt("z1", Data.SpawnArea.Z1);

            tree.SetInt("x2", Data.SpawnArea.X2);
            tree.SetInt("y2", Data.SpawnArea.Y2);
            tree.SetInt("z2", Data.SpawnArea.Z2);
            tree.SetInt("spawnCount", Data.RemoveAfterSpawnCount);
            tree.SetBool("spawnOnlyAfterImport", Data.SpawnOnlyAfterImport);
            tree.SetInt("initialQuantitySpawned", Data.InitialQuantitySpawned);
            tree.SetInt("initialSpawnQuantity", Data.InitialSpawnQuantity);
            tree.SetInt("groupSize", Data.GroupSize);
            tree.SetBool("wasImported", Data.WasImported);

            tree["spawnedEntities"] = new LongArrayAttribute(this.spawnedEntities.ToArray());
        }

        public override void FromTreeAtributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAtributes(tree, worldAccessForResolve);
            
            Data = new BESpawnerData()
            {
                EntityCodes = (tree["entityCodes"] as StringArrayAttribute)?.value,
                MaxCount = tree.GetInt("maxCount"),
                InGameHourInterval = tree.GetFloat("intervalHours"),
                SpawnArea = new Cuboidi(
                    tree.GetInt("x1"), tree.GetInt("y1"), tree.GetInt("z1"),
                    tree.GetInt("x2"), tree.GetInt("y2"), tree.GetInt("z2")
                ),
                RemoveAfterSpawnCount = tree.GetInt("spawnCount"),
                SpawnOnlyAfterImport = tree.GetBool("spawnOnlyAfterImport"),
                GroupSize = tree.GetInt("groupSize"),
                InitialQuantitySpawned = tree.GetInt("initialQuantitySpawned"),
                InitialSpawnQuantity = tree.GetInt("initialSpawnQuantity"),
                WasImported = tree.GetBool("wasImported"),
            };

            long[] values = (tree["spawnedEntities"] as LongArrayAttribute)?.value;

            lastSpawnTotalHours = tree.GetDecimal("lastSpawnTotalHours");

            this.spawnedEntities = new List<long>(values == null ? new long[0] : values);
        }

        public override void OnLoadCollectibleMappings(IWorldAccessor worldForNewMappings, Dictionary<int, AssetLocation> oldBlockIdMapping, Dictionary<int, AssetLocation> oldItemIdMapping)
        {
            base.OnLoadCollectibleMappings(worldForNewMappings, oldBlockIdMapping, oldItemIdMapping);

            object dval = null;
            worldForNewMappings.Api.ObjectCache.TryGetValue("donotResolveImports", out dval);
            if (dval is bool && (bool)dval) return;

            Data.WasImported = true;
            lastSpawnTotalHours = 0;
        }
    }
}
