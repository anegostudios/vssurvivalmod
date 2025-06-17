﻿using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

#nullable disable

namespace Vintagestory.GameContent
{
    public enum EnumTreeGrowthStage
    {
        Seed,
        Sapling
    }

    public class BlockEntitySapling : BlockEntity
    {
        double totalHoursTillGrowth;
        long growListenerId;
        public EnumTreeGrowthStage stage;
        public bool plantedFromSeed;
        private NormalRandom normalRandom;

        MeshData dirtMoundMesh
        {
            get
            {
                ICoreClientAPI capi = Api as ICoreClientAPI;
                if (capi == null) return null;

                return ObjectCacheUtil.GetOrCreate(Api, "dirtMoundMesh", () =>
                {

                    Shape shape = API.Common.Shape.TryGet(capi, AssetLocation.Create("shapes/block/plant/dirtmound.json", Block.Code.Domain));
                    capi.Tesselator.TesselateShape(Block, shape, out MeshData mesh);

                    return mesh;
                });
            }
        }


        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            if (api is ICoreServerAPI)
            {
                normalRandom = new NormalRandom(api.World.Seed);
                growListenerId = RegisterGameTickListener(CheckGrow, 2000);
            }
        }

        NatFloat nextStageDaysRnd
        {
            get
            {
                if (stage == EnumTreeGrowthStage.Seed)
                {
                    NatFloat sproutDays = NatFloat.create(EnumDistribution.UNIFORM, 1.5f, 0.5f);
                    if (Block?.Attributes != null)
                    {
                        return Block.Attributes["growthDays"].AsObject(sproutDays);
                    }
                    return sproutDays;
                }

                NatFloat matureDays = NatFloat.create(EnumDistribution.UNIFORM, 7f, 2f);
                if (Block?.Attributes != null)
                {
                    return Block.Attributes["matureDays"].AsObject(matureDays);
                }
                return matureDays;
            }
        }

        float GrowthRateMod => Api.World.Config.GetString("saplingGrowthRate").ToFloat(1);

        public override void OnBlockPlaced(ItemStack byItemStack = null)
        {
            stage = byItemStack?.Collectible is ItemTreeSeed ? EnumTreeGrowthStage.Seed : EnumTreeGrowthStage.Sapling;
            plantedFromSeed = stage == EnumTreeGrowthStage.Seed;
            totalHoursTillGrowth = Api.World.Calendar.TotalHours + nextStageDaysRnd.nextFloat(1, Api.World.Rand) * 24 * GrowthRateMod;
        }


        private void CheckGrow(float dt)
        {
            if (Api.World.Calendar.TotalHours < totalHoursTillGrowth) return;

            float temperature = Api.World.BlockAccessor.GetClimateAt(Pos, EnumGetClimateMode.ForSuppliedDate_TemperatureOnly, Api.World.Calendar.TotalDays).Temperature;
            if (temperature < 5)
            {
                return;
            }

            if (stage == EnumTreeGrowthStage.Seed)
            {
                stage = EnumTreeGrowthStage.Sapling;
                totalHoursTillGrowth = Api.World.Calendar.TotalHours + nextStageDaysRnd.nextFloat(1, Api.World.Rand) * 24 * GrowthRateMod;
                MarkDirty(true);
                return;
            }

            const int chunksize = GlobalConstants.ChunkSize;
            foreach (BlockFacing facing in BlockFacing.HORIZONTALS)
            {
                Vec3i dir = facing.Normali;
                int x = Pos.X + dir.X * chunksize;
                int z = Pos.Z + dir.Z * chunksize;

                // Not at world edge and chunk is not loaded? We must be at the edge of loaded chunks. Wait until more chunks are generated
                if (Api.World.BlockAccessor.IsValidPos(x, Pos.InternalY, z) && Api.World.BlockAccessor.GetChunkAtBlockPos(x, Pos.InternalY, z) == null) return;
            }

            Block block = Api.World.BlockAccessor.GetBlock(Pos);
            string treeGenCode = block.Attributes?["treeGen"].AsString(null);

            if (treeGenCode == null)
            {
                UnregisterGameTickListener(growListenerId);
                growListenerId = 0;
                return;
            }

            AssetLocation code = new AssetLocation(treeGenCode);
            ICoreServerAPI sapi = Api as ICoreServerAPI;

            if (!sapi.World.TreeGenerators.TryGetValue(code, out ITreeGenerator gen))
            {
                UnregisterGameTickListener(growListenerId);
                growListenerId = 0;
                return;
            }

            Api.World.BlockAccessor.SetBlock(0, Pos);
            Api.World.BulkBlockAccessor.ReadFromStagedByDefault = true;
            float size = 0.6f + (float)Api.World.Rand.NextDouble() * 0.5f;

            TreeGenParams pa = new TreeGenParams()
            {
                skipForestFloor = true,
                size = size,
                otherBlockChance = 0,
                vinesGrowthChance = 0,
                mossGrowthChance = 0
            };

            gen.GrowTree(Api.World.BulkBlockAccessor, Pos.DownCopy(), pa, normalRandom);

            Api.World.BulkBlockAccessor.Commit();
        }


        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);

            tree.SetDouble("totalHoursTillGrowth", totalHoursTillGrowth);
            tree.SetInt("growthStage", (int)stage);
            tree.SetBool("plantedFromSeed", plantedFromSeed);
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);

            totalHoursTillGrowth = tree.GetDouble("totalHoursTillGrowth", 0);
            stage = (EnumTreeGrowthStage)tree.GetInt("growthStage", 1);
            plantedFromSeed = tree.GetBool("plantedFromSeed");
        }

        public ItemStack[] GetDrops()
        {
            if (stage == EnumTreeGrowthStage.Seed)
            {
                Item item = Api.World.GetItem(AssetLocation.Create("treeseed-" + Block.Variant["wood"], Block.Code.Domain));
                return new ItemStack[] { new ItemStack(item) };
            } else
            {
                return new ItemStack[] { new ItemStack(Block) };
            }
        }


        public string GetBlockName()
        {
            if (stage == EnumTreeGrowthStage.Seed)
            {
                return Lang.Get("treeseed-planted-" + Block.Variant["wood"]);
            } else
            {
                return Block.OnPickBlock(Api.World, Pos).GetName();
            }
        }


        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            base.GetBlockInfo(forPlayer, dsc);

            double hoursleft = totalHoursTillGrowth - Api.World.Calendar.TotalHours;
            double daysleft = hoursleft / Api.World.Calendar.HoursPerDay;

            if (stage == EnumTreeGrowthStage.Seed)
            {
                if (daysleft <= 1)
                {
                    dsc.AppendLine(Lang.Get("Will sprout in less than a day"));
                }
                else
                {
                    dsc.AppendLine(Lang.Get("Will sprout in about {0} days", (int)daysleft));
                }
            }
            else
            {

                if (daysleft <= 1)
                {
                    dsc.AppendLine(Lang.Get("Will mature in less than a day"));
                }
                else
                {
                    dsc.AppendLine(Lang.Get("Will mature in about {0} days", (int)daysleft));
                }
            }
        }


        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tessThreadTesselator)
        {
            if (plantedFromSeed)
            {
                mesher.AddMeshData(dirtMoundMesh);
            }

            if (stage == EnumTreeGrowthStage.Seed)
            {
                return true;
            }

            return base.OnTesselation(mesher, tessThreadTesselator);
        }



    }
}
