using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

#nullable disable

namespace Vintagestory.GameContent.Mechanics
{
    public class RegionTurbulenceSources
    {
        public Dictionary<BlockPos, float> RadiiByPosition = new Dictionary<BlockPos, float>();
    }

    public class ModSystemWindTurbulence : ModSystem
    {
        Dictionary<long, RegionTurbulenceSources> windmillsByRegion = new();
        public override bool ShouldLoad(EnumAppSide forSide) => forSide == EnumAppSide.Server;
        int regionSize;

        public override void StartServerSide(ICoreServerAPI api)
        {
            base.StartServerSide(api);
            regionSize = api.World.BlockAccessor.RegionSize;
        }

        public void SetTurbulence(BlockPos pos, float radius)
        {
            RegionTurbulenceSources sources = getorCreateSources(pos);
            sources.RadiiByPosition[pos.Copy()] = radius;
        }
        public void RemoveTurbulence(BlockPos pos)
        {
            RegionTurbulenceSources sources = getSources(pos);
            if (sources == null) return;
            sources.RadiiByPosition.Remove(pos);
        }


        /// <summary>
        /// Returns maxvalue if no source found
        /// </summary>
        /// <param name="pos"></param>
        /// <returns></returns>
        public float GetNearestTurbulenceDistance(BlockPos pos)
        {
            float mindistsq = float.MaxValue;
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dz = -1; dz <= 1; dz++)
                {
                    float distsq = getSourceMinDist(pos, getSources(pos.AddCopy(dx * regionSize, 0, dz * regionSize)));
                    if (distsq < mindistsq) mindistsq = distsq;
                }
            }

