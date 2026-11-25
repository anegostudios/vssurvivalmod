using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.GameContent.Mechanics
{
    public class SpurGearRenderer : MechBlockRenderer
    {
        class GearMeshPool
        {
            public MaterialMeshPool Disc;
            public MaterialMeshPool Hub;
        }

        static readonly Vec2f noUvOffset = new();

        readonly GearMeshPool ownPool;
        readonly Dictionary<long, GearMeshPool> extraPools = [];
        readonly Dictionary<int, MaterialMeshPool> shaftPools = [];
        readonly Block textureSourceBlock;
        readonly BlockPos lookupPos = new BlockPos(0, 0, 0);
        readonly float[] lightAndTransform = new float[InstanceDataSize];
        readonly Vec2f materialUvOffset = new();

        public SpurGearRenderer(ICoreClientAPI capi, MechanicalPowerMod mechanicalPowerMod, Block textureSourceBlock, CompositeShape shapeLoc) : base(capi, mechanicalPowerMod)
        {
            this.textureSourceBlock = textureSourceBlock;

            AssetLocation loc = shapeLoc.Base.Clone().WithPathPrefixOnce("shapes/").WithPathAppendixOnce(".json");
            Shape shape = API.Common.Shape.TryGet(capi, loc);
            ownPool = createGearPool(textureSourceBlock, shape, new Vec3f(shapeLoc.rotateX, shapeLoc.rotateY, shapeLoc.rotateZ));
        }

        GearMeshPool createGearPool(Block gearBlock, Shape shape, Vec3f rotation)
        {
            return new GearMeshPool
            {
                Disc = createMaterialMeshPool(gearBlock, GetDiscShape(shape), rotation),
                Hub = createMaterialMeshPool(gearBlock, GetHubShape(shape), rotation)
            };
        }

        // The disc carries the mesh phase, the hub turns with the shaft, so they are drawn from separate meshes.
        public static Shape GetDiscShape(Shape shape)
        {
            Shape discShape = shape.Clone();
            foreach (ShapeElement root in discShape.Elements)
            {
                root.FacesResolved = new ShapeElementFace[6];
                if (root.Children == null) continue;
                foreach (ShapeElement element in root.Children)
                {
                    if (isHub(element)) hideFaces(element);
                }
            }
            return discShape;
        }

        public static Shape GetHubShape(Shape shape)
        {
            Shape hubShape = shape.Clone();
            foreach (ShapeElement root in hubShape.Elements)
            {
                root.RotationX = 0;
                root.RotationY = 0;
                root.RotationZ = 0;
                if (root.Children == null) continue;
                foreach (ShapeElement element in root.Children)
                {
                    if (!isHub(element)) hideFaces(element);
                }
            }
            return hubShape;
        }

        static bool isHub(ShapeElement element)
        {
            return element.Name == "Hub" || element.Name == "Hub2";
        }

        static void hideFaces(ShapeElement element)
        {
            element.FacesResolved = new ShapeElementFace[6];
            if (element.Children == null) return;
            foreach (ShapeElement child in element.Children) hideFaces(child);
        }

        GearMeshPool getPool(BlockSpurGear gearBlock, BlockFacing face)
        {
            long key = ((long)gearBlock.BlockId << 3) | (uint)face.Index;
            if (extraPools.TryGetValue(key, out GearMeshPool pool)) return pool;

            // The face variant of the disc's gear block carries the correctly rotated shape.
            Block faceBlock = (capi.World.GetBlock(gearBlock.CodeWithVariant("orientation", $"{face.Code[0]}")) as BlockSpurGear) ?? gearBlock;
            CompositeShape cshape = faceBlock.Shape;
            Shape shape = API.Common.Shape.TryGet(capi, cshape.Base.Clone().WithPathPrefixOnce("shapes/").WithPathAppendixOnce(".json"));
            pool = createGearPool(faceBlock, shape, new Vec3f(cshape.rotateX, cshape.rotateY, cshape.rotateZ));
            extraPools[key] = pool;
            return pool;
        }

        static Vec3f getAxisSign(BlockFacing face)
        {
            if (face.IsAxisWE) return BlockFacing.WEST.Normalf;
            if (face.IsVertical) return BlockFacing.UP.Normalf;
            return BlockFacing.NORTH.Normalf;
        }

        BEBehaviorMPAxle getSupport(BEBehaviorMPSpurGear gear, BlockFacing face)
        {
            lookupPos.Set(gear.Position, gear.Position.dimension).Add(face);
            return mechanicalPowerMod.GetDevice(lookupPos) as BEBehaviorMPAxle;
        }

        MaterialMeshPool getShaftPool(Block shaftBlock)
        {
            if (shaftPools.TryGetValue(shaftBlock.BlockId, out MaterialMeshPool pool)) return pool;

            CompositeShape cshape = shaftBlock.Shape;
            Shape shape = API.Common.Shape.TryGet(capi, cshape.Base.Clone().WithPathPrefixOnce("shapes/").WithPathAppendixOnce(".json"));
            pool = createMaterialMeshPool(shaftBlock, shape, new Vec3f(cshape.rotateX, cshape.rotateY, cshape.rotateZ));
            shaftPools[shaftBlock.BlockId] = pool;
            return pool;
        }

        void addInstance(MaterialMeshPool pool, BEBehaviorMPSpurGear gear, BlockFacing extraFace, bool throughShaft, float angle, Vec3f axis, BEBehaviorMPAxle materialSource = null)
        {
            UpdateLightAndTransformMatrix(lightAndTransform, 0, tmp, gear.LightRgba, angle * axis.X, angle * axis.Y, angle * axis.Z);
            foreach (var piece in pool.Pieces)
            {
                Vec2f uvOffset = noUvOffset;
                if (piece.MaterialTextureCode != null)
                {
                    if (materialSource?.TryGetMaterialUvOffset(piece.MaterialTextureCode, materialUvOffset) != true)
                    {
                        if (throughShaft) gear.GetThroughShaftMaterialUvOffset(capi, piece.MaterialTextureCode, materialUvOffset);
                        else if (extraFace != null) gear.GetExtraDiscMaterialUvOffset(capi, extraFace, piece.MaterialTextureCode, materialUvOffset);
                        else gear.TryGetMaterialUvOffset(piece.MaterialTextureCode, materialUvOffset);
                    }
                    uvOffset = materialUvOffset;
                }
                setMaterialInstance(piece.Floats.Values, pool.Count, lightAndTransform, uvOffset);
            }
            pool.Count++;
        }

        protected override void UpdateLightAndTransformMatrix(int index, Vec3f distToCamera, float rotRad, IMechanicalPowerRenderable dev)
        {
        }

        protected override void UpdateCustomFloatBuffer()
        {
            ownPool.Disc.Count = 0;
            ownPool.Hub.Count = 0;
            foreach (GearMeshPool pool in extraPools.Values)
            {
                pool.Disc.Count = 0;
                pool.Hub.Count = 0;
            }
            foreach (MaterialMeshPool pool in shaftPools.Values) pool.Count = 0;

            Vec3d cameraPos = capi.World.Player.Entity.CameraPos;
            foreach (IMechanicalPowerRenderable device in renderedDevices.Values)
            {
                if (device is not BEBehaviorMPSpurGear gear) continue;

                tmp.Set(
                    (float)(device.Position.X - cameraPos.X),
                    (float)(device.Position.InternalY - cameraPos.Y),
                    (float)(device.Position.Z - cameraPos.Z)
                );

                BEBehaviorMPAxle support = getSupport(gear, gear.Facing);
                Vec3f axis = getAxisSign(gear.Facing);
                addInstance(ownPool.Disc, gear, null, false, gear.GetDiscRenderAngle(gear.Facing, support), axis);
                addInstance(ownPool.Hub, gear, null, false, gear.GetShaftRenderAngle(support), axis, support);

                foreach (BlockFacing face in BlockFacing.ALLFACES)
                {
                    if (!gear.HasExtraExternalSpurFace(face)) continue;
                    BlockSpurGear discBlock = gear.GetExtraDiscBlock(face);
                    if (discBlock == null) continue;

                    GearMeshPool pool = getPool(discBlock, face);
                    BEBehaviorMPAxle extraSupport = getSupport(gear, face);
                    Vec3f extraAxis = getAxisSign(face);
                    addInstance(pool.Disc, gear, face, false, gear.GetDiscRenderAngle(face, extraSupport), extraAxis);
                    addInstance(pool.Hub, gear, face, false, gear.GetShaftRenderAngle(extraSupport), extraAxis, extraSupport);
                }

                if (gear.HasThroughShaft)
                {
                    Block shaftBlock = gear.GetThroughShaftRenderBlock();
                    if (shaftBlock != null) addInstance(getShaftPool(shaftBlock), gear, null, true, gear.GetShaftRenderAngle(support), axis);
                }
            }
        }

        public override void OnRenderFrame(float deltaTime, IShaderProgram prog)
        {
            UpdateCustomFloatBuffer();

            renderMaterialMeshPool(prog, ownPool.Disc);
            renderMaterialMeshPool(prog, ownPool.Hub);

            foreach (GearMeshPool pool in extraPools.Values)
            {
                renderMaterialMeshPool(prog, pool.Disc);
                renderMaterialMeshPool(prog, pool.Hub);
            }

            foreach (MaterialMeshPool pool in shaftPools.Values) renderMaterialMeshPool(prog, pool);
        }

        public override void Dispose()
        {
            base.Dispose();
            disposeMaterialMeshPool(ownPool.Disc);
            disposeMaterialMeshPool(ownPool.Hub);
            foreach (GearMeshPool pool in extraPools.Values)
            {
                disposeMaterialMeshPool(pool.Disc);
                disposeMaterialMeshPool(pool.Hub);
            }
            foreach (MaterialMeshPool pool in shaftPools.Values) disposeMaterialMeshPool(pool);
        }
    }
}
