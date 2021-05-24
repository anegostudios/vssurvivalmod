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

namespace Vintagestory.GameContent
{
    
    public class BEBehaviorBurning : BlockEntityBehavior
    {
        public float startDuration;
        public float remainingBurnDuration;
        public BlockFacing fromFacing = BlockFacing.NORTH;

        Block fireBlock;
        Block fuelBlock;

        public Vec3d EffectOffset = new Vec3d();

        string startedByPlayerUid;

        ILoadedSound ambientSound;
        Cuboidf fireCuboid = new Cuboidf(0, 0, 0, 1, 1, 1);
        WeatherSystemBase wsys;
        Vec3d tmpPos = new Vec3d();


        public float TimePassed
        {
            get { return startDuration - remainingBurnDuration; }
        }

        public API.Common.Action<float> OnFireTick;
        public API.Common.ActionBoolReturn ShouldBurn;
        public API.Common.ActionBoolReturn<BlockPos> OnCanBurn;

        public bool IsBurning;

        public BlockPos FirePos;
        public BlockPos FuelPos;
        long l1, l2;


        public BEBehaviorBurning(BlockEntity be) : base(be) {

            OnCanBurn = (pos) =>
            {
                Block block = Api.World.BlockAccessor.GetBlock(pos);
                return block?.CombustibleProps != null && block.CombustibleProps.BurnDuration > 0;
            };
            ShouldBurn = () => true;
            OnFireTick = (dt) =>
            {
                if (remainingBurnDuration <= 0)
                {
                    if (canBurn(FuelPos))
                    {
                        TrySpreadTo(FuelPos, fromFacing);
                    }
                    KillFire();
                }
            };
        }

        public override void Initialize(ICoreAPI api, JsonObject properties)
        {
            base.Initialize(api, properties);

            fireBlock = Api.World.GetBlock(new AssetLocation("fire"));
            if (fireBlock == null) fireBlock = new Block();

            if (IsBurning)
            {
                startBurning();
            }
        }

        public void OnFirePlaced(BlockFacing fromFacing, string startedByPlayerUid)
        {
            if (IsBurning || !ShouldBurn()) return;

            this.fromFacing = fromFacing;
            this.startedByPlayerUid = startedByPlayerUid;

            FirePos = Blockentity.Pos;
            FuelPos = Blockentity.Pos.AddCopy(fromFacing.Opposite);
            fuelBlock = Api.World.BlockAccessor.GetBlock(FuelPos);

            if (!canBurn(FuelPos))
            {
                foreach (BlockFacing facing in BlockFacing.ALLFACES)
                {
                    BlockPos nnpos = FirePos.AddCopy(facing);
                    fuelBlock = Api.World.BlockAccessor.GetBlock(nnpos);
                    if (canBurn(nnpos))
                    {
                        this.fromFacing = facing.Opposite;
                        startDuration = remainingBurnDuration = fuelBlock.CombustibleProps.BurnDuration;
                        return;
                    }
                }

                startDuration = 1;
                remainingBurnDuration = 1;
            }
            else
            {
                if (fuelBlock.CombustibleProps != null)
                {
                    startDuration = remainingBurnDuration = fuelBlock.CombustibleProps.BurnDuration;
                }
            }

            startBurning();
        }



        private void startBurning()
        {
            if (IsBurning) return;

            FirePos = Blockentity.Pos;
            FuelPos = Blockentity.Pos.AddCopy(fromFacing.Opposite);
            fuelBlock = Api.World.BlockAccessor.GetBlock(FuelPos);

            IsBurning = true;

            l1 = Blockentity.RegisterGameTickListener(OnTick, 25);
            if (Api.Side == EnumAppSide.Server)
            {
                l2 = Blockentity.RegisterGameTickListener(OnSlowServerTick, 1000);
            }

            wsys = Api.ModLoader.GetModSystem<WeatherSystemBase>();

            if (ambientSound == null && Api.Side == EnumAppSide.Client)
            {
                ambientSound = ((IClientWorldAccessor)Api.World).LoadSound(new SoundParams()
                {
                    Location = new AssetLocation("sounds/environment/fire.ogg"),
                    ShouldLoop = true,
                    Position = FirePos.ToVec3f().Add(0.5f, 0.25f, 0.5f).Add((float)EffectOffset.X, (float)EffectOffset.Y, (float)EffectOffset.Z),
                    DisposeOnFinish = false,
                    Volume = 1f
                });

                if (ambientSound != null)
                {
                    ambientSound.PlaybackPosition = ambientSound.SoundLengthSeconds * (float)Api.World.Rand.NextDouble();
                    ambientSound.Start();
                }
            }
        }





