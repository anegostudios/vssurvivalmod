using System;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.GameContent.Mechanics
{

    
    public class BEBehaviorMPWaterWheel : BEBehaviorMPRotor
    {
        protected float flowRate;
        protected BlockFacing facing;
        protected float dir;
        protected BEBehaviorRightClickConstructable bebconstructable;
        protected Vec3f flowVec = new Vec3f();
        protected bool blocked = false;
        protected int diameter = 3;

        protected int suitablePowerSourceBlockCount = 0;
        protected float requiresMinFlowSpeed = 1.5f; // A modder will most certainly want to modify waterwheel rapids requirement, so lets add that already

        private AssetLocation sound;
        protected override AssetLocation Sound => sound;
        protected override float GetSoundVolume() => 0.5f + 0.5f * flowRate;
        protected override float Resistance => blocked ? 1 : 0.003f;
        protected override double AccelerationFactor => 0.05d;
        protected override float TargetSpeed => (float)Math.Min(0.3f, flowRate);
        protected override float TorqueFactor => flowRate;

        public override float AngleRad => base.AngleRad * dir;
               

        public BEBehaviorMPWaterWheel(BlockEntity blockentity) : base(blockentity) { }

        public override void Initialize(ICoreAPI api, JsonObject properties)
        {
            bebconstructable = Blockentity.GetBehavior<BEBehaviorRightClickConstructable>();
            base.Initialize(api, properties);

            this.sound = new AssetLocation("sounds/block/waterwheel-wood");
            Blockentity.RegisterGameTickListener(CheckWater, 1000);
            facing = BlockFacing.FromCode(Block.Variant["side"]).Opposite;
            diameter = properties["diameter"].AsInt(3);

            requiresMinFlowSpeed = properties["requiresMinFlowSpeed"].AsFloat(1.5f);

            bebconstructable.OnShapeChanged += (shape) => Shape = shape;
        }

        protected override CompositeShape GetShape()
        {
            return bebconstructable?.shape;
        }

        public override MechPowerPath[] GetMechPowerExits(MechPowerPath entryDir)
        {
            return new MechPowerPath[] {
                entryDir,
                new MechPowerPath(entryDir.OutFacing.Opposite, entryDir.gearingRatio, Position, !entryDir.invert)
            };
        }


        protected void CheckWater(float dt)
        {
            if (!bebconstructable.IsComplete)
            {
                flowRate = 0;
                return;
            }

            suitablePowerSourceBlockCount = 0;

            flowVec.Set(0, 0, 0);
            var fcw = facing.GetCW();
            blocked = false;
            int radius = diameter / 2;
            Vec3f pushVector;
            for (int a = -radius; a <= radius; a++)
            {
                int offset = radius;
                var pos = new BlockPos(Pos.X + a * fcw.Normali.X, Pos.Y - offset, Pos.Z + a * fcw.Normali.Z);

                // Below
                var block = Api.World.BlockAccessor.GetBlock(pos);
                
                if (blockedBy(block, pos, BlockFacing.UP)) return;
                if (getFlowSpeed(block) > requiresMinFlowSpeed)
                {
                    pushVector = block.Attributes?["pushvector"].AsObject<Vec3f>(null);
                    if (pushVector != null)
                    {
                        flowVec.Add(pushVector);
                        suitablePowerSourceBlockCount++;
                    }
                }
                // Above
                pos.Y = Pos.Y + offset;
                block = Api.World.BlockAccessor.GetBlock(pos);
                if (blockedBy(block, pos, BlockFacing.DOWN)) return;
                if (getFlowSpeed(block) > requiresMinFlowSpeed)
                {
                    pushVector = block.Attributes?["pushvector"].AsObject<Vec3f>(null);
                    if (pushVector != null)
                    {
                        flowVec.Add(pushVector.Mul(-1));
                        suitablePowerSourceBlockCount++;
                    }
                }
                // Left
                pos.Set(Pos.X + fcw.Normali.X * offset, Pos.Y + a, Pos.Z + fcw.Normali.Z * offset);
                block = Api.World.BlockAccessor.GetBlock(pos);
                if (blockedBy(block, pos, facing)) return;
                if (getFlowSpeed(block) > requiresMinFlowSpeed)
                {
                    pushVector = block.Attributes?["pushvector"].AsObject<Vec3f>(null);
                    if (pushVector != null)
                    {
                        flowVec.Add(pushVector.Mul(-1));
                        suitablePowerSourceBlockCount++;
                    }
                }
                // Right
                pos.Set(Pos.X - fcw.Normali.X * offset, Pos.Y + a, Pos.Z - fcw.Normali.Z * offset);
                block = Api.World.BlockAccessor.GetBlock(pos);
                if (blockedBy(block, pos, facing.Opposite)) return;
                if (getFlowSpeed(block) > requiresMinFlowSpeed)
                {
                    pushVector = block.Attributes?["pushvector"].AsObject<Vec3f>(null);
                    if (pushVector != null)
                    {
                        flowVec.Add(pushVector);
                        suitablePowerSourceBlockCount++;
                    }
                }
            }

            /// North: Negative Z
            /// East: Positive X
            /// South: Positive Z
            /// West: Negative X
            /// Up: Positive Y
            /// Down: Negative Y
            float f = 0;
            if (fcw.Axis == EnumAxis.X)
            {
                f = flowVec.X + flowVec.Y;
                if (facing == BlockFacing.NORTH) f *= -1;
            }
            if (fcw.Axis == EnumAxis.Z)
            {
                f = flowVec.Z + flowVec.Y;
                if (facing == BlockFacing.EAST) f *= -1;
            }

            flowRate = Math.Abs(f * 750);

            if (flowVec.Length() > 0)
            {
                float prevDir = dir;
                dir = Math.Sign(f);
                if (dir != prevDir)
                {
                    this.SetPropagationDirection(new MechPowerPath(dir < 0 ? facing.Opposite : facing, 1, Pos, false));
                }
            }
        }

        private float getFlowSpeed(Block block)
        {
            return block.Attributes?["flowspeed"].AsFloat(0) ?? 0;
        }

        protected bool blockedBy(Block block, BlockPos pos, BlockFacing face)
        {
            if (block.SideIsSolid(pos, face.Index))
            {
                blocked = true;
                flowRate = 0;
                return true;
            }

            return false;
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder sb)
        {
            base.GetBlockInfo(forPlayer, sb);

            if (blocked) sb.AppendLine(Lang.Get("Wheel is blocked, make sure no the entire wheel is free from solid blocks."));
            else sb.AppendLine(Lang.Get("Suitable power source blocks nearby: {0}", suitablePowerSourceBlockCount));

            if (Api.World.EntityDebugMode)
            {
                sb.AppendLine("<font color='#ccc'>flow vector= " + flowVec + "</font>");
                sb.AppendLine("<font color='#ccc'>torque= " + TorqueFactor + "</font>");
                sb.AppendLine("<font color='#ccc'>targetspeed= " + TargetSpeed + "</font>");
                sb.AppendLine("<font color='#ccc'>flowrate = " + flowRate + "</font>");
            }
        }
    }
}
