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
using Vintagestory.API.Util;

namespace Vintagestory.GameContent.Mechanics
{
    /// <summary>
    /// When engaged: add transmission to the network and also render this turning
    /// When not engaged: transmission block does not continue a network but can still be connected to 2 networks on 2 sides!
    /// </summary>
    public class BEClutch : BlockEntity, IMechanicalPowerRenderable
    {
        private static double DEGREES15 = Math.PI / 12;
        private static double DEGREES30 = 2 * DEGREES15;
        private static float REVERSEANGLE = GameMath.TWOPI; // * 17f / 16f;
        protected MechanicalPowerMod manager;
        public bool Engaged { get; protected set; }
        private BlockPos transmissionPos;
        public BlockFacing Facing { get; protected set; }

        public virtual BlockPos Position { get { return Pos; } }
        public Vec4f lightRbs = new Vec4f();
        public virtual Vec4f LightRgba { get { return lightRbs; } }
        public virtual int[] AxisSign { get; protected set; }
        public Vec3f hinge = new Vec3f(0.375f, 0f, 0.5f);
        private double armAngle;
        private float drumAngle;
        private float drumSpeed;
        private float drumAngleOffset = -0.000001f;
        private float transmissionAngleLast = -0.000001f;
        private float catchUpAngle = 0f;
        public virtual float AngleRad { get { return (float) armAngle; }}
        private CompositeShape shape = null;
        public virtual CompositeShape Shape
        {
            get { return shape; }
            set
            {
                CompositeShape prev = Shape;
                if (prev != null && manager != null)
                {
                    manager.RemoveDeviceForRender(this);
                    this.shape = value;
                    manager.AddDeviceForRender(this);
                }
                else
                {
                    this.shape = value;
                }
            }
        }

        BlockEntityAnimationUtil animUtil
        {
            get { return GetBehavior<BEBehaviorAnimatable>().animUtil; }
        }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            Shape = Block.Shape;
            Facing = BlockFacing.FromCode(Block.Variant["side"]);
            this.transmissionPos = Pos.AddCopy(Facing);

            manager = Api.ModLoader.GetModSystem<MechanicalPowerMod>();
            manager.AddDeviceForRender(this);

            AxisSign = new int[3] { 0, 0, 0 };
            switch(Facing.Index)
            {
                case 0:
                    AxisSign[0] = -1;
                    hinge = new Vec3f(0.5f, 0f, 0.375f);
                    break;
                case 2:
                    AxisSign[0] = 1;
                    hinge = new Vec3f(0.5f, 0f, 0.625f);
                    break;
                case 1:
                    AxisSign[2] = -1;
                    hinge = new Vec3f(0.625f, 0f, 0.5f);
                    break;
                default:
                    AxisSign[2] = 1;
                    break;
            }

            if (api.World.Side == EnumAppSide.Client)
            {
                RegisterGameTickListener(OnClientGameTick, 16);
            }
        }