        private void OnSlowServerTick(float dt)
        {
            if (!canBurn(FuelPos))
            {
                KillFire();
                return;
            }

            Entity[] entities = Api.World.GetEntitiesAround(FirePos.ToVec3d().Add(0.5, 0.5, 0.5), 3, 3, (e) => e.Alive);
            Vec3d ownPos = FirePos.ToVec3d();
            for (int i = 0; i < entities.Length; i++)
            {
                Entity entity = entities[i];
                if (CollisionTester.AabbIntersect(entity.CollisionBox, entity.ServerPos.X, entity.ServerPos.Y, entity.ServerPos.Z, fireCuboid, ownPos))
                {
                    entity.ReceiveDamage(new DamageSource() { Source = EnumDamageSource.Block, SourceBlock = fireBlock, SourcePos = ownPos, Type = EnumDamageType.Fire }, 2f);
                }
            }

            if (Api.World.BlockAccessor.GetRainMapHeightAt(FirePos.X, FirePos.Y) <= FirePos.Y)   // It's more efficient to do this quick check before GetPrecipitation
            {
                // Die on rainfall
                tmpPos.Set(FirePos.X + 0.5, FirePos.Y + 0.5, FirePos.Z + 0.5);
                double rain = wsys.GetPrecipitation(tmpPos);
                if (rain > 0.1)
                {
                    Api.World.PlaySoundAt(new AssetLocation("sounds/effect/extinguish"), FirePos.X + 0.5, FirePos.Y, FirePos.Z + 0.5, null, false, 16);

                    if (rand.NextDouble() < rain / 2)
                    {
                        KillFire();
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

                if (((ICoreServerAPI)Api).Server.Config.AllowFireSpread && spreadChance > Api.World.Rand.NextDouble())
                {
                    TrySpreadFire();
                }
            }

            if (Api.Side == EnumAppSide.Client)
            {
                int index = Math.Min(fireBlock.ParticleProperties.Length - 1, Api.World.Rand.Next(fireBlock.ParticleProperties.Length + 1));
                AdvancedParticleProperties particles = fireBlock.ParticleProperties[index];
                particles.basePos = RandomBlockPos(Api.World.BlockAccessor, FuelPos, fuelBlock, fromFacing).Add(EffectOffset);

                particles.Quantity.avg = 0.75f;
                particles.TerrainCollision = false;
                Api.World.SpawnParticles(particles);
                particles.Quantity.avg = 0;
            }
        }




        private void KillFire()
        {
            IsBurning = false;
            Blockentity.UnregisterGameTickListener(l1);
            Blockentity.UnregisterGameTickListener(l2);
            ambientSound?.FadeOutAndStop(1);

            Api.World.BlockAccessor.SetBlock(0, FirePos);
            Api.World.BlockAccessor.TriggerNeighbourBlockUpdate(FirePos);
        }


        private void TrySpreadFire()
        {
            foreach (BlockFacing facing in BlockFacing.ALLFACES)
            {
                BlockPos npos = FuelPos.AddCopy(facing);

                if (canBurn(npos))
                {
                    if (Api.World.BlockAccessor.GetBlock(npos.AddCopy(fromFacing)).BlockId == 0 && TrySpreadTo(npos.AddCopy(fromFacing), fromFacing))
                    {
                        break;
                    }

                    bool dobreak = false;
                    foreach (BlockFacing firefacing in BlockFacing.ALLFACES)
                    {
                        BlockPos nnpos = npos.AddCopy(firefacing);
                        
                        if (canBurn(nnpos) && TrySpreadTo(nnpos, firefacing))
                        {
                            dobreak = true;
                            break;
                        }
                    }

                    if (dobreak) break;
                }
            }
        }


        public bool TrySpreadTo(BlockPos pos, BlockFacing facing)
        {
            IPlayer player = Api.World.PlayerByUid(startedByPlayerUid);
            
            if (player != null && Api.World.Claims.TestAccess(player, pos, EnumBlockAccessFlags.BuildOrBreak) != EnumWorldAccessResponse.Granted) {
                return false;
            }

            Api.World.BlockAccessor.SetBlock(fireBlock.BlockId, pos);

            BlockEntity befire = Api.World.BlockAccessor.GetBlockEntity(pos);
            befire.GetBehavior<BEBehaviorBurning>()?.OnFirePlaced(facing, startedByPlayerUid);

            return true;
        }


        bool canBurn(BlockPos pos)
        {
            return OnCanBurn(pos) && Api.ModLoader.GetModSystem<ModSystemBlockReinforcement>()?.IsReinforced(pos) != true;
        }

        public override void OnBlockRemoved()
        {
            base.OnBlockRemoved();

            if (ambientSound != null)
            {
                ambientSound?.Stop();
                ambientSound?.Dispose();
                ambientSound = null;
            }
        }

        ~BEBehaviorBurning()
        {
            if (ambientSound != null)
            {
                ambientSound?.Dispose();
            }
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);
            remainingBurnDuration = tree.GetFloat("remainingBurnDuration");
            startDuration = tree.GetFloat("startDuration");
            fromFacing = BlockFacing.ALLFACES[tree.GetInt("fromFacing")];

            bool wasBurning = IsBurning;
            bool nowBurning = tree.GetBool("isBurning", true);

            if (nowBurning && !wasBurning)
            {
                startBurning();
            }

            startedByPlayerUid = tree.GetString("startedByPlayerUid");
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            tree.SetFloat("remainingBurnDuration", remainingBurnDuration);
            tree.SetFloat("startDuration", startDuration);
            tree.SetInt("fromFacing", fromFacing.Index);
            tree.SetBool("isBurning", IsBurning);

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
                    pos.Y + box.Y1 + rand.NextDouble() * box.YSize,
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
                    pos.Y + 0.5f + face.Y / 1.95f + (haveCollisionBox && facing.Axis == EnumAxis.Y ? (face.Y > 0 ? collisionBoxes[0].Y2 - 1 : collisionBoxes[0].Y1) : 0),
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
