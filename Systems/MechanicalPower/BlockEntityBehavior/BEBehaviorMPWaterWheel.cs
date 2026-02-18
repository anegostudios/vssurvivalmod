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


        // Checked both server side and client side: client side only to produce suitablePowerSourceBlockCount for the Block Info HUD
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
            int wheelX = fcw.Normali.X;
            int wheelZ = fcw.Normali.Z;
            FastVec3f axialVec = new FastVec3f(facing.Normalf);
            blocked = false;
            int radius = diameter / 2;
            if (radius < 1) return;

            float moment = 0f;  // The moment is the total rotational force - which becomes torque - being applied to the wheel by any surrounding water
            BlockPos pos = new BlockPos(Pos.dimension);
            FastVec3i firstWaterPos = new FastVec3i(-1, -1, -1);
            FastVec3i lastWaterPos = new FastVec3i(-1, -1, -1);
            Block firstsWaterBlock = null;
            Block lastWaterBlock = null;

            int numberOfPoints = 4 + radius * 4; // 8 points for radius 1, 12 points for radius 2, 16 for radius 3, 20 for radius 4, 24 for radius 5 - these all work, up to radius 5, producing a nice circle/octagon of water check points symmetrical around the hub. Not investigated above radius 5 but it should in principle always be approximately correct
            for (int i = 0; i < numberOfPoints; i++)
            {
                // Calculate dX, dY for a ring of points around the circumference of the wheel
                int dX = (int)(GameMath.Sin(GameMath.TWOPI * i / numberOfPoints) * (radius + 0.5f));
                int dY = (int)(GameMath.Cos(GameMath.TWOPI * i / numberOfPoints) * (radius + 0.5f));

                // dX -> correct dX, dZ for wheel orientation
                int dZ = dX * wheelZ;
                dX *= wheelX;

                pos.Set(Pos.X + dX, Pos.Y + dY, Pos.Z + dZ);
                var block = Api.World.BlockAccessor.GetBlock(pos, BlockLayersAccess.MostSolid);
                // Figure out which side of the water wheel we are approachin this block from - we will test whether it blocks only on the closest face to the wheel. Stretch goal: it could be the lateral faces as well, or test whether the block blocks waterflow?
                BlockFacing blockSide = BlockFacing.UP;
                if (Math.Abs(dY) >= Math.Abs(dX + dZ))
                {
                    if (dY > 0) blockSide = BlockFacing.DOWN;
                }
                else if (dX == 0)
                {
                    blockSide = (dZ > 0) ? BlockFacing.NORTH : BlockFacing.SOUTH;
                }
                else
                {
                    blockSide = (dX > 0) ? BlockFacing.WEST : BlockFacing.EAST;
                }
                if (blockedBy(block, pos, blockSide)) return;    // NOTE: for waterwheels with radius larger than one, we are not checking whether any inner blocks block the rotation - that should probably be checked also
                                                                // also seems better to check for any block with a collision box, e.g. axle is not solid on any side

                // Apply rotational force
                if (!block.ForFluidsLayer) block = Api.World.BlockAccessor.GetBlock(pos, BlockLayersAccess.Fluid);    // Need because if there are stones or plants or snowlayer at pos, this will miss the fluid block
                if (block is IBlockFlowing waterBlock && !waterBlock.IsStill && getFlowSpeed(block) > requiresMinFlowSpeed)
                {
                    FastVec3f pushVector = waterBlock.GetPushVector(pos);
                    moment += new FastVec3f(dX, dY, dZ).Normalize().Cross(axialVec).Dot(pushVector);    // Applies the push in the correct rotational sense for this position
                    suitablePowerSourceBlockCount++;
                    lastWaterPos.Set(pos);
                    lastWaterBlock = block;
                    if (firstsWaterBlock == null)
                    {
                        firstWaterPos.Set(pos);
                        firstsWaterBlock = block;
                    }
                }
            }

            moment *= radius;       // Optional but physically correct: the torque on larger radius wheels is higher, due to leverage.  Extension for modders: larger wheels should have slower rotational speed due to distance

            if (moment > 0)    // In this case the first and last need to be reversed - without this check we don't know which way around the wheel our for (i = ...) loop above ran
            {
                lastWaterBlock = firstsWaterBlock;
                lastWaterPos = firstWaterPos;
            }

            if (lastWaterBlock != null)
            {
                // Figure out the *next* block in this rivulet's flow, and ensure it's set to a normal flowing water (not rapid), if not already

                pos.Set(lastWaterPos);
                pos.Down();
                if (!ReplaceRapidWater(pos) && lastWaterBlock.LiquidLevel > 1)    // If liquidLevel == 1, the only possible flow would be downwards
                {
                    pos.Up();
                    // If we did not find water below, now look in the direction of flow of this block
                    FastVec3f pushVector = (lastWaterBlock as IBlockFlowing).GetPushVector(pos).Normalize();
                    pos.Add(pushVector.X * 1.5f, 0, pushVector.Z * 1.5f);
                    if (!ReplaceRapidWater(pos) && Math.Abs(pushVector.X) + Math.Abs(pushVector.Z) > 1f)
                    {
                        // Now test the two straight positions if this is a diagonal flowing block
                        // First straight position: Z move only
                        pos.Add(pushVector.X * -1.5f, 0, 0);
                        if (!ReplaceRapidWater(pos))
                        {
                            // Second straight position: X move only
                            pos.Add(pushVector.X * 1.5f, 0, -pushVector.Z * 1.5f);
                            if (!ReplaceRapidWater(pos))
                            {
                                // Found no water in any of the positions expected adjacent to the last water block
#if DEBUG
                                // Throw exception in debug build, so that coding team can hopefully spot this and fix. In release build do nothing (only possible consequence is that rapid water does not get destroyed by a waterwheel in a certain configuration)
                                throw new Exception("Could not find water exit position from waterwheel at " + Pos);
#endif
                            }
                        }
                    }
                }
            }
            flowRate = Math.Abs(moment * 750);
            if (flowRate > 0)
            {
                float prevDir = dir;
                dir = Math.Sign(moment) * -1;
                if (dir != prevDir)
                {
                    bool oppositeDir = (dir < 0);
                    this.SetPropagationDirection(new MechPowerPath(oppositeDir ? facing.Opposite : facing, 1, Pos, false));
                }
            }
        }

        // Returns true if any type of water found at this position (most commonly, will be where rapid-flowing water was already replaced), otherwise false
        protected bool ReplaceRapidWater(BlockPos pos)
        {
            Block nextBlock = Api.World.BlockAccessor.GetBlock(pos, BlockLayersAccess.Fluid);
            var bh = nextBlock.GetBehavior<BlockBehaviorFiniteSpreadingLiquid>();
            if (bh != null && nextBlock.LiquidCode == "water")
            {
                if (!bh.multiplySpread)    // If the water here should not spread (e.g. vanilla rapid-flowing water) then replace it with standard water here
                {
                    string code = nextBlock.Code.FirstCodePart();
                    code = "water" + nextBlock.Code.Path.Substring(code.Length);
                    Block newBlock = Api.World.GetBlock(new AssetLocation(nextBlock.Code.Domain, code));
                    int newBlockId = newBlock?.Id ?? 0;
                    Api.World.BlockAccessor.SetBlock(newBlockId, pos, BlockLayersAccess.Fluid);
                    newBlock.OnNeighbourBlockChange(Api.World, pos, pos);
                    Api.World.BlockAccessor.TriggerNeighbourBlockUpdate(pos);   // Needed because setting blocks to the fluids layer does not update neighbours, by default (as currently implemented in BlockAccessorRelaxed.SetFluidBlockInternal(), game version 1.22-pre.2)
                }

                return true;
            }

            return false;
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

            if (!bebconstructable.IsComplete)
            {
                sb.AppendLine(Lang.Get("Construction step: {0}/{1}", bebconstructable.CurrentCompletedStage, bebconstructable.Stages));
            }
            else
            {

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
}
