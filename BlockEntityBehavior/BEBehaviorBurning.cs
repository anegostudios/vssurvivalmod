using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

#nullable disable

namespace Vintagestory.GameContent
{

    public class BEBehaviorBurning : BlockEntityBehavior
    {
        public float startDuration;
        public float remainingBurnDuration;

        Block fireBlock;
        Block fuelBlock;
        string startedByPlayerUid;

        static Cuboidf fireCuboid = new Cuboidf(-0.125f, 0, -0.125f, 1.125f, 1, 1.125f);
        WeatherSystemBase wsys;
        Vec3d tmpPos = new Vec3d();

        ICoreClientAPI capi;

        public bool AllowFireSpread;
        
        public float TimePassed
        {
            get { return startDuration - remainingBurnDuration; }
        }

        public Action<float> OnFireTick;
        public Action<bool> OnFireDeath;
        public ActionBoolReturn ShouldBurn;
        public ActionBoolReturn<BlockPos> OnCanBurn;


        public bool IsBurning;

        public BlockPos FirePos;
        public BlockPos FuelPos;
        long l1, l2;


        public BEBehaviorBurning(BlockEntity be) : base(be) {

            OnCanBurn = (pos) =>
            {
                return getBurnDuration(pos) > 0;
            };
            ShouldBurn = () => true;
            OnFireTick = (dt) =>
            {
                if (remainingBurnDuration <= 0)
                {
                    KillFire(true);
                }
            };

            OnFireDeath = (consumefuel) =>
            {
                if (consumefuel)
                {
                    var becontainer = Api.World.BlockAccessor.GetBlockEntity(FuelPos) as BlockEntityContainer;
                    becontainer?.OnBlockBroken();

                    Api.World.BlockAccessor.SetBlock(0, FuelPos);
                    Api.World.BlockAccessor.TriggerNeighbourBlockUpdate(FuelPos);
                    if (AllowFireSpread)
                    {
                        TrySpreadTo(FuelPos);
                    }
                }

                Api.World.BlockAccessor.SetBlock(0, FirePos);

                Api.World.BlockAccessor.TriggerNeighbourBlockUpdate(FirePos);
            };
        }

        float getBurnDuration(BlockPos pos)
        {
            Block block = Api.World.BlockAccessor.GetBlock(pos);
            if (block.CombustibleProps != null) return block.CombustibleProps.BurnDuration;

            if (block.GetInterface<ICombustible>(Api.World, pos) is ICombustible bic)
            {
                return bic.GetBurnDuration(Api.World, pos);
            }

            return 0;
        }

        public override void Initialize(ICoreAPI api, JsonObject properties)
        {
            base.Initialize(api, properties);

            fireBlock = Api.World.GetBlock(new AssetLocation("fire"));
            if (fireBlock == null) fireBlock = new Block();

            capi = api as ICoreClientAPI;

            if (IsBurning)
            {
                initSoundsAndTicking();
            }

            AllowFireSpread = Api.World.Config.GetBool("allowFireSpread");
        }

        public void OnFirePlaced(BlockFacing fromFacing, string startedByPlayerUid)
        {
            OnFirePlaced(Blockentity.Pos, Blockentity.Pos.AddCopy(fromFacing.Opposite), startedByPlayerUid);
        }

        public void OnFirePlaced(BlockPos firePos, BlockPos fuelPos, string startedByPlayerUid, bool didSpread = false)
        {
            if (IsBurning || !ShouldBurn()) return;

            this.startedByPlayerUid = startedByPlayerUid;

            if (!string.IsNullOrEmpty(startedByPlayerUid))
            {
                var playerByUid = Api.World.PlayerByUid(startedByPlayerUid);
                if (playerByUid != null)
                {
                    if (didSpread)
                    {
                        Api.Logger.Audit($"{playerByUid.PlayerName} started a fire that spread to {firePos}");   
                    }
                    else
                    {
                        Api.Logger.Audit($"{playerByUid.PlayerName} started a fire at {firePos}");
                    }
                }
            }
            

            FirePos = firePos.Copy();
            FuelPos = fuelPos.Copy();

            if (FuelPos == null || !canBurn(FuelPos))
            {
                foreach (BlockFacing facing in BlockFacing.ALLFACES)
                {
                    BlockPos npos = FirePos.AddCopy(facing);
                    if (canBurn(npos))
                    {
                        FuelPos = npos;
                        startDuration = remainingBurnDuration = getBurnDuration(npos);
                        startBurning();
                        return;
                    }
                }

                startDuration = 1;
                remainingBurnDuration = 1;
                FuelPos = FirePos.Copy(); // No fuel left
            }
            else
            {
                float bdur = getBurnDuration(fuelPos);
                if (bdur > 0)
                {
                    startDuration = remainingBurnDuration = bdur;
                }
            }

            startBurning();
        }



