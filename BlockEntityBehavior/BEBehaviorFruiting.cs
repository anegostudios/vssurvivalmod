using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.GameContent
{
    public class BEBehaviorFruiting : BlockEntityBehavior
    {
        // These fields are read from behavior properties

        int positionsCount = 0;
        int maxFruit = 5;
        int fruitStages = 6;
        float maxGerminationDays = 6;
        float transitionDays = 1;
        float successfulGrowthChance = 0.75f;
        string[] fruitCodeBases;
        int ripeStage;
        AssetLocation dropCode;

        // The fruit positions are dynamically generated from the mature plant model, client-side only

        double[] points;

        // These fields are dynamically populated for each separate blockEntity

        private FruitingSystem manager;
        public Vec4f LightRgba { get; internal set; }
        protected Vec3d[] positions;
        protected FruitData[] fruitPoints;
        private double dateLastChecked;

        // Static rotation matrices copied from JsonTesselator, so that our fruit positions can adapt to the same random rotations as the tesselated plant shape

        public static float[] randomRotations = new float[] { -22.5f, 22.5f, 90 - 22.5f, 90 + 22.5f, 180 - 22.5f, 180 + 22.5f, 270 - 22.5f, 270 + 22.5f };
        public static float[][] randomRotMatrices;

        static BEBehaviorFruiting()
        {
            randomRotMatrices = new float[randomRotations.Length][];
            for (int i = 0; i < randomRotations.Length; i++)
            {
                float[] matrix = Mat4f.Create();
                Mat4f.Translate(matrix, matrix, 0.5f, 0.5f, 0.5f);
                Mat4f.RotateY(matrix, matrix, randomRotations[i] * GameMath.DEG2RAD);
                Mat4f.Translate(matrix, matrix, -0.5f, -0.5f, -0.5f);

                randomRotMatrices[i] = matrix;
            }
        }


        public BEBehaviorFruiting(BlockEntity be) : base(be)
        {
        }


        public override void Initialize(ICoreAPI api, JsonObject properties)
        {
            base.Initialize(api, properties);
            dateLastChecked = Api.World.Calendar.TotalDays;

            // Read the properties

            fruitCodeBases = properties["fruitCodeBases"].AsArray<string>(Array.Empty<string>());
            if (fruitCodeBases.Length == 0) return;

            positionsCount = properties["positions"].AsInt(0);
            if (positionsCount <= 0) return;

            string maturePlant = properties["maturePlant"].AsString(null);
            if (maturePlant == null) return;
            Block mature = api.World.GetBlock(new AssetLocation(maturePlant));
            if (!(mature is BlockFruiting matureCrop)) return;
            if (Api.Side == EnumAppSide.Client)
            {
                points = matureCrop.GetFruitingPoints();
            }

            maxFruit = properties["maxFruit"].AsInt(5);
            fruitStages = properties["fruitStages"].AsInt(6);
            maxGerminationDays = properties["maxGerminationDays"].AsFloat(6);
            transitionDays = properties["transitionDays"].AsFloat(1);
            successfulGrowthChance = properties["successfulGrowthChance"].AsFloat(0.75f);
            ripeStage = properties["ripeStage"].AsInt(fruitStages - 1);
            dropCode = new AssetLocation(properties["dropCode"].AsString(null));


            // Set up the manager - used for the instanced renderer

            manager = Api.ModLoader.GetModSystem<FruitingSystem>();


            // If initialising client-side after reading from tree attributes, send fruits to the renderer

            bool addToManager = false;
            if (Api.Side == EnumAppSide.Client && fruitPoints != null)
            {
                LightRgba = Api.World.BlockAccessor.GetLightRGBs(Blockentity.Pos);
                addToManager = true;
            }

            InitializeArrays();

            if (addToManager)
            {
                for (int i = 0; i < positionsCount; i++)
                {
                    FruitData val = fruitPoints[i];
                    if (val.variant >= fruitCodeBases.Length) val.variant %= fruitCodeBases.Length;
                    if (val.variant >= 0 && val.currentStage > 0)
                    {
                        val.SetRandomRotation(Api.World, i, positions[i], this.Blockentity.Pos);
                        manager.AddFruit(new AssetLocation(fruitCodeBases[val.variant] + val.currentStage), positions[i], val);
                    }
                }
            }

            // Tick the blockEntity server-side only

            if (Api.Side == EnumAppSide.Server) Blockentity.RegisterGameTickListener(CheckForGrowth, 2250);
        }


        private void CheckForGrowth(float dt)
        {
            double timeFactor = GameMath.Clamp(Api.World.Calendar.SpeedOfTime / 60, 0.1, 5);
            double now = Api.World.Calendar.TotalDays;
            bool fastForwarded = now > dateLastChecked + 0.5;
            dateLastChecked = now;
            if (Api.World.Rand.NextDouble() > 0.2 * timeFactor && !fastForwarded) return;

            int count = 0;
            bool dirty = false;
            foreach (var val in fruitPoints)
            {
                if (val.variant >= 0)
                {
                    if (val.transitionDate == 0)
                    {
                        val.transitionDate = GetGerminationDate();  // probably this code path is never reached, but just in case something went wrong reading a BlockEntity from a save etc.
                        dirty = true;
                    }

                    if (val.currentStage > 0) count++;
                }
            }

            bool finalStagePlant = false;
            if (this.Blockentity.Block is BlockCrop crop)
            {
                finalStagePlant = (crop.CurrentCropStage == crop.CropProps.GrowthStages);
            }
            foreach (var val in fruitPoints)
            {
                if (val.variant >= 0)
                {
                    if (now > val.transitionDate)
                    {
                        if (val.currentStage == 0 && count >= maxFruit) continue;     // Suppress growth for now - too many fruits already on this plant - but try again soon

                        if (finalStagePlant && val.currentStage < fruitStages - 3) continue;    // No small green fruit growth on final stage crop, except ripe fruit can become overripe

                        if (++val.currentStage > fruitStages)
                        {
                            // reached final stage
                            val.transitionDate = Double.MaxValue;
                            val.currentStage = fruitStages;
                        }
                        else
                        {
                            // set up next transition - take 2.5 times as long for the final stage
                            val.transitionDate = now + transitionDays * (1 + Api.World.Rand.NextDouble()) / 1.5 / PlantHealth() * (val.currentStage == fruitStages - 1 ? 2.5 : 1);
                        }
                        dirty = true;
                    }
                }
            }

            if (dirty) Blockentity.MarkDirty();
        }


        public void InitializeArrays()
        {
            // If initialising on fresh creation, both arrays will be null  (otherwise fruitPoints was already initialised in FromTreeAttributes)

            if (fruitPoints == null)
            {
                // This code path is called on block placement (i.e. when previous stage crop grows to final stage)
                // This initialises each fruit point with a different germination time
                // (if called after FromTreeAttributes, fruitPoints will already have been populated, and GetGerminationDate() will be called in the next CheckForGrowth() call instead)

                fruitPoints = new FruitData[positionsCount];
                int randomSelector = Math.Abs(this.Blockentity.Pos.GetHashCode()) % fruitCodeBases.Length;

                for (int i = 0; i < positionsCount; i++)
                {
                    int fruitVariant = i;
                    if (i >= fruitCodeBases.Length) fruitVariant = randomSelector++ % fruitCodeBases.Length;
                    fruitPoints[i] = new FruitData(fruitVariant, GetGerminationDate(), this, null);
                }
            }

            positions = new Vec3d[positionsCount];

            Vec3f temp = new Vec3f();
            float[] matrix = null;
            if (Blockentity.Block.RandomizeRotations)
            {
                // For performance, only call the hash function once per blockEntity loaded
                int randomSelector = GameMath.MurmurHash3(-Blockentity.Pos.X, Blockentity.Block.RandomizeAxes == EnumRandomizeAxes.XYZ ? Blockentity.Pos.Y : 0, Blockentity.Pos.Z);
                matrix = randomRotMatrices[GameMath.Mod(randomSelector, randomRotations.Length)];
            }

            for (int i = 0; i < positionsCount; i++)
            {
                if (Api.Side == EnumAppSide.Client)
                {
                    positions[i] = new Vec3d(points[i * 3], points[i * 3 + 1], points[i * 3 + 2]);
                    if (matrix != null)
                    {
                        Mat4f.MulWithVec3_Position(matrix, (float)positions[i].X, (float)positions[i].Y, (float)positions[i].Z, temp);
                        positions[i].X = temp.X;
                        positions[i].Y = temp.Y;
                        positions[i].Z = temp.Z;
                    }
                }
                else
                {
                    positions[i] = new Vec3d((i + 1) / positionsCount, (i + 1) / positionsCount, (i + 1) / positionsCount);  //dummy vector for serverside - exact positions don't matter because they won't be rendered
                }

                positions[i].Add(Blockentity.Pos);
            }
        }


        private double GetGerminationDate()
        {
            // Only a percentage of the potential growth points will be able to grow fruits successfully
            double plantGrowthSlowdown = 1.0 / PlantHealth();
            double fruitSpawnReduction = (plantGrowthSlowdown + 0.25) / 1.25;  //Nerf fruit count if plant is unhealthy, e.g. 40% less fruit if plant growth rate is 50%; up to 10% bonus if healthy
            double randomNo = Api.World.Rand.NextDouble() / successfulGrowthChance * fruitSpawnReduction;

            return randomNo > 1 ? Double.MaxValue : Api.World.Calendar.TotalDays + randomNo * maxGerminationDays;
        }


        private double PlantHealth()
        {
            BlockPos posBelow = Blockentity.Pos.DownCopy();
            if (Blockentity.Api.World.BlockAccessor.GetBlockEntity(posBelow) is BlockEntityFarmland bef)
            {
                return bef.GetGrowthRate();
            }

            return 1.0;
        }


        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tesselator)
        {
            base.OnTesselation(mesher, tesselator);

            LightRgba = Api.World.BlockAccessor.GetLightRGBs(Blockentity.Pos);

            return false;

        }


        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);

            if (positionsCount == 0) positionsCount = tree.GetInt("count");
            if (positionsCount == 0) positionsCount = 10;
            if (fruitPoints == null) fruitPoints = new FruitData[positionsCount];

            for (int i = 0; i < positionsCount; i++)
            {
                double td = tree.GetDouble("td" + i);
                int var = tree.GetInt("var" + i);
                int tc = tree.GetInt("tc" + i);

                FruitData val = fruitPoints[i];
                if (val == null)
                {
                    val = new FruitData(-1, td, this, null);
                    fruitPoints[i] = val;
                }
                if (Api is ICoreClientAPI && val.variant >= 0)  //there was an existing FruitData at this position
                {
                    manager.RemoveFruit(fruitCodeBases[val.variant] + val.currentStage, positions[i]);
                }
                val.variant = var;
                val.currentStage = tc;
                val.transitionDate = td;

                if (Api is ICoreClientAPI && val.variant >= 0 && val.currentStage > 0)
                {
                    val.SetRandomRotation(Api.World, i, positions[i], this.Blockentity.Pos);
                    manager.AddFruit(new AssetLocation(fruitCodeBases[val.variant] + val.currentStage), positions[i], val);
                }
            }


        }


        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);

            tree.SetInt("count", positionsCount);
            for (int i = 0; i < positionsCount; i++)
            {
                FruitData val = fruitPoints[i];
                if (val == null) continue;
                tree.SetDouble("td" + i, val.transitionDate);
                tree.SetInt("var" + i, val.variant);
                tree.SetInt("tc" + i, val.currentStage);
            }
        }


        public override void OnBlockUnloaded()
        {
            base.OnBlockUnloaded();

            if (Api is ICoreClientAPI)
            {
                RemoveRenderedFruits();
            }
        }


        public override void OnBlockRemoved()
        {
            base.OnBlockRemoved();

            if (Api is ICoreClientAPI)
            {
                RemoveRenderedFruits();
            }

            int dropCount = 0;
            for (int i = 0; i < fruitPoints.Length; i++)
            {
                FruitData val = fruitPoints[i];
                if (val.variant < 0 || val.currentStage == 0) continue;
                Item item = Api.World.GetItem(new AssetLocation(fruitCodeBases[val.variant] + val.currentStage));
                if (item == null) continue;
                if (item.Attributes != null && item.Attributes["onGround"].AsBool(false)) continue;  // ignore fruits already on the ground

                if (val.currentStage == this.ripeStage)
                {
                    dropCount++;
                }
                else if (Math.Abs(val.currentStage - this.ripeStage) == 1 && Api.World.Rand.NextDouble() > 0.5) dropCount++;
            }

            if (dropCount > 0)
            {
                ItemStack stack = new ItemStack(Api.World.GetItem(dropCode), dropCount);
                Api.World.SpawnItemEntity(stack, Blockentity.Pos.ToVec3d().Add(0.5, 0.25, 0.5));
            }
        }


        public virtual void RemoveRenderedFruits()
        {
            if (positions == null || fruitCodeBases == null) return;
            for (int i = 0; i < fruitPoints.Length; i++)
            {
                FruitData val = fruitPoints[i];
                if (val.variant >= 0 && val.currentStage > 0) manager.RemoveFruit(fruitCodeBases[val.variant] + val.currentStage, positions[i]);
            }
        }


        public virtual bool OnPlayerInteract(float secondsUsed, IPlayer player, Vec3d hit)
        {
            if (player?.InventoryManager?.ActiveTool != EnumTool.Knife) return false;

            if (Api.Side == EnumAppSide.Server) return true;

            bool hasPickableFruit = false;
            for (int i = 0; i < fruitPoints.Length; i++)
            {
                FruitData val = fruitPoints[i];
                if (val.variant >= 0 && val.currentStage >= this.ripeStage)
                {
                    Item item = Api.World.GetItem(new AssetLocation(fruitCodeBases[val.variant] + val.currentStage));
                    if (item == null) continue;
                    if (item.Attributes != null && item.Attributes["onGround"].AsBool(false)) continue;  // ignore fruits already on the ground

                    hasPickableFruit = true;
                    break;
                }
           }

            return hasPickableFruit && secondsUsed < 0.3f;
        }


        public virtual void OnPlayerInteractStop(float secondsUsed, IPlayer player, Vec3d hit)
        {
            if (secondsUsed < 0.2f) return;

            for (int i = 0; i < fruitPoints.Length; i++)
            {
                FruitData val = fruitPoints[i];
                if (val.variant >= 0 && val.currentStage >= this.ripeStage)
                {
                    Item item = Api.World.GetItem(new AssetLocation(fruitCodeBases[val.variant] + val.currentStage));
                    if (item == null) continue;
                    if (item.Attributes != null && item.Attributes["onGround"].AsBool(false)) continue;  // ignore fruits already on the ground

                    if (Api.Side == EnumAppSide.Client) manager.RemoveFruit(fruitCodeBases[val.variant] + val.currentStage, positions[i]);
                    val.variant = -1;   // Flag this fruit as picked
                    val.transitionDate = Double.MaxValue;

                    if (val.currentStage >= this.ripeStage)  // allow both ripe and overripe fruits to be picked
                    {
                        // Play snickety sound
                        double posx = Blockentity.Pos.X + hit.X;
                        double posy = Blockentity.Pos.Y + hit.Y;
                        double posz = Blockentity.Pos.Z + hit.Z;
                        player.Entity.World.PlaySoundAt(new AssetLocation("sounds/effect/squish1"), posx, posy, posz, player, 1.1f + (float)Api.World.Rand.NextDouble() * 0.4f, 16, 0.25f);

                        // Update transition time of any non-started fruit, otherwise they may all start immediately
                        double now = Api.World.Calendar.TotalDays;
                        for (int j = 0; j < fruitPoints.Length; j++)
                        {
                            val = fruitPoints[j];
                            if (val.variant >= 0 && val.currentStage == 0 && val.transitionDate < now)
                            {
                                val.transitionDate = now + Api.World.Rand.NextDouble() * maxGerminationDays / 2;
                            }
                        }

                        // Give the player one item
                        ItemStack stack = new ItemStack(Api.World.GetItem(dropCode), 1);

                        if (!player.InventoryManager.TryGiveItemstack(stack))
                        {
                            Api.World.SpawnItemEntity(stack, Blockentity.Pos.ToVec3d().Add(0.5, 0.25, 0.5));
                        }
                        Api.World.Logger.Audit("{0} Took 1x{1} from {2} at {3}.",
                            player.PlayerName,
                            stack.Collectible.Code,
                            Blockentity.Block.Code,
                            Blockentity.Pos
                        );
                    }
                    Blockentity.MarkDirty();
                    break;
                }
            }
        }
    }



    public class FruitData
    {
        /// <summary>
        /// The date (w.r.t. Calendar.TotalDays) when this fruit will transition to the next stage
        /// </summary>
        public double transitionDate;
        /// <summary>
        /// 0 for the nub (no fruit grown yet), or the current stage e.g. 1-5
        /// </summary>
        public int currentStage;
        /// <summary>
        /// This BEBehavior - used by the renderer to get the current lightRGBA at this position
        /// </summary>
        public BEBehaviorFruiting behavior;
        /// <summary>
        /// The random rotation of this individual fruit
        /// </summary>
        public Vec3f rotation;
        /// <summary>
        /// The variant of the currently rendered fruit item (on-tree shape).  -1 indicates a fruit which has already been picked or dropped
        /// </summary>
        public int variant;

        public FruitData(int variant, double totalDays, BEBehaviorFruiting be, Vec3f rot)
        {
            this.variant = variant;
            this.transitionDate = totalDays;
            this.behavior = be;
            this.rotation = rot;
        }

        internal void SetRandomRotation(IWorldAccessor world, int index, Vec3d vec3d, BlockPos pos)
        {
            if (rotation == null)
            {
                double dx = vec3d.X - pos.X - 0.5;
                double dz = vec3d.Z - pos.Z - 0.5;
                double angle = (Math.Atan2(dx, dz) + GameMath.PI) % GameMath.TWOPI;
                angle += (float)(world.Rand.NextDouble() * GameMath.TWOPI - GameMath.PI) / 70;
                if (angle < 0) angle += GameMath.TWOPI;

                //TODO: This random function will produce a different XZ random rotation each chunk reload
                //      It would be better to generate a pseudo-random hash from the block position and the fruit index
                rotation = new Vec3f((float)(world.Rand.NextDouble() * GameMath.TWOPI - GameMath.PI) / 50, (float) angle, (float)(world.Rand.NextDouble() * GameMath.TWOPI - GameMath.PI) / 50);
            }
        }
    }


}
