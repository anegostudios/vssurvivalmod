using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.ServerMods;

namespace Vintagestory.GameContent
{
    public class BlockEntityLocustNest : BlockEntitySpawner
    {
        long herdId;
        int insideLocustCount;

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            requireSpawnOnWallSide = true;
        }

        // Remember the herdid forever
        protected override long GetNextHerdId()
        {
            if (herdId == 0)
            {
                herdId = base.GetNextHerdId();
            }

            return herdId;
        }

        public override void OnBlockPlaced(ItemStack byItemStack = null)
        {
            base.OnBlockPlaced(byItemStack);

            // min(1, 1.5 - x/40)
            // http://fooplot.com/#W3sidHlwZSI6MCwiZXEiOiJtaW4oMSwxLjUteC80MCkiLCJjb2xvciI6IiMwMDAwMDAifSx7InR5cGUiOjEwMDAsIndpbmRvdyI6WyIwIiwiMTIwIiwiMCIsIjEuNSJdfV0-
            float corrupLocustNestChance = Math.Min(1, 1.5f - (float)Pos.Y / (0.36f * Api.World.SeaLevel));

            string entityCode = "locust-bronze";
            

            if (Api.World.Rand.NextDouble() < corrupLocustNestChance)
            {
                entityCode = "locust-corrupt";
            }
            
            Data = new BESpawnerData()
            {
                EntityCodes = new string[] { entityCode },
                InGameHourInterval = 0.1f + 0.9f * (float)Api.World.Rand.NextDouble(),
                MaxCount = Api.World.Rand.Next(7) + 3,
                SpawnArea = new Cuboidi(-5, -5, -5, 5, 2, 5),
                GroupSize = 2 + Api.World.Rand.Next(4),
                SpawnOnlyAfterImport = false,
                InitialSpawnQuantity = 4 + Api.World.Rand.Next(7),
                MinPlayerRange = 36
            };
        }

        public void OnBlockBreaking()
        {
            if (Api.Side != EnumAppSide.Client) return;

            if (Api.World.Rand.NextDouble() < 0.3)
            {
                (Api as ICoreClientAPI).Network.SendBlockEntityPacket(Pos.X, Pos.Y, Pos.Z, 123);
            }
        }

        public override void OnReceivedClientPacket(IPlayer fromPlayer, int packetid, byte[] bytes)
        {
            base.OnReceivedClientPacket(fromPlayer, packetid, bytes);

            if (packetid == 123)
            {
                if (Api.World.Rand.NextDouble() < 0.25 && insideLocustCount > 0)
                {
                    ICoreServerAPI sapi = Api as ICoreServerAPI;
                    int rnd = sapi.World.Rand.Next(Data.EntityCodes.Length);
                    EntityProperties type = Api.World.GetEntityType(new AssetLocation(Data.EntityCodes[rnd]));

                    if (type == null) return;

                    Cuboidf collisionBox = new Cuboidf()
                    {
                        X1 = -type.CollisionBoxSize.X / 2,
                        Z1 = -type.CollisionBoxSize.X / 2,
                        X2 = type.CollisionBoxSize.X / 2,
                        Z2 = type.CollisionBoxSize.X / 2,
                        Y2 = type.CollisionBoxSize.Y
                    }.OmniNotDownGrowBy(0.1f);

                    Vec3d spawnPos = new Vec3d();
                    for (int tries = 0; tries < 15; tries++)
                    {
                        spawnPos.Set(Pos).Add(
                            -0.5 + Api.World.Rand.NextDouble(),
                            -1,
                            -0.5 + Api.World.Rand.NextDouble()
                        );

                        if (!collisionTester.IsColliding(Api.World.BlockAccessor, collisionBox, spawnPos, false))
                        {
                            if (herdId == 0) herdId = GetNextHerdId();

                            DoSpawn(type, spawnPos, herdId);
                            break;
                        }
                    }

                    insideLocustCount--;
                }
            }
        }

        protected override void OnGameTick(float dt)
        {
            base.OnGameTick(dt);

            if (Api.World.Rand.NextDouble() < 0.1)
            {
                insideLocustCount = Math.Min(insideLocustCount + 1, 15);
            }
        }

        protected override void DoSpawn(EntityProperties entityType, Vec3d spawnPosition, long herdId)
        {
            

            base.DoSpawn(entityType, spawnPosition, herdId);
        }


        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);

            herdId = tree.GetLong("herdId");
            insideLocustCount = tree.GetInt("insideLocustCount");

            // Maybe an old nest
            if (Data.EntityCodes != null && Data.EntityCodes.Length > 0 && Data.EntityCodes[0] == "locust")
            {
                Data.EntityCodes[0] = "locust-bronze";
            }

            Data.MinPlayerRange = 36;
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            tree.SetLong("herdId", herdId);
            tree.SetInt("insideLocustCount", insideLocustCount);
        }
    }
}