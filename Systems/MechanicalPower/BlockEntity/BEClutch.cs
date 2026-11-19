using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.GameContent.Mechanics
{
    /// <summary>
    /// When engaged: add transmission to the network and also render this turning
    /// When not engaged: transmission block does not continue a network but can still be connected to 2 networks on 2 sides!
    /// </summary>
    public class BEClutch : BlockEntity, IMechanicalPowerRenderable
    {
        const double degrees15 = Math.PI / 12;
        const double engagedAngle = 2 * degrees15;
        const float reverseAngle = GameMath.TWOPI; // * 17f / 16f;
        const float angledGearPhaseOffset = GameMath.PI / 8f;
        protected MechanicalPowerMod manager;
        public bool Engaged { get; protected set; }
        public ItemStack PlacedStack { get; private set; }
        private BlockPos transmissionPos;
        private BlockPos axleLookupPos;
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

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            Shape = Block.Shape;
            Facing = BlockFacing.FromCode(Block.Variant["side"]);
            this.transmissionPos = Pos.AddCopy(Facing);
            axleLookupPos = new BlockPos(Pos.dimension);

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

        public override void OnBlockPlaced(ItemStack byItemStack = null)
        {
            base.OnBlockPlaced(byItemStack);
            if (byItemStack == null) return;

            PlacedStack = byItemStack.Clone();
            PlacedStack.StackSize = 1;
            MarkDirty(true);
        }

        float? getNeighbourAngleRad()
        {
            var neighbour = Api.World.BlockAccessor.GetBlockEntity(transmissionPos);
            var trans = neighbour?.GetBehavior<BEBehaviorMPTransmission>();
            if (trans != null) return trans.AngleRad;

            var gearBeh = neighbour?.GetBehavior<BEBehaviorMPAngledGears>();
            BlockFacing shaftFace = gearBeh?.GetCollinearGearFace();
            if (shaftFace == null) return null;

            axleLookupPos.Set(transmissionPos).Add(shaftFace);
            var axleBeh = Api.World.BlockAccessor.GetBlockEntity(axleLookupPos)?.GetBehavior<BEBehaviorMPBase>();
            float displayed = axleBeh?.AngleRad ?? gearBeh.GetMultiGearAngleRad(shaftFace);
            return (displayed + gearBeh.GetMultiGearRenderAngleOffset(shaftFace) + angledGearPhaseOffset) % GameMath.TWOPI;
        }

        public float RotationNeighbour()
        {
            if (armAngle > degrees15)
            {
                float? rawAngle = getNeighbourAngleRad();
                if (rawAngle.HasValue)
                {
                    float transmissionAngle = (this.Facing == BlockFacing.EAST || this.Facing == BlockFacing.NORTH ? reverseAngle - rawAngle.Value : rawAngle.Value) % GameMath.TWOPI + drumAngleOffset;
                    if (armAngle < engagedAngle)
                    {
                        float angleAdjust = catchUpAngle * (float) (2D - armAngle / degrees15);
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
                float? rawAngle = getNeighbourAngleRad();
                if (rawAngle.HasValue)
                {
                    float transmissionAngle = (this.Facing == BlockFacing.EAST || this.Facing == BlockFacing.NORTH ? reverseAngle - rawAngle.Value : rawAngle.Value) % GameMath.TWOPI;
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
            dt *= 4;
            if (Engaged)
            {
                if (armAngle < engagedAngle)
                {
                    armAngle += degrees15 * dt;
                    if (armAngle > engagedAngle) armAngle = engagedAngle;
                }
            }
            else if (armAngle > 0d)
            {
                armAngle -= degrees15 * dt;
                if (armAngle < 0d) armAngle = 0d;
            }
        }

        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tesselator)
        {
            lightRbs = Api.World.BlockAccessor.GetLightRGBs(Pos);
            ICoreClientAPI capi = Api as ICoreClientAPI;
            Shape shape = API.Common.Shape.TryGet(capi, "shapes/block/wood/mechanics/clutch-rest.json");
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
            capi.Tesselator.TesselateShape(Block.Code + " rest", shape, out MeshData mesh, ((BlockClutch)Block).GetMaterialTextureSource(capi, shape, Block.Code + " rest", PlacedStack), new Vec3f(0, rotateY, 0));
            mesher.AddMeshData(mesh);
            return true;
        }

        public bool OnInteract(IPlayer byPlayer)
        {
            var neighbour = Api.World.BlockAccessor.GetBlockEntity(transmissionPos);
            BEBehaviorMPTransmission be = neighbour?.GetBehavior<BEBehaviorMPTransmission>();
            BEBehaviorMPAngledGears gearHub = neighbour?.GetBehavior<BEBehaviorMPAngledGears>();
            if (gearHub?.IsCollinearHub != true) gearHub = null;
            if (!Engaged && be != null && be.engaged) return true;
            Engaged = !Engaged;
            Api.World.PlaySoundAt(new AssetLocation("sounds/effect/woodswitch.ogg"), Pos, 0, byPlayer);
            be?.CheckEngaged(Api.World.BlockAccessor, true);
            gearHub?.CheckEngagedByClutch(Api.World.BlockAccessor, true);

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
            manager?.RemoveDeviceForRender(this);
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);

            PlacedStack = tree.GetItemstack("placedStack");
            PlacedStack?.ResolveBlockOrItem(worldAccessForResolve);
            Engaged = tree.GetBool("engaged");
            if (Engaged && armAngle == 0d) armAngle = engagedAngle;
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);

            tree.SetBool("engaged", Engaged);
            if (PlacedStack == null) tree.RemoveAttribute("placedStack");
            else tree.SetItemstack("placedStack", PlacedStack);
        }

        public override void OnLoadCollectibleMappings(IWorldAccessor worldForNewMappings, Dictionary<int, AssetLocation> oldBlockIdMapping, Dictionary<int, AssetLocation> oldItemIdMapping, int schematicSeed, bool resolveImports)
        {
            base.OnLoadCollectibleMappings(worldForNewMappings, oldBlockIdMapping, oldItemIdMapping, schematicSeed, resolveImports);
            if (PlacedStack == null) return;
            if (!PlacedStack.FixMapping(oldBlockIdMapping, oldItemIdMapping, worldForNewMappings))
            {
                PlacedStack = null;
                return;
            }
            PlacedStack.Collectible.OnLoadCollectibleMappings(worldForNewMappings, new DummySlot(PlacedStack), oldBlockIdMapping, oldItemIdMapping, resolveImports);
        }

        public override void OnStoreCollectibleMappings(Dictionary<int, AssetLocation> blockIdMapping, Dictionary<int, AssetLocation> itemIdMapping)
        {
            base.OnStoreCollectibleMappings(blockIdMapping, itemIdMapping);
            PlacedStack?.Collectible?.OnStoreCollectibleMappings(Api.World, new DummySlot(PlacedStack), blockIdMapping, itemIdMapping);
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder sb)
        {
            base.GetBlockInfo(forPlayer, sb);
            sb.AppendLine(string.Format(Lang.Get(Engaged ? "Engaged" : "Disengaged")));
        }

        public void onNeighbourChange(BlockPos neibpos)
        {
            if (Engaged && neibpos.Equals(transmissionPos))
            {
                Engaged = false;
            }
        }
    }
}
