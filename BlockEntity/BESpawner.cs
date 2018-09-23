using ProtoBuf;
using System;
using System.Collections.Generic;
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
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class BESpawnerData
    {
        public string EntityCode;
        public Cuboidi SpawnArea = new Cuboidi(-3, 0, -3, 3, 3, 3);
        public float InGameHourInterval;
        public int MaxCount;
    }

    public class BlockEntitySpawner : BlockEntity
    {
        BESpawnerData data = new BESpawnerData();

        List<long> spawnedEntities = new List<long>();
        double lastSpawnTotalHours;

        GuiDialogSpawner dlg;
        CollisionTester collisionTester = new CollisionTester();

        public BlockEntitySpawner()
        {

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
            if (data.EntityCode == null) return;
            if (lastSpawnTotalHours + data.InGameHourInterval > api.World.Calendar.TotalHours) return;
            if (!IsAreaLoaded()) return;

            
            ICoreServerAPI sapi = api as ICoreServerAPI;
            EntityProperties type = api.World.GetEntityType(new AssetLocation(data.EntityCode));
            if (type == null) return;

            for (int i = 0; i < spawnedEntities.Count; i++)
            {
                if (!sapi.World.LoadedEntities.ContainsKey(spawnedEntities[i])) {
                    spawnedEntities.RemoveAt(i);
                    i--;
                }
            }

            if (spawnedEntities.Count >= data.MaxCount)
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

            Vec3d spawnPos = new Vec3d();
            for (int tries = 0; tries < 15; tries++)
            {
                spawnPos.Set(pos).Add(
                    0.5 + data.SpawnArea.MinX + api.World.Rand.NextDouble() * data.SpawnArea.SizeX,
                    data.SpawnArea.MinY + api.World.Rand.NextDouble() * data.SpawnArea.SizeY,
                    0.5 + data.SpawnArea.MinZ + api.World.Rand.NextDouble() * data.SpawnArea.SizeZ
                );


                if (!collisionTester.IsColliding(api.World.BlockAccessor, collisionBox, spawnPos, false))
                {
                    long herdid = sapi.WorldManager.GetNextHerdId();
                    DoSpawn(type, spawnPos, herdid);
                    lastSpawnTotalHours = api.World.Calendar.TotalHours;
                    return;
                }
            }
        }


        private void DoSpawn(EntityProperties entityType, Vec3d spawnPosition, long herdid)
        {
            Entity entity = api.World.ClassRegistry.CreateEntity(entityType);

            EntityAgent agent = entity as EntityAgent;
            if (agent != null) agent.HerdId = herdid;

            entity.ServerPos.SetPos(spawnPosition);
            entity.ServerPos.SetYaw(api.World.Rand.Next() * GameMath.TWOPI);
            entity.Pos.SetFrom(entity.ServerPos);
            entity.Attributes.SetString("origin", "entityspawner");
            api.World.SpawnEntity(entity);

            spawnedEntities.Add(entity.EntityId);

            //Console.WriteLine("spawn " + entityType.Code);
        }

        public bool IsAreaLoaded()
        {
            ICoreServerAPI sapi = api as ICoreServerAPI;

            int chunksize = api.World.BlockAccessor.ChunkSize;
            int sizeX = sapi.WorldManager.MapSizeX / chunksize;
            int sizeY = sapi.WorldManager.MapSizeY / chunksize;
            int sizeZ = sapi.WorldManager.MapSizeZ / chunksize;

            int mincx = GameMath.Clamp((pos.X + data.SpawnArea.MinX) / chunksize, 0, sizeX - 1);
            int maxcx = GameMath.Clamp((pos.Y + data.SpawnArea.MaxX) / chunksize, 0, sizeX - 1);
            int mincy = GameMath.Clamp((pos.Z + data.SpawnArea.MinY) / chunksize, 0, sizeY - 1);
            int maxcy = GameMath.Clamp((pos.X + data.SpawnArea.MaxY) / chunksize, 0, sizeY - 1);
            int mincz = GameMath.Clamp((pos.Y + data.SpawnArea.MinZ) / chunksize, 0, sizeZ - 1);
            int maxcz = GameMath.Clamp((pos.Z + data.SpawnArea.MaxZ) / chunksize, 0, sizeZ - 1);
            
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
                sapi.Network.SendBlockEntityPacket(byPlayer as IServerPlayer, pos.X, pos.Y, pos.Z, 1000, SerializerUtil.Serialize(data));
                return;
            }

            dlg = new GuiDialogSpawner(pos, api as ICoreClientAPI);
            dlg.spawnerData = data;
            dlg.TryOpen();
        }

        public override void OnReceivedServerPacket(int packetid, byte[] bytes)
        {
            if (packetid == 1000)
            {
                data = SerializerUtil.Deserialize<BESpawnerData>(bytes);
                if (dlg?.IsOpened() == true)
                {
                    dlg.UpdateFromServer(data);
                }
            }
        }

        public override void OnReceivedClientPacket(IPlayer fromPlayer, int packetid, byte[] bytes)
        {
            if (packetid == 1001)
            {
                data = SerializerUtil.Deserialize<BESpawnerData>(bytes);
                MarkDirty();
            }
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);

            tree.SetInt("maxCount", data.MaxCount);
            tree.SetFloat("intervalHours", data.InGameHourInterval);
            tree.SetString("entityCode", data.EntityCode == null ? "" : data.EntityCode);
            tree.SetInt("x1", data.SpawnArea.X1);
            tree.SetInt("y1", data.SpawnArea.Y1);
            tree.SetInt("z1", data.SpawnArea.Z1);

            tree.SetInt("x2", data.SpawnArea.X2);
            tree.SetInt("y2", data.SpawnArea.Y2);
            tree.SetInt("z2", data.SpawnArea.Z2);

            tree["spawnedEntities"] = new LongArrayAttribute(this.spawnedEntities.ToArray());
        }

        public override void FromTreeAtributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAtributes(tree, worldAccessForResolve);

            data = new BESpawnerData()
            {
                EntityCode = tree.GetString("entityCode"),
                MaxCount = tree.GetInt("maxCount"),
                InGameHourInterval = tree.GetFloat("intervalHours"),
                SpawnArea = new Cuboidi(
                    tree.GetInt("x1"), tree.GetInt("y1"), tree.GetInt("z1"),
                    tree.GetInt("x2"), tree.GetInt("y2"), tree.GetInt("z2")
                )
            };

            long[] values = (tree["spawnedEntities"] as LongArrayAttribute)?.value;

            this.spawnedEntities = new List<long>(values == null ? new long[0] : values);
        }
    }
}
