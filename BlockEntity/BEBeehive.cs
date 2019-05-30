using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public enum EnumHivePopSize
    {
        Poor = 0,
        Decent = 1,
        Large = 2
    }

    public class BlockEntityBeehive : BlockEntity, IBlockShapeSupplier
    {        
        // Stored values
        int scanIteration;
        int quantityNearbyFlowers;
        int quantityNearbyHives;
        List<BlockPos> emptySkeps = new List<BlockPos>();
        bool isWildHive;
        BlockPos skepToPop;
        double beginPopStartTotalHours;
        float popHiveAfterHours;
        double cooldownUntilTotalHours;
        double harvestableAtTotalHours;
        public bool Harvestable;

        // Current scan values
        int scanQuantityNearbyFlowers;
        int scanQuantityNearbyHives;
        List<BlockPos> scanEmptySkeps = new List<BlockPos>();

        // Temporary values
        EnumHivePopSize hivePopSize;
        bool wasPlaced = false;
        public static SimpleParticleProperties Bees;
        string orientation;

        static BlockEntityBeehive()
        {
            Bees = new SimpleParticleProperties(
                1, 1,
                ColorUtil.ToRgba(255, 215, 156, 65),
                new Vec3d(), new Vec3d(),
                new Vec3f(0, 0, 0),
                new Vec3f(0, 0, 0),
                1f,
                0f,
                0.5f, 0.5f,
                EnumParticleModel.Cube
            );
        }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            
            RegisterGameTickListener(TestHarvestable, 3000);
            RegisterGameTickListener(OnScanForEmptySkep, api.World.Rand.Next(20000) + 120000 / 4);

            if (api.Side == EnumAppSide.Client)
            {
                RegisterGameTickListener(SpawnBeeParticles, 300);
            }

            if (wasPlaced)
            {
                harvestableAtTotalHours = api.World.Calendar.TotalHours + 24/2 * (3 + api.World.Rand.NextDouble() * 8);
            }


            Block block = api.World.BlockAccessor.GetBlock(pos);
            orientation = block.LastCodePart();
            isWildHive = block.FirstCodePart() != "skep";
            if (!isWildHive && api.Side == EnumAppSide.Client)
            {
                ICoreClientAPI capi = api as ICoreClientAPI;
                Block fullSkep = api.World.GetBlock(new AssetLocation("skep-populated-east"));

                MeshData mesh;
                capi.Tesselator.TesselateShape(
                    fullSkep, 
                    api.Assets.TryGet("shapes/block/beehive/skep-harvestable.json").ToObject<Shape>(), 
                    out mesh, 
                    new Vec3f(0, BlockFacing.FromCode(orientation).HorizontalAngleIndex * 90 - 90, 0)
                );
                api.ObjectCache["beehive-harvestablemesh-" + orientation] = mesh;
            }
            
        }

        Vec3d startPos = new Vec3d();
        Vec3d endPos = new Vec3d();
        Vec3f minVelo = new Vec3f();
        Vec3f maxVelo = new Vec3f();
        private void SpawnBeeParticles(float dt)
        {
            if (api.World.Rand.NextDouble() > 2 * api.World.Calendar.DayLightStrength - 0.5) return;

            Random rand = api.World.Rand;
            
            // Leave hive
            if (api.World.Rand.NextDouble() > 0.5)
            {    
                startPos.Set(pos.X + 0.5f, pos.Y + 0.5f, pos.Z + 0.5f);
                minVelo.Set((float)rand.NextDouble() * 3 - 1.5f, (float)rand.NextDouble() * 1 - 0.5f, (float)rand.NextDouble() * 3 - 1.5f);

                Bees.minPos = startPos;
                Bees.minVelocity = minVelo;
                Bees.lifeLength = 1f;
                Bees.WithTerrainCollision = false;
            }

            // Go back to hive
            else
            {
                startPos.Set(pos.X + rand.NextDouble() * 5 - 2.5, pos.Y + rand.NextDouble() * 2 - 1f, pos.Z + rand.NextDouble() * 5 - 2.5f);
                endPos.Set(pos.X + 0.5f, pos.Y + 0.5f, pos.Z + 0.5f);

                minVelo.Set((float)(endPos.X - startPos.X), (float)(endPos.Y - startPos.Y), (float)(endPos.Z - startPos.Z));
                minVelo /= 2;

                Bees.minPos = startPos;
                Bees.minVelocity = minVelo;
                Bees.WithTerrainCollision = true;
            }

            api.World.SpawnParticles(Bees);
        }

        public override void OnBlockPlaced(ItemStack byItemStack = null)
        {
            wasPlaced = true;
            if (api?.World != null)
            {
                harvestableAtTotalHours = api.World.Calendar.TotalHours + 24 / 2 * (3 + api.World.Rand.NextDouble() * 8);
            }
        }

        

        private void TestHarvestable(float dt)
        {
            if (!Harvestable && !isWildHive && api.World.Calendar.TotalHours > harvestableAtTotalHours && hivePopSize > EnumHivePopSize.Poor)
            {
                Harvestable = true;
                MarkDirty(true);
            }
        }

        private void OnScanForEmptySkep(float dt)
        {
            if (api.Side == EnumAppSide.Client) return;
            if (api.World.Calendar.TotalHours < cooldownUntilTotalHours) return;

            if (scanIteration == 0)
            {
                scanQuantityNearbyFlowers = 0;
                scanQuantityNearbyHives = 0;
                scanEmptySkeps.Clear();
            }

            // Let's count/collect 3 things in a 20x20x20 cube
            // 1. All positions of empty skeps
            // 2. Amount of living beehives (skeps or wild)
            // 3. Amount of flowers

            // Default Spread speed: Once every 4 in game days * factor
            // Don't spread at all if 3 * livinghives + 3 > flowers

            // factor = Clamped(livinghives / Math.Sqrt(flowers - 3 * livinghives - 3), 1, 1000)
            // After spreading: 4 extra days cooldown

            int minX = -8 + 8 * (scanIteration / 2);
            int minZ = -8 + 8 * (scanIteration % 2);
            int size = 8;

            Block emptySkepN = api.World.GetBlock(new AssetLocation("skep-empty-north"));
            Block emptySkepE = api.World.GetBlock(new AssetLocation("skep-empty-east"));
            Block emptySkepS = api.World.GetBlock(new AssetLocation("skep-empty-south"));
            Block emptySkepW = api.World.GetBlock(new AssetLocation("skep-empty-west"));

            Block fullSkepN = api.World.GetBlock(new AssetLocation("skep-populated-north"));
            Block fullSkepE = api.World.GetBlock(new AssetLocation("skep-populated-east"));
            Block fullSkepS = api.World.GetBlock(new AssetLocation("skep-populated-south"));
            Block fullSkepW = api.World.GetBlock(new AssetLocation("skep-populated-west"));


            Block wildhive1 = api.World.GetBlock(new AssetLocation("wildbeehive-medium"));
            Block wildhive2 = api.World.GetBlock(new AssetLocation("wildbeehive-large"));
            

            api.World.BlockAccessor.WalkBlocks(pos.AddCopy(minX, -5, minZ), pos.AddCopy(minX + size - 1, 5, minZ + size - 1), (block, pos) =>
            {
                if (block.Id == 0) return;

                if (block.Attributes?["beeFeed"].AsBool() == true) scanQuantityNearbyFlowers++;

                if (block == emptySkepN || block == emptySkepE || block == emptySkepS || block == emptySkepW)
                {
                    scanEmptySkeps.Add(pos.Copy());
                }
                if (block == fullSkepN || block == fullSkepE || block == fullSkepS || block == fullSkepW || block == wildhive1 || block == wildhive2)
                {
                    scanQuantityNearbyHives++;
                }
            });

            scanIteration++;
            
            if (scanIteration == 4)
            {
                scanIteration = 0;
                OnScanComplete();
            }
        }

        private void OnScanComplete()
        {
            quantityNearbyFlowers = scanQuantityNearbyFlowers;
            quantityNearbyHives = scanQuantityNearbyHives;
            emptySkeps = new List<BlockPos>(scanEmptySkeps);

            if (emptySkeps.Count == 0)
            {
                skepToPop = null;
            }

            hivePopSize = (EnumHivePopSize)GameMath.Clamp(quantityNearbyFlowers - 3 * quantityNearbyHives, 0, 2);

            MarkDirty();


            if (3 * quantityNearbyHives + 3 > quantityNearbyFlowers)
            {
                skepToPop = null;
                MarkDirty(false);
                return;
            }

            if (skepToPop != null && api.World.Calendar.TotalHours > beginPopStartTotalHours + popHiveAfterHours)
            {
                TryPopCurrentSkep();
                cooldownUntilTotalHours = api.World.Calendar.TotalHours + 4 / 2 * 24;
                MarkDirty(false);
                return;
            }

            // Default Spread speed: Once every 4 in game days * factor
            // Don't spread at all if 3 * livinghives + 3 > flowers

            // factor = Clamped(livinghives / Math.Sqrt(flowers - 3 * livinghives - 3), 1, 1000)
            // After spreading: 4 extra days cooldown
            if (skepToPop != null)
            {
                float newPopHours = 4 * 24 / 2 * GameMath.Clamp(quantityNearbyHives / GameMath.Sqrt(quantityNearbyFlowers - 3 * quantityNearbyHives - 3), 1, 1000);
                this.popHiveAfterHours = (float)(0.75 * popHiveAfterHours + 0.25 * newPopHours);

                if (!emptySkeps.Contains(skepToPop))
                {
                    skepToPop = null;
                    MarkDirty(false);
                    return;
                }

            } else
            {
                popHiveAfterHours = 4 * 24 / 2 * GameMath.Clamp(quantityNearbyHives / GameMath.Sqrt(quantityNearbyFlowers - 3 * quantityNearbyHives - 3), 1, 1000);

                beginPopStartTotalHours = api.World.Calendar.TotalHours;

                float mindistance = 999;
                BlockPos closestPos = new BlockPos();
                foreach (BlockPos pos in emptySkeps)
                {
                    float dist = pos.DistanceTo(this.pos);
                    if (dist < mindistance)
                    {
                        mindistance = dist;
                        closestPos = pos;
                    }
                }

                skepToPop = closestPos;
            }
        }


        private void TryPopCurrentSkep()
        {
            Block skepToPopBlock = api.World.BlockAccessor.GetBlock(skepToPop);
            if (skepToPopBlock == null || !(skepToPopBlock is BlockSkep))
            {
                // Skep must have changed since last time we checked, so lets restart 
                this.skepToPop = null;
                return;
            }

            string orient = skepToPopBlock.LastCodePart();

            string blockcode = "skep-populated-" + orient;
            Block fullSkep = api.World.GetBlock(new AssetLocation(blockcode));

            if (fullSkep == null)
            {
                api.World.Logger.Warning("BEBeehive.TryPopSkep() - block with code {0} does not exist?", blockcode);
                return;
            }

            api.World.BlockAccessor.SetBlock(fullSkep.BlockId, skepToPop);
            hivePopSize = EnumHivePopSize.Poor;
            this.skepToPop = null;
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);

            
            tree.SetInt("scanIteration", scanIteration);

            tree.SetInt("quantityNearbyFlowers", quantityNearbyFlowers);
            tree.SetInt("quantityNearbyHives", quantityNearbyHives);
            TreeAttribute emptyskepTree = new TreeAttribute();
            for (int i = 0; i < emptySkeps.Count; i++)
            {
                emptyskepTree.SetInt("posX-" + i, emptySkeps[i].X);
                emptyskepTree.SetInt("posY-" + i, emptySkeps[i].Y);
                emptyskepTree.SetInt("posZ-" + i, emptySkeps[i].Z);
            }
            tree["emptyskeps"] = emptyskepTree;



            tree.SetInt("scanQuantityNearbyFlowers", scanQuantityNearbyFlowers);
            tree.SetInt("scanQuantityNearbyHives", scanQuantityNearbyHives);
            TreeAttribute scanEmptyskepTree = new TreeAttribute();
            for (int i = 0; i < scanEmptySkeps.Count; i++)
            {
                scanEmptyskepTree.SetInt("posX-" + i, scanEmptySkeps[i].X);
                scanEmptyskepTree.SetInt("posY-" + i, scanEmptySkeps[i].Y);
                scanEmptyskepTree.SetInt("posZ-" + i, scanEmptySkeps[i].Z);
            }
            tree["scanEmptySkeps"] = scanEmptyskepTree;


            tree.SetInt("isWildHive", isWildHive ? 1 : 0);
            tree.SetInt("harvestable", Harvestable ? 1 : 0);
            tree.SetInt("skepToPopX", skepToPop == null ? 0 : skepToPop.X);
            tree.SetInt("skepToPopY", skepToPop == null ? 0 : skepToPop.Y);
            tree.SetInt("skepToPopZ", skepToPop == null ? 0 : skepToPop.Z);
            tree.SetDouble("beginPopStartTotalHours", beginPopStartTotalHours);
            tree.SetFloat("popHiveAfterHours", popHiveAfterHours);
            tree.SetDouble("cooldownUntilTotalHours", cooldownUntilTotalHours);
            tree.SetDouble("harvestableAtTotalHours", harvestableAtTotalHours);
            tree.SetInt("hiveHealth", (int)hivePopSize);
        }

        public override void FromTreeAtributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAtributes(tree, worldForResolving);

            bool wasHarvestable = Harvestable;

            scanIteration = tree.GetInt("scanIteration");

            quantityNearbyFlowers = tree.GetInt("quantityNearbyFlowers");
            quantityNearbyHives = tree.GetInt("quantityNearbyHives");
            emptySkeps.Clear();
            TreeAttribute emptySkepTree = tree["emptyskeps"] as TreeAttribute;
            for (int i = 0; i < emptySkepTree.Count/3; i++)
            {
                emptySkeps.Add(new BlockPos(
                    emptySkepTree.GetInt("posX-" + i),
                    emptySkepTree.GetInt("posY-" + i),
                    emptySkepTree.GetInt("posZ-" + i)
                ));
            }


            scanQuantityNearbyFlowers = tree.GetInt("scanQuantityNearbyFlowers");
            scanQuantityNearbyHives = tree.GetInt("scanQuantityNearbyHives");
            scanEmptySkeps.Clear();
            TreeAttribute scanEmptySkepTree = tree["scanEmptySkeps"] as TreeAttribute;
            for (int i = 0; scanEmptySkepTree != null && i < scanEmptySkepTree.Count / 3; i++)
            {
                scanEmptySkeps.Add(new BlockPos(
                    scanEmptySkepTree.GetInt("posX-" + i),
                    scanEmptySkepTree.GetInt("posY-" + i),
                    scanEmptySkepTree.GetInt("posZ-" + i)
                ));
            }


            isWildHive = tree.GetInt("isWildHive") > 0;
            Harvestable = tree.GetInt("harvestable") > 0;
            int x = tree.GetInt("skepToPopX");
            int y = tree.GetInt("skepToPopY");
            int z = tree.GetInt("skepToPopZ");
            if (x != 0 || y != 0 || z != 0)
            {
                skepToPop = new BlockPos(x, y, z);
            } else skepToPop = null;

            beginPopStartTotalHours = tree.GetDouble("beginPopStartTotalHours");
            popHiveAfterHours = tree.GetFloat("popHiveAfterHours");
            cooldownUntilTotalHours = tree.GetDouble("cooldownUntilTotalHours");
            harvestableAtTotalHours = tree.GetDouble("harvestableAtTotalHours");
            hivePopSize = (EnumHivePopSize)tree.GetInt("hiveHealth");

            if (Harvestable != wasHarvestable && api != null)
            {
                MarkDirty(true);
            }
        }


        public bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tesselator)
        {
            if (Harvestable)
            {
                mesher.AddMeshData(api.ObjectCache["beehive-harvestablemesh-" + orientation] as MeshData);
                return true;
            }

            return false;
        }


        public override string GetBlockInfo(IPlayer forPlayer)
        {
            if (api.World.EntityDebugMode && forPlayer.WorldData.CurrentGameMode == EnumGameMode.Creative)
            {
                return 
                    Lang.Get("Nearby flowers: {0}, Nearby Hives: {1}, Empty Hives: {2}, Pop after hours: {3}. harvest in {4}, repop cooldown: {5}", quantityNearbyFlowers, quantityNearbyHives, emptySkeps.Count, (beginPopStartTotalHours + popHiveAfterHours - api.World.Calendar.TotalHours).ToString("#.##"), (harvestableAtTotalHours - api.World.Calendar.TotalHours).ToString("#.##"), (cooldownUntilTotalHours - api.World.Calendar.TotalHours).ToString("#.##"))
                    + "\n" + Lang.Get("Population Size: " + hivePopSize);
            }

            string str = Lang.Get("Nearby flowers: {0}\nPopulation Size: {1}", quantityNearbyFlowers, hivePopSize);
            if (Harvestable) str += "\n" + Lang.Get("Harvestable");

            if (skepToPop != null && api.World.Calendar.TotalHours > cooldownUntilTotalHours)
            {
                double inhours = beginPopStartTotalHours + popHiveAfterHours - api.World.Calendar.TotalHours;
                double days = inhours / api.World.Calendar.HoursPerDay;

                if (days > 1.5)
                {
                    str += "\n" + Lang.Get("Will swarm in approx. {0} days", Math.Round(days));
                } else if (days > 0.5)
                {
                    str += "\n" + Lang.Get("Will swarm in approx. one day");
                } else
                {
                    str += "\n" + Lang.Get("Will swarm in less than a day");
                }
            }

            return str;
        }
    }
}