        private void startBurning()
        {
            if (IsBurning) return;
            IsBurning = true;
            unloaded = false;
            if (Api != null) initSoundsAndTicking();
        }

        BlockFacing particleFacing;
         
        private void initSoundsAndTicking()
        {
            fuelBlock = Api.World.BlockAccessor.GetBlock(FuelPos);

            l1 = Blockentity.RegisterGameTickListener(OnTick, 25);
            if (Api.Side == EnumAppSide.Server)
            {
                l2 = Blockentity.RegisterGameTickListener(OnSlowServerTick, 1000);
            }

            wsys = Api.ModLoader.GetModSystem<WeatherSystemBase>();

            // To get the ambient sound engine to update quicker
            Api.World.BlockAccessor.MarkBlockDirty(Pos);

            particleFacing = BlockFacing.FromNormal(new Vec3i(FirePos.X - FuelPos.X, FirePos.Y - FuelPos.Y, FirePos.Z - FuelPos.Z));

            if (capi != null)
            {
                capi.Event.RegisterAsyncParticleSpawner(onAsyncParticles);
            }
        }


        bool unloaded = false;
        public override void OnBlockUnloaded()
        {
            base.OnBlockUnloaded();
            unloaded = true;
        }

        public override void OnBlockRemoved()
        {
            base.OnBlockRemoved();
            unloaded = true;
        }


        private void OnSlowServerTick(float dt)
        {
            if (!canBurn(FuelPos))
            {
                KillFire(false);
                return;
            }

            Entity[] entities = Api.World.GetEntitiesAround(FirePos.ToVec3d().Add(0.5, 0.5, 0.5), 3, 3, (e) => true);
            Vec3d ownPos = FirePos.ToVec3d();
            for (int i = 0; i < entities.Length; i++)
            {
                Entity entity = entities[i];
                if (!CollisionTester.AabbIntersect(entity.SelectionBox, entity.ServerPos.X, entity.ServerPos.Y, entity.ServerPos.Z, fireCuboid, ownPos)) continue;

                if (entity.Alive)
                {
                    entity.ReceiveDamage(new DamageSource() { Source = EnumDamageSource.Block, SourceBlock = fireBlock, SourcePos = ownPos, Type = EnumDamageType.Fire }, 2f);
                }

                if (Api.World.Rand.NextDouble() < 0.125)
                {
                    entity.Ignite();
                }
            }

            var bl = Api.World.BlockAccessor;
            if (FuelPos != FirePos && (bl.GetBlock(FirePos, BlockLayersAccess.Fluid).Attributes?.IsTrue("smothersFire") == true || bl.GetBlock(FuelPos, BlockLayersAccess.Fluid).Attributes?.IsTrue("smothersFire") == true))
            {
                KillFire(false);
                return;
            }

            if (bl.GetRainMapHeightAt(FirePos.X, FirePos.Z) <= FirePos.Y)   // It's more efficient to do this quick check before GetPrecipitation
            {
                // Die on rainfall
                tmpPos.Set(FirePos.X + 0.5, FirePos.Y + 0.5, FirePos.Z + 0.5);
                double rain = wsys.GetPrecipitation(tmpPos);
                if (rain > 0.05)
                {
                    if (rand.NextDouble() < rain / 2)
                    {
                        Api.World.PlaySoundAt(new AssetLocation("sounds/effect/extinguish"), FirePos, -0.25, null, false, 16);
                    }

                    if (rand.NextDouble() < rain / 2)
                    {
                        KillFire(false);
                        return;
                    }
                }
            }
        }


