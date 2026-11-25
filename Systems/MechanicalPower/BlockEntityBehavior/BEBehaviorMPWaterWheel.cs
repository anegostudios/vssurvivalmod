using System;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using static OpenTK.Graphics.OpenGL.GL;

#nullable disable

namespace Vintagestory.GameContent.Mechanics
{


    public class BEBehaviorMPWaterWheel : BEBehaviorMPRotor
    {
        protected float flowRate;
        protected BlockFacing facing;
        protected float waterDirection;
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
        protected override float TorqueDirection => base.TorqueDirection * waterDirection;


        public BEBehaviorMPWaterWheel(BlockEntity blockentity) : base(blockentity) { }

        public override void Initialize(ICoreAPI api, JsonObject properties)
        {
            bebconstructable = Blockentity.GetBehavior<BEBehaviorRightClickConstructable>();
            bebconstructable?.OnAttemptConstruct = OnAttemptConstruction;
            facing = BlockFacing.FromCode(Block.Variant["side"]).Opposite;

            base.Initialize(api, properties);

            this.sound = new AssetLocation("sounds/block/waterwheel-wood");
            Blockentity.RegisterGameTickListener(CheckWater, 1000);
            diameter = properties["diameter"].AsInt(3);

            requiresMinFlowSpeed = properties["requiresMinFlowSpeed"].AsFloat(1.5f);

            bebconstructable.OnShapeChanged += (shape) => Shape = shape;
        }

        protected override CompositeShape GetShape()
        {
            return bebconstructable?.shape;
        }

        public override float GetTorque(long tick, float speed, out float resistance)
        {
            float torque = base.GetTorque(tick, speed, out resistance);
            if (flowRate <= 0) resistance += Resistance;
            return torque;
        }

        public override MechPowerPath[] GetMechPowerExits(MechPowerPath entryDir)
        {
            return [
                entryDir,
                entryDir.PropagatedClone(entryDir.OutFacing.Opposite, !entryDir.invert, fromPos: Position)
            ];
        }

        public bool OnAttemptConstruction(IPlayer byPlayer, BlockSelection blockSel)
        {
            CheckWater(0);
            if (blocked)
            {
                (Api as ICoreClientAPI)?.TriggerIngameError(this, "waterwheelblocked", Lang.Get("error-wheel-blocked"));
                return false;
            }

            if (suitablePowerSourceBlockCount == 0)
            {
                (Api as ICoreClientAPI)?.TriggerIngameError(this, "waterwheelnowater", Lang.Get("error-wheel-nowater"));
            }

            return true;
        }

        // Checked both server side and client side: client side only to produce suitablePowerSourceBlockCount for the Block Info HUD
        protected void CheckWater(float dt)
        {
            bool incomplete = !bebconstructable.IsComplete;

            suitablePowerSourceBlockCount = 0;

            flowVec.Set(0, 0, 0);
            var fcw = facing.GetCW();
            int wheelX = fcw.Normali.X;
            int wheelZ = fcw.Normali.Z;
            FastVec3f axialVec = new FastVec3f(facing.Normalf);
            blocked = false;
            int radius = diameter / 2;
            if (radius < 1)
            {
                flowRate = 0;
                return;
            }

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
                if (blockedBy(block, pos, blockSide))
                {
                    flowRate = 0;
                    return;    // NOTE: for waterwheels with radius larger than one, we are not checking whether any inner blocks block the rotation - that should probably be checked also
                               // also seems better to check for any block with a collision box, e.g. axle is not solid on any side
                }

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

            var bh = lastWaterBlock?.GetBehavior<BlockBehaviorFiniteSpreadingLiquid>();
            if (Api.Side == EnumAppSide.Server && bh != null)
            {
                // Water leaving the wheel must return to normal flow.
                var blockAccessor = Api.World.BlockAccessor;
                pos.Set(lastWaterPos);
                Block solidBlock = blockAccessor.GetBlock(pos, BlockLayersAccess.Solid);
                BlockPos npos = pos.Copy();
                foreach (BlockFacing face in BlockFacing.ALLFACES)
                {
                    face.IterateThruFacingOffsets(npos);
                    if (face == BlockFacing.UP) continue;

                    // An existing falling column is still an outlet even when spreading cannot replace it.
                    if (face == BlockFacing.DOWN || bh.CanSpreadIntoBlock(lastWaterBlock, solidBlock, pos, npos, face, Api.World)) ReplaceRapidWater(npos);
                }

                // Downstream paths may be empty between liquid updates.
                foreach (var path in bh.FindDownwardPaths(Api.World, pos, lastWaterBlock))
                {
                    if (!bh.CanSpreadIntoBlock(lastWaterBlock, solidBlock, pos, path.pos, path.pos.FacingFrom(pos), Api.World)) continue;
                    if (blockAccessor.GetBlock(path.pos, BlockLayersAccess.Fluid).BlockId == 0)
                    {
                        blockAccessor.SetBlock(bh.GetLessLiquidBlockId(Api.World, path.pos, lastWaterBlock), path.pos, BlockLayersAccess.Fluid);
                    }

                    ReplaceRapidWater(path.pos);
                }
            }

            flowRate = Math.Abs(moment * 750);
            if (flowRate > 0) waterDirection = -Math.Sign(moment);

            if (incomplete)
            {
                // If incomplete, we have counted the flowRate but still set the power to 0
                flowRate = 0;
                flowVec.Set(0, 0, 0);
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

        public override MechanicalNetwork CreateJoinAndDiscoverNetwork(BlockFacing powerOutFacing)
        {
            var newNetwork = base.CreateJoinAndDiscoverNetwork(powerOutFacing);

            if (powerOutFacing != facing || newNetwork == null) return newNetwork;

            // Producers begin discovery through one face, so spread the same network through the other shaft end.
            BlockFacing oppositeFacing = powerOutFacing.Opposite;
            var oppositePath = new MechPowerPath(oppositeFacing, GearedRatio, Position, GetPropagationDirection() != oppositeFacing, AngleTransform);
            if (!spreadTo(Api, newNetwork, Position.AddCopy(oppositeFacing), oppositePath, out Vec3i missingChunkPos))
            {
                newNetwork.AwaitChunkThenDiscover(missingChunkPos);
                manager.testFullyLoaded(newNetwork);
            }

            return newNetwork;
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
            if (!bebconstructable.IsComplete)
            {
                sb.AppendLine(Lang.Get("waterwheel-construction-step-with-stage-current-and-total", bebconstructable.CurrentCompletedStage, bebconstructable.Stages));
                if (blocked) sb.AppendLine(Lang.Get("Wheel is blocked, make sure the entire wheel is free from solid blocks."));
            }
            else
            {

                if (blocked) sb.AppendLine(Lang.Get("Wheel is blocked, make sure the entire wheel is free from solid blocks."));
                else sb.AppendLine(Lang.Get("waterwheel-suitable-power-source-blocks-nearby-blockamount", suitablePowerSourceBlockCount));

                if (Api.World.EntityDebugMode)
                {
                    sb.AppendLine("<font color='#ccc'>flow vector= " + flowVec + "</font>");
                    sb.AppendLine("<font color='#ccc'>torque= " + TorqueFactor + "</font>");
                    sb.AppendLine("<font color='#ccc'>targetspeed= " + TargetSpeed + "</font>");
                    sb.AppendLine("<font color='#ccc'>flowrate = " + flowRate + "</font>");
                }
            }

            base.GetBlockInfo(forPlayer, sb);
        }
    }
}