            return mindistsq;
        }


        private static float getSourceMinDist(BlockPos pos, RegionTurbulenceSources src)
        {
            if (src == null) return float.MaxValue;
            float minDist = float.MaxValue;

            foreach (var val in src.RadiiByPosition)
            {
                if (val.Key == pos) continue; // Don't check self

                float dist = Math.Max(0, pos.DistanceTo(val.Key) - val.Value);
                if (dist < minDist)
                {
                    minDist = dist;
                }
            }

            return minDist;
        }

        private RegionTurbulenceSources getorCreateSources(BlockPos pos)
        {
            long key = (((long)pos.Z / regionSize) << 31) + (pos.X / regionSize);
            RegionTurbulenceSources sources;
            if (!windmillsByRegion.TryGetValue(key, out sources))
            {
                sources = windmillsByRegion[key] = new RegionTurbulenceSources();
            }

            return sources;
        }

        private RegionTurbulenceSources getSources(BlockPos pos)
        {
            long key = (((long)pos.Z / regionSize) << 31) + (pos.X / regionSize);
            RegionTurbulenceSources sources;
            if (!windmillsByRegion.TryGetValue(key, out sources))
            {
                return null;
            }
            return sources;
        }

    }

    public class BEBehaviorWindmillRotor : BEBehaviorMPRotor
    {
        protected WeatherSystemBase weatherSystem;
        protected double windSpeed;
        protected int sailLength = 0;
        public int SailLength => sailLength;

        private AssetLocation sound;
        protected override AssetLocation Sound => sound;
        protected override float GetSoundVolume() => (0.5f + 0.5f * (float)windSpeed * (turbulenceExposed ? 0.5f : 1f)) * sailLength / 3f;
        protected override float Resistance => 0.003f;
        protected override double AccelerationFactor => 0.05d;
        protected override float TargetSpeed => (float)Math.Min(0.6f, windSpeed);
        protected override float TorqueFactor => sailLength / 4f * powerMul * (turbulenceExposed ? 0.5f : 1f);    // Should stay at /4f (5 sails are supposed to have "125% power output")

        protected bool turbulenceExposed;

        protected virtual float SelfTurbulenceRadius => sailLength * 1.5f;

        protected float powerMul = 1f;

        public BEBehaviorWindmillRotor(BlockEntity blockentity) : base(blockentity) { }

        public override void Initialize(ICoreAPI api, JsonObject properties)
        {
            base.Initialize(api, properties);

            powerMul = properties["powerMul"].AsFloat(1);
            this.sound = new AssetLocation("sounds/effect/swoosh");
            weatherSystem = Api.ModLoader.GetModSystem<WeatherSystemBase>();
            Blockentity.RegisterGameTickListener(CheckWindSpeed, 1000);

            if (api.Side == EnumAppSide.Server)
            {
                api.ModLoader.GetModSystem<ModSystemWindTurbulence>().SetTurbulence(Pos, SelfTurbulenceRadius);
                if (SailLength > 0) turbulenceExposed = api.ModLoader.GetModSystem<ModSystemWindTurbulence>().GetNearestTurbulenceDistance(Pos) < SelfTurbulenceRadius;
            }
        }

        protected void CheckWindSpeed(float dt)
        {
            windSpeed = weatherSystem.WeatherDataSlowAccess.GetWindSpeed(Blockentity.Pos.ToVec3d());
            if (Api.World.BlockAccessor.GetLightLevel(Blockentity.Pos, EnumLightLevelType.OnlySunLight) < 5 && Api.World.Config.GetString("undergroundWindmills", "false") != "true") windSpeed = 0;


            if (Api.Side == EnumAppSide.Server && sailLength > 0 && Api.World.Rand.NextDouble() < 0.1)
            {
                bool prevTurb = turbulenceExposed;
                var dist = Api.ModLoader.GetModSystem<ModSystemWindTurbulence>().GetNearestTurbulenceDistance(Pos);
                turbulenceExposed = dist < SelfTurbulenceRadius;
                if (prevTurb != turbulenceExposed) Blockentity.MarkDirty(true);

                // Todo: Entity Fling and Damage thing
                /*Cuboidd cuboid = new Cuboidd(-sailLength + 0.1, -sailLength + 0.1, 0.2, sailLength - 0.2, sailLength - 0.2, 0.8);
                float rot = (ownFacing.HorizontalAngleIndex+1) * 90;

                cuboid = cuboid.RotatedCopy(0, rot, 0, new Vec3d(0.5, 0.5, 0.5));
                cuboid = cuboid.OffsetCopy(Position.X, Position.Y, Position.Z);

                Api.World.SpawnParticles(100, ColorUtil.WhiteArgb, cuboid.Start, cuboid.End, new Vec3f(), new Vec3f(), 2f, 0);

                Vec3d centerPos = Position.ToVec3d().Add(0.5, 0.5, 0.5);

                partitionUtil.WalkEntityPartitions(centerPos, SailLength + 1, (e) =>
                {
                    if (!e.IsInteractable) return;
                    if (cuboid.IntersectsOrTouches(e.CollisionBox, e.Pos.X, e.Pos.Y, e.Pos.Z))
                    {
                        e.ReceiveDamage(new DamageSource() { SourceBlock = Block, SourcePos = Position.ToVec3d().Add(0.5, 0.5, 0.5), Type = EnumDamageType.BluntAttack, Source = EnumDamageSource.Block }, 0.5f);

                        float dx = (float)(centerPos.X - e.Pos.X);
                        float dy = (float)(centerPos.Y - e.Pos.Y);
                        float dz = (float)(centerPos.Y - e.Pos.Z);

                        e.Pos.Motion.Add(dx, dy, dz);
                    }
                });*/

                if (obstructed(sailLength + 1))
                {
                    Api.World.PlaySoundAt(new AssetLocation("sounds/effect/toolbreak"), Position, 0, null, false, 20, 1f);
                    while (sailLength-- > 0)
                    {
                        ItemStack stacks = new ItemStack(Api.World.GetItem(new AssetLocation("sail")), 4);
                        Api.World.SpawnItemEntity(stacks, Blockentity.Pos);
                    }
                    sailLength = 0;
                    Blockentity.MarkDirty(true);
                    this.network.updateNetwork(manager.getTickNumber());
                }
            }
        }

        public override void OnBlockBroken(IPlayer byPlayer = null)
        {
            while (sailLength-- > 0)
            {
                Api.World.SpawnItemEntity(SailStack, Blockentity.Pos);
            }

            base.OnBlockBroken(byPlayer);
        }

        ItemStack SailStack
        {
            get
            {
                var jstack = Block.Attributes["sailStack"].AsObject<JsonItemStack>();
                jstack.Resolve(Api.World, Block.Code + " sail stack");
                return jstack.ResolvedItemstack;
            }
        }

        public bool OnInteract(IPlayer byPlayer)
        {
            if (sailLength >= Block.Attributes["sailedShapes"]["maxLength"].AsInt(0)) return false;

            ItemSlot slot = byPlayer.InventoryManager.ActiveHotbarSlot;
            if (slot.Empty || slot.StackSize < 4) return false;

            var sailStack = SailStack;

            if (!slot.Itemstack.Equals(Api.World, sailStack, GlobalConstants.IgnoredStackAttributes)) return false;

            if (obstructed(sailLength + 2))
            {
                if (Api.Side == EnumAppSide.Client)
                {
                    (Api as ICoreClientAPI).TriggerIngameError(this, "notenoughspace", Lang.Get("Cannot add more sails. Make sure there's space for the sails to rotate freely"));
                }

                return false;
            }

            if (byPlayer.WorldData.CurrentGameMode != EnumGameMode.Creative)
            {
                slot.TakeOut(sailStack.StackSize);
                slot.MarkDirty();
            }

            sailLength++;
            updateShape(Api.World);

            if (Api.Side == EnumAppSide.Server)
            {
                Api.ModLoader.GetModSystem<ModSystemWindTurbulence>().SetTurbulence(Pos, SelfTurbulenceRadius);
            }

            Blockentity.MarkDirty(true);
            return true;
        }

        protected bool obstructed(int len)
        {
            BlockPos tmpPos = new BlockPos(Position.dimension);

            for (int dxz = -len; dxz <= len; dxz++)
            {
                for (int dy = -len; dy <= len; dy++)
                {
                    if (dxz == 0 && dy == 0) continue;
                    if (len > 1 && Math.Abs(dxz) == len && Math.Abs(dy) == len) continue;

                    int dx = ownFacing.Axis == EnumAxis.Z ? dxz : 0;
                    int dz = ownFacing.Axis == EnumAxis.X ? dxz : 0;
                    tmpPos.Set(Position.X + dx, Position.Y + dy, Position.Z + dz);

                    Block block = Api.World.BlockAccessor.GetBlock(tmpPos);
                    Cuboidf[] collBoxes = block.GetCollisionBoxes(Api.World.BlockAccessor, tmpPos);
                    if (collBoxes != null && collBoxes.Length > 0 && !(block is BlockSnowLayer) && !(block is BlockSnow))
                    {

                        return true;
                    }
                }
            }

            return false;
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            sailLength = tree.GetInt("sailLength");
            turbulenceExposed = tree.GetBool("turbulenceExposed");

            base.FromTreeAttributes(tree, worldAccessForResolve);
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            tree.SetInt("sailLength", sailLength);
            tree.SetBool("turbulenceExposed", turbulenceExposed);
            base.ToTreeAttributes(tree);
        }

        public override void OnBlockRemoved()
        {
            base.OnBlockRemoved();
            if (Api.Side == EnumAppSide.Server)
            {
                Api.ModLoader.GetModSystem<ModSystemWindTurbulence>().RemoveTurbulence(Pos);
            }
        }

        public override void OnBlockUnloaded()
        {
            base.OnBlockUnloaded();
            if (Api.Side == EnumAppSide.Server)
            {
                Api.ModLoader.GetModSystem<ModSystemWindTurbulence>().RemoveTurbulence(Pos);
            }
        }

        protected override void updateShape(IWorldAccessor worldForResolve)
        {
            if (worldForResolve.Side != EnumAppSide.Client || Block == null)
            {
                return;
            }

            if (sailLength == 0)
            {
                Shape = new CompositeShape()
                {
                    Base = Block.Shape.Base,
                    rotateY = Block.Shape.rotateY
                };
            }
            else
            {
                var al = AssetLocation.Create(Block.Attributes["sailedShapes"]["wildcardPath"].AsString(), Block.Code.Domain);
                al.Path = al.Path.Replace("{length}", "" + sailLength);

                Shape = new CompositeShape() { Base = al, rotateY = Block.Shape.rotateY };
            }
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder sb)
        {
            base.GetBlockInfo(forPlayer, sb);

            sb.AppendLine(string.Format(Lang.Get("Wind speed: {0}%", (int)(100*windSpeed))));
            sb.AppendLine(Lang.Get("Sails power output: {0} kN", (int)(sailLength / 5f * 100f * powerMul * (turbulenceExposed ? 0.5f : 1f))));

            if (turbulenceExposed) sb.AppendLine("Exposed to turbulence from nearby windmill, power output reduced by 50%.");
        }
    }
}