        private void OnTick(float dt)
        {
            if (Api.Side == EnumAppSide.Server)
            {
                remainingBurnDuration -= dt;

                OnFireTick?.Invoke(dt);

                float spreadChance = (TimePassed - 2.5f) / 450f;

                if (AllowFireSpread && spreadChance > Api.World.Rand.NextDouble())
                {
                    TrySpreadFireAllDirs();
                }
            }
        }


        private bool onAsyncParticles(float dt, IAsyncParticleManager manager)
        {
            if (fuelBlock == null) return true; // Not yet loaded

            int index = Math.Min(fireBlock.ParticleProperties.Length - 1, Api.World.Rand.Next(fireBlock.ParticleProperties.Length + 1));
            AdvancedParticleProperties particles = fireBlock.ParticleProperties[index];

            particles.basePos = RandomBlockPos(Api.World.BlockAccessor, FuelPos, fuelBlock, particleFacing);
            particles.Quantity.avg = index == 1 ? 4 : 0.75f;
            particles.TerrainCollision = false;
            manager.Spawn(particles);
            particles.Quantity.avg = 0;

            return !unloaded && IsBurning;
        }




        public void KillFire(bool consumeFuel)
        {
            IsBurning = false;
            Blockentity.UnregisterGameTickListener(l1);
            Blockentity.UnregisterGameTickListener(l2);
            l1 = 0;
            l2 = 0;
            OnFireDeath(consumeFuel);
            unloaded = true;
        }


        protected void TrySpreadFireAllDirs()
        {
            foreach (BlockFacing facing in BlockFacing.ALLFACES)
            {
                BlockPos npos = FirePos.AddCopy(facing);
                TrySpreadTo(npos);
            }

            if (FuelPos != FirePos)
            {
                TrySpreadTo(FirePos);
            }
        }


        public bool TrySpreadTo(BlockPos pos)
        {
            // 1. Replaceable test
            BlockEntity be = Api.World.BlockAccessor.GetBlockEntity(pos);
            var block = Api.World.BlockAccessor.GetBlock(pos);
            if (block.Replaceable < 6000 && be is not BlockEntityGroundStorage) return false;

            var bhbu = be?.GetBehavior<BEBehaviorBurning>();
            if (bhbu?.IsBurning == true) return false;

            // 2. fuel test
            bool hasFuel = false;
            BlockPos npos = null;
            foreach (BlockFacing firefacing in BlockFacing.ALLFACES)
            {
                npos = pos.AddCopy(firefacing);
                if (canBurn(npos) && Api.World.BlockAccessor.GetBlockEntity(npos)?.GetBehavior<BEBehaviorBurning>() == null) {
                    hasFuel = true;
                    break;
                }
            }

            var begs = be as BlockEntityGroundStorage;
            if (hasFuel == false && begs?.IsBurning == false && begs.CanIgnite)
            {
                hasFuel = true;
            }

            if (!hasFuel) return false;

            // 3. Land claim test
            IPlayer player = Api.World.PlayerByUid(startedByPlayerUid);
            if (player != null && (Api.World.Claims.TestAccess(player, pos, EnumBlockAccessFlags.BuildOrBreak) != EnumWorldAccessResponse.Granted || Api.World.Claims.TestAccess(player, npos, EnumBlockAccessFlags.BuildOrBreak) != EnumWorldAccessResponse.Granted)) {
                return false;
            }

            SpreadTo(pos, npos, begs);
            return true;
        }

        private void SpreadTo(BlockPos pos, BlockPos npos, BlockEntityGroundStorage begs)
        {
            //Api.World.Logger.Debug(string.Format("Fire @{0}: Spread to {1}.", FirePos, pos));
            if (begs == null)
            {
                Api.World.BlockAccessor.SetBlock(fireBlock.BlockId, pos);
            }
            var befire = Api.World.BlockAccessor.GetBlockEntity(pos);
            var buhu = befire?.GetBehavior<BEBehaviorBurning>();

            if (begs?.IsBurning == false && buhu?.IsBurning == false)
            {
                begs.TryIgnite();
            }
            else
            {
                buhu?.OnFirePlaced(pos, npos, startedByPlayerUid, true);
            }
        }