        public float RotationNeighbour()
        {
            if (armAngle > DEGREES15)
            {
                BEBehaviorMPTransmission be = Api.World.BlockAccessor.GetBlockEntity(transmissionPos)?.GetBehavior<BEBehaviorMPTransmission>();
                if (be != null)
                {
                    float transmissionAngle = (this.Facing == BlockFacing.EAST || this.Facing == BlockFacing.NORTH ? REVERSEANGLE - be.AngleRad : be.AngleRad) % GameMath.TWOPI + drumAngleOffset;
                    if (armAngle < DEGREES30)
                    {
                        float angleAdjust = catchUpAngle * (float) (2D - armAngle / DEGREES15);
                        drumSpeed = transmissionAngle - angleAdjust - drumAngle;
                        drumAngle = transmissionAngle - angleAdjust;
                    }
                    else
                    {
                        drumSpeed = transmissionAngle - drumAngle;
                        drumAngle = transmissionAngle;
                        catchUpAngle = 0f;
                    }
                }
            }
            else if (this.Engaged)
            {
                BEBehaviorMPTransmission be = Api.World.BlockAccessor.GetBlockEntity(transmissionPos)?.GetBehavior<BEBehaviorMPTransmission>();
                if (be != null)
                {
                    float transmissionAngle = (this.Facing == BlockFacing.EAST || this.Facing == BlockFacing.NORTH ? REVERSEANGLE - be.AngleRad : be.AngleRad) % GameMath.TWOPI;
                    float targetSpeed = transmissionAngleLast == -0.000001f ? 0f : transmissionAngle - transmissionAngleLast;
                    transmissionAngleLast = transmissionAngle;

                    if (drumAngleOffset < 0f)
                    {
                        int eighths = (int) ((drumAngle - transmissionAngle) % GameMath.TWOPI / GameMath.TWOPI * 8f);
                        if (eighths < 0) eighths += 8;
                        //else eighths+=2;
                        drumAngleOffset = eighths * GameMath.TWOPI / 8f;
                        drumSpeed = 0;
                    }
                    float accel = targetSpeed - drumSpeed;
                    if (Math.Abs(accel) > 0.00045f) accel = accel < 0 ? -0.00045f : 0.00045f;
                    drumSpeed += accel;
                    drumAngle += drumSpeed;
                    catchUpAngle = (transmissionAngle + drumAngleOffset - drumAngle) % GameMath.TWOPI;
                    if (catchUpAngle > GameMath.PI) catchUpAngle = catchUpAngle - GameMath.TWOPI;
                    if (catchUpAngle < -GameMath.PI) catchUpAngle = catchUpAngle + GameMath.TWOPI;
                }
            }
            else
            {
                drumAngle += drumSpeed;
                if (drumAngle > GameMath.TWOPI) drumAngle = drumAngle % GameMath.TWOPI;
                drumSpeed *= 0.99f;
                if (drumSpeed > 0.0001f) drumSpeed -= 0.0001f;
                else if (drumSpeed > 0) drumSpeed = 0;
                else if (drumSpeed < -0.0001f) drumSpeed += 0.0001f;
                else if (drumSpeed < 0) drumSpeed = 0;
                drumAngleOffset = -0.000001f;
                transmissionAngleLast = -0.000001f;
            }
            return drumAngle;
        }

        private void OnClientGameTick(float dt)
        {
            if (Engaged)
            {
                if (armAngle < DEGREES30)
                {
                    armAngle += DEGREES15 * dt;
                    if (armAngle > DEGREES30) armAngle = DEGREES30;
                }
            }
            else if (armAngle > 0d)
            {
                armAngle -= DEGREES15 * dt;
                if (armAngle < 0d) armAngle = 0d;
            }
        }

        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tesselator)
        {
            lightRbs = Api.World.BlockAccessor.GetLightRGBs(Pos);
            ICoreClientAPI capi = Api as ICoreClientAPI;
            Shape shape = capi.Assets.TryGet("shapes/block/wood/mechanics/clutch-rest.json").ToObject<Shape>();
            float rotateY = 0f;
            switch(Facing.Index)
            {
                case 0:
                    rotateY = 180;
                    break;
                case 1:
                    rotateY = 90;
                    break;
                case 3:
                    rotateY = 270;
                    break;
                default:
                    break;
            }
            MeshData mesh;
            capi.Tesselator.TesselateShape(Block, shape, out mesh, new Vec3f(0, rotateY, 0));
            mesher.AddMeshData(mesh);
            return true;
        }

        public bool OnInteract(IPlayer byPlayer)
        {
            BEBehaviorMPTransmission be = Api.World.BlockAccessor.GetBlockEntity(transmissionPos)?.GetBehavior<BEBehaviorMPTransmission>();
            if (!Engaged && be != null && be.engaged) return true;
            Engaged = !Engaged;
            Api.World.PlaySoundAt(new AssetLocation("sounds/effect/woodswitch.ogg"), Pos.X + 0.5, Pos.Y + 0.5, Pos.Z + 0.5, byPlayer);
            if (be != null)
            {
                be.CheckEngaged(Api.World.BlockAccessor, true);
            }

            MarkDirty(true);
            return true;
        }

        public override void OnBlockRemoved()
        {
            base.OnBlockRemoved();
            manager.RemoveDeviceForRender(this);
        }

        public override void OnBlockUnloaded()
        {
            base.OnBlockUnloaded();
            manager.RemoveDeviceForRender(this);
        }

        public override void FromTreeAtributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAtributes(tree, worldAccessForResolve);

            Engaged = tree.GetBool("engaged");
            if (Engaged && armAngle == 0d) armAngle = DEGREES30; 
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);

            tree.SetBool("engaged", Engaged);
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder sb)
        {
            base.GetBlockInfo(forPlayer, sb);
            sb.AppendLine(string.Format(Lang.Get(Engaged ? "Engaged" : "Disengaged")));
        }
    }
}
