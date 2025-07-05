using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

#nullable disable

namespace Vintagestory.GameContent
{
    public class AiTaskBellAlarm : AiTaskBase
    {
        string[] seekEntityCodesExact = new string[] { "player" };
        string[] seekEntityCodesBeginsWith = Array.Empty<string>();

        int spawnRange;
        float seekingRange = 12;
        EntityProperties[] spawnMobs;
        Entity targetEntity;

        AssetLocation repeatSoundLoc;
        ICoreServerAPI sapi;

        int spawnIntervalMsMin = 2000;
        int spawnIntervalMsMax = 12000;
        int spawnMaxQuantity = 5;

        int nextSpawnIntervalMs;

        List<Entity> spawnedEntities = new List<Entity>();

        public AiTaskBellAlarm(EntityAgent entity) : base(entity)
        {
            sapi = entity.World.Api as ICoreServerAPI;
        }

        public override void LoadConfig(JsonObject taskConfig, JsonObject aiConfig)
        {
            base.LoadConfig(taskConfig, aiConfig);

            spawnRange = taskConfig["spawnRange"].AsInt(12);
            spawnIntervalMsMin = taskConfig["spawnIntervalMsMin"].AsInt(2500);
            spawnIntervalMsMax = taskConfig["spawnIntervalMsMax"].AsInt(12000);
            spawnMaxQuantity = taskConfig["spawnMaxQuantity"].AsInt(5);
            seekingRange = taskConfig["seekingRange"].AsFloat(12);

            var spawnMobLocs = taskConfig["spawnMobs"].AsObject<AssetLocation[]>(Array.Empty<AssetLocation>());
            List<EntityProperties> props = new List<EntityProperties>();
            foreach (var val in spawnMobLocs)
            {
                var etype = sapi.World.GetEntityType(val);
                if (etype == null)
                {
                    sapi.World.Logger.Warning("AiTaskBellAlarm defined spawnmob {0}, but no such entity type found, will ignore.", val);
                    continue;
                }

                props.Add(etype);
            }

            spawnMobs = props.ToArray();

            repeatSoundLoc = !taskConfig["repeatSound"].Exists ? null : AssetLocation.Create(taskConfig["repeatSound"].AsString(), entity.Code.Domain).WithPathPrefixOnce("sounds/");


            string[] codes = taskConfig["onNearbyEntityCodes"].AsArray<string>(new string[] { "player" });

            List<string> exact = new List<string>();
            List<string> beginswith = new List<string>();

            for (int i = 0; i < codes.Length; i++)
            {
                string code = codes[i];
                if (code.EndsWith('*')) beginswith.Add(code.Substring(0, code.Length - 1));
                else exact.Add(code);
            }

            seekEntityCodesExact = exact.ToArray();
            seekEntityCodesBeginsWith = beginswith.ToArray();


            cooldownUntilTotalHours = entity.World.Calendar.TotalHours + mincooldownHours + entity.World.Rand.NextDouble() * (maxcooldownHours - mincooldownHours);
        }

        public override bool ShouldExecute()
        {
            if (entity.World.Rand.NextDouble() > 0.05) return false;
            if (cooldownUntilMs > entity.World.ElapsedMilliseconds) return false;
            if (cooldownUntilTotalHours > entity.World.Calendar.TotalHours) return false;
            if (!PreconditionsSatisifed()) return false;

            float range = seekingRange;
            bool listening = entity.GetBehavior<EntityBehaviorTaskAI>().TaskManager.IsTaskActive("listen");
            if (!listening)
            {
                range /= 3;
            } else
            {
                range *= 1.25f;
            }

            targetEntity = entity.World.GetNearestEntity(entity.ServerPos.XYZ, range, range, (e) => {
                if (!e.Alive || e.EntityId == this.entity.EntityId) return false;

                if (e is EntityPlayer eplr && eplr.Player?.WorldData.CurrentGameMode != EnumGameMode.Creative && (e as EntityPlayer).Player?.WorldData.CurrentGameMode != EnumGameMode.Spectator)
                {
                    bool makingSound = eplr.ServerControls.TriesToMove || eplr.ServerControls.LeftMouseDown || eplr.ServerControls.RightMouseDown || eplr.ServerControls.Jump || !eplr.OnGround || eplr.ServerControls.HandUse != EnumHandInteract.None;
                    bool silent = eplr.ServerControls.TriesToMove && !eplr.ServerControls.LeftMouseDown && !eplr.ServerControls.RightMouseDown && !eplr.ServerControls.Jump && eplr.OnGround && eplr.ServerControls.HandUse == EnumHandInteract.None && eplr.ServerControls.Sneak;

                    if (!makingSound)
                    {
                        double dist = eplr.Pos.DistanceTo(entity.Pos.XYZ);
                        if (dist >= 3 - (listening ? 1 : 0)) return false;
                    } else if (silent)
                    {
                        double dist = eplr.Pos.DistanceTo(entity.Pos.XYZ);
                        if (dist >= 6 - (listening ? 3 : 0)) return false;
                    }

                    return true;
                }


                return false;
            });

            if (targetEntity != null)
            {
                return true;
            }


            return false;
        }