        protected bool canBurn(BlockPos pos)
        {
            return
                OnCanBurn(pos)
                && Api.ModLoader.GetModSystem<ModSystemBlockReinforcement>()?.IsReinforced(pos) != true
            ;
        }
        
        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);
            remainingBurnDuration = tree.GetFloat("remainingBurnDuration");
            startDuration = tree.GetFloat("startDuration");

            // pre v1.15-pre.3 fire
            if (!tree.HasAttribute("fireposX"))
            {
                BlockFacing fromFacing = BlockFacing.ALLFACES[tree.GetInt("fromFacing", 0)];
                FirePos = Blockentity.Pos.Copy();
                FuelPos = FirePos.AddCopy(fromFacing);
            }
            else
            {
                FirePos = new BlockPos(tree.GetInt("fireposX"), tree.GetInt("fireposY"), tree.GetInt("fireposZ"));
                FuelPos = new BlockPos(tree.GetInt("fuelposX"), tree.GetInt("fuelposY"), tree.GetInt("fuelposZ"));
            }

            bool wasBurning = IsBurning;
            bool nowBurning = tree.GetBool("isBurning", false);

            if (nowBurning && !wasBurning)
            {
                startBurning();
            }
            if (!nowBurning && wasBurning)
            {
                KillFire(remainingBurnDuration <= 0);
                IsBurning = nowBurning;
            }

            startedByPlayerUid = tree.GetString("startedByPlayerUid");
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            tree.SetFloat("remainingBurnDuration", remainingBurnDuration);
            tree.SetFloat("startDuration", startDuration);
            tree.SetBool("isBurning", IsBurning);

            tree.SetInt("fireposX", FirePos.X); tree.SetInt("fireposY", FirePos.Y); tree.SetInt("fireposZ", FirePos.Z);
            tree.SetInt("fuelposX", FuelPos.X); tree.SetInt("fuelposY", FuelPos.Y); tree.SetInt("fuelposZ", FuelPos.Z);

            if (startedByPlayerUid != null)
            {
                tree.SetString("startedByPlayerUid", startedByPlayerUid);
            }
        }


        static Random rand = new Random();
        public static Vec3d RandomBlockPos(IBlockAccessor blockAccess, BlockPos pos, Block block, BlockFacing facing = null)
        {
            if (facing == null)
            {
                Cuboidf[] selectionBoxes = block.GetSelectionBoxes(blockAccess, pos);
                Cuboidf box = (selectionBoxes != null && selectionBoxes.Length > 0) ? selectionBoxes[0] : Block.DefaultCollisionBox;

                return new Vec3d(
                    pos.X + box.X1 + rand.NextDouble() * box.XSize,
                    pos.InternalY + box.Y1 + rand.NextDouble() * box.YSize,
                    pos.Z + box.Z1 + rand.NextDouble() * box.ZSize
                );
            }
            else
            {
                Vec3i face = facing.Normali;

                Cuboidf[] collisionBoxes = block.GetCollisionBoxes(blockAccess, pos);

                bool haveCollisionBox = collisionBoxes != null && collisionBoxes.Length > 0;

                Vec3d basepos = new Vec3d(
                    pos.X + 0.5f + face.X / 1.95f + (haveCollisionBox && facing.Axis == EnumAxis.X ? (face.X > 0 ? collisionBoxes[0].X2 - 1 : collisionBoxes[0].X1) : 0),
                    pos.InternalY + 0.5f + face.Y / 1.95f + (haveCollisionBox && facing.Axis == EnumAxis.Y ? (face.Y > 0 ? collisionBoxes[0].Y2 - 1 : collisionBoxes[0].Y1) : 0),
                    pos.Z + 0.5f + face.Z / 1.95f + (haveCollisionBox && facing.Axis == EnumAxis.Z ? (face.Z > 0 ? collisionBoxes[0].Z2 - 1 : collisionBoxes[0].Z1) : 0)
                );

                Vec3d posVariance = new Vec3d(
                    1f * (1 - face.X),
                    1f * (1 - face.Y),
                    1f * (1 - face.Z)
                );

                return new Vec3d(
                    basepos.X + (rand.NextDouble() - 0.5) * posVariance.X,
                    basepos.Y + (rand.NextDouble() - 0.5) * posVariance.Y,
                    basepos.Z + (rand.NextDouble() - 0.5) * posVariance.Z
                );
            }
        }

    }
}
