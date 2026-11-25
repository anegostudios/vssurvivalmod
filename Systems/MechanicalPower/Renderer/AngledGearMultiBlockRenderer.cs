using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.GameContent.Mechanics
{
    public class AngledGearMultiBlockRenderer : MechBlockRenderer
    {
        class FacePool
        {
            public MaterialMeshPool Gear;
            public MaterialMeshPool Shaft;
        }

        static readonly Vec2f noUvOffset = new();

        readonly Dictionary<long, FacePool> pools = [];
        readonly BlockPos lookupPos = new BlockPos(0, 0, 0);
        readonly float[] lightAndTransform = new float[InstanceDataSize];
        readonly Vec2f materialUvOffset = new();

        public AngledGearMultiBlockRenderer(ICoreClientAPI capi, MechanicalPowerMod mechanicalPowerMod, Block textureSourceBlock, CompositeShape shapeLoc) : base(capi, mechanicalPowerMod)
        {
        }

        FacePool getPool(BlockAngledGears gearBlock, BlockFacing face)
        {
            long key = ((long)gearBlock.BlockId << 3) | (uint)face.Index;
            if (pools.TryGetValue(key, out FacePool pool)) return pool;

            Block faceBlock = gearBlock.GetGearBlock(capi.World, false, face);
            if (faceBlock?.Shape == null) return null;

            CompositeShape shape = faceBlock.Shape;
            AssetLocation loc = shape.Base.Clone().WithPathPrefixOnce("shapes/").WithPathAppendixOnce(".json");
            Shape resolvedShape = API.Common.Shape.TryGet(capi, loc);
            if (resolvedShape == null) return null;

            Vec3f rotation = new(shape.rotateX, shape.rotateY, shape.rotateZ);
            if (gearBlock.HasShaft)
            {
                pool = new FacePool { Gear = createMaterialMeshPool(faceBlock, SpurGearRenderer.GetDiscShape(resolvedShape), rotation, shape.SelectiveElements) };
                try
                {
                    pool.Shaft = createMaterialMeshPool(faceBlock, SpurGearRenderer.GetHubShape(resolvedShape), rotation, shape.SelectiveElements);
                }
                catch
                {
                    disposeMaterialMeshPool(pool.Gear);
                    throw;
                }
            }
            else
            {
                pool = new FacePool { Gear = createMaterialMeshPool(faceBlock, resolvedShape, rotation, shape.SelectiveElements) };
            }
            pools[key] = pool;
            return pool;
        }

        // Same shaft handedness as the axle renderer for each face axis.
        static Vec3f getAxisSign(BlockFacing face)
        {
            if (face.IsAxisWE) return BlockFacing.WEST.Normalf;
            if (face.IsVertical) return BlockFacing.UP.Normalf;
            return BlockFacing.NORTH.Normalf;
        }

        void addInstance(MaterialMeshPool pool, BEBehaviorMPAngledGears gear, BlockFacing face, BEBehaviorMPAxle materialSource, float rotation, Vec3f axis)
        {
            UpdateLightAndTransformMatrix(lightAndTransform, 0, tmp, gear.LightRgba, rotation * axis.X, rotation * axis.Y, rotation * axis.Z);
            foreach (var piece in pool.Pieces)
            {
                Vec2f uvOffset = noUvOffset;
                if (piece.MaterialTextureCode != null)
                {
                    if (materialSource?.TryGetMaterialUvOffset(piece.MaterialTextureCode, materialUvOffset) != true)
                    {
                        gear.GetGearMaterialUvOffset(capi, face, piece.MaterialTextureCode, materialUvOffset);
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
            foreach (FacePool pool in pools.Values)
            {
                pool.Gear.Count = 0;
                pool.Shaft?.Count = 0;
            }

            Vec3d cameraPos = capi.World.Player.Entity.CameraPos;
            foreach (IMechanicalPowerRenderable device in renderedDevices.Values)
            {
                if (device is not BEBehaviorMPAngledGears gear || gear.Block is not BlockAngledGearMulti) continue;

                tmp.Set(
                    (float)(device.Position.X - cameraPos.X),
                    (float)(device.Position.InternalY - cameraPos.Y),
                    (float)(device.Position.Z - cameraPos.Z)
                );

                bool jammed = gear.IsMultiGearJammed;
                foreach (BlockFacing face in BlockFacing.ALLFACES)
                {
                    BlockAngledGears gearBlock = gear.GetGearBlock(face);
                    if (gearBlock == null) continue;

                    FacePool pool = getPool(gearBlock, face);
                    if (pool == null) continue;

                    lookupPos.Set(device.Position, device.Position.dimension).Add(face);
                    BEBehaviorMPBase support = mechanicalPowerMod.GetDevice(lookupPos);
                    float shaftAngle = (support?.AngleRad ?? gear.GetMultiGearAngleRad(face)) % GameMath.TWOPI;
                    float rotation = gear.GetMultiGearRenderAngleOffset(face);
                    if (!jammed) rotation = (shaftAngle + rotation) % GameMath.TWOPI;

                    Vec3f axis = getAxisSign(face);
                    addInstance(pool.Gear, gear, face, null, rotation, axis);
                    if (pool.Shaft != null) addInstance(pool.Shaft, gear, face, support as BEBehaviorMPAxle, shaftAngle, axis);
                }
            }
        }

        public override void OnRenderFrame(float deltaTime, IShaderProgram prog)
        {
            UpdateCustomFloatBuffer();

            foreach (FacePool pool in pools.Values)
            {
                renderMaterialMeshPool(prog, pool.Gear);
                if (pool.Shaft != null) renderMaterialMeshPool(prog, pool.Shaft);
            }
        }

        public override void Dispose()
        {
            base.Dispose();
            foreach (FacePool pool in pools.Values)
            {
                disposeMaterialMeshPool(pool.Gear);
                if (pool.Shaft != null) disposeMaterialMeshPool(pool.Shaft);
            }
        }
    }
}