        public override void StartExecute()
        {
            sapi.Network.BroadcastEntityPacket(entity.EntityId, 1025, SerializerUtil.Serialize(repeatSoundLoc));

            nextSpawnIntervalMs = spawnIntervalMsMin + entity.World.Rand.Next(spawnIntervalMsMax - spawnIntervalMsMin);

            base.StartExecute();
        }

        float spawnAccum;

        public override bool ContinueExecute(float dt)
        {
            //Check if time is still valid for task.
            if (!IsInValidDayTimeHours(false)) return false;

            spawnAccum += dt;

            if (spawnAccum > nextSpawnIntervalMs/1000f)
            {
                float playerScaling = sapi.World.GetPlayersAround(entity.ServerPos.XYZ, 15, 10, (plr) => plr.Entity.Alive).Length * sapi.Server.Config.SpawnCapPlayerScaling;

                trySpawnCreatures(GameMath.RoundRandom(sapi.World.Rand, spawnMaxQuantity * playerScaling), spawnRange);

                nextSpawnIntervalMs = spawnIntervalMsMin + entity.World.Rand.Next(spawnIntervalMsMax - spawnIntervalMsMin);

                spawnAccum = 0;
            }

            if (targetEntity.Pos.SquareDistanceTo(entity.Pos) > Math.Pow(seekingRange + 5, 2))
            {
                return false;
            }

            return true;
        }



        public override void FinishExecute(bool cancelled)
        {
            sapi.Network.BroadcastEntityPacket(entity.EntityId, 1026);

            base.FinishExecute(cancelled);
        }

        public override void OnEntityDespawn(EntityDespawnData reason)
        {
            sapi.Network.BroadcastEntityPacket(entity.EntityId, 1026);

            base.OnEntityDespawn(reason);
        }




        CollisionTester collisionTester = new CollisionTester();
        private void trySpawnCreatures(int maxquantity, int range = 13)
        {
            Vec3d centerPos = entity.Pos.XYZ;
            Vec3d spawnPos = new Vec3d();
            BlockPos spawnPosi = new BlockPos();    // Omit dimension, because dimension will come from the InternalY being used in centerPos and spawnPos

            for (int i = 0; i < spawnedEntities.Count; i++)
            {
                if (spawnedEntities[i] == null || !spawnedEntities[i].Alive)
                {
                    spawnedEntities.RemoveAt(i);
                    i--;
                }
            }

            int cnt = spawnedEntities.Count;

            if (cnt <= maxquantity)
            {
                int tries = 50;
                int spawned = 0;
                while (tries-- > 0 && spawned < 1)
                {
                    int index = sapi.World.Rand.Next(spawnMobs.Length);
                    var type = spawnMobs[index];

                    int rndx = sapi.World.Rand.Next(2 * range) - range;
                    int rndy = sapi.World.Rand.Next(2 * range) - range;
                    int rndz = sapi.World.Rand.Next(2 * range) - range;

                    spawnPos.Set((int)centerPos.X + rndx + 0.5, (int)centerPos.Y + rndy + 0.001, (int)centerPos.Z + rndz + 0.5);

                    spawnPosi.Set((int)spawnPos.X, (int)spawnPos.Y, (int)spawnPos.Z);

                    while (sapi.World.BlockAccessor.GetBlockBelow(spawnPosi).Id == 0 && spawnPos.Y > 0)
                    {
                        spawnPosi.Y--;
                        spawnPos.Y--;
                    }

                    if (!sapi.World.BlockAccessor.IsValidPos((int)spawnPos.X, (int)spawnPos.Y, (int)spawnPos.Z)) continue;
                    Cuboidf collisionBox = type.SpawnCollisionBox.OmniNotDownGrowBy(0.1f);
                    if (collisionTester.IsColliding(sapi.World.BlockAccessor, collisionBox, spawnPos, false)) continue;

                    DoSpawn(type, spawnPos, 0);
                    spawned++;
                }
            }
            
        }

        private void DoSpawn(EntityProperties entityType, Vec3d spawnPosition, long herdid)
        {
            Entity entity = sapi.ClassRegistry.CreateEntity(entityType);

            EntityAgent agent = entity as EntityAgent;
            if (agent != null) agent.HerdId = herdid;

            entity.ServerPos.SetPosWithDimension(spawnPosition);
            entity.ServerPos.SetYaw((float)sapi.World.Rand.NextDouble() * GameMath.TWOPI);
            entity.Pos.SetFrom(entity.ServerPos);
            entity.PositionBeforeFalling.Set(entity.ServerPos.X, entity.ServerPos.Y, entity.ServerPos.Z);

            entity.Attributes.SetString("origin", "bellalarm");

            sapi.World.SpawnEntity(entity);

            spawnedEntities.Add(entity);
        }
    }
}
