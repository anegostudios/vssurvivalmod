using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent.Mechanics;

#nullable disable

namespace Vintagestory.GameContent.Mechanics
{
    public class SpurGearMultiBlockRenderer : MechBlockRenderer
    {
        readonly CustomMeshDataPartFloat[] matrixAndLightFloats = new CustomMeshDataPartFloat[6];
        readonly MeshRef[] meshRefs = new MeshRef[6];
        readonly int[] faceInstanceCounts = new int[6];

        public SpurGearMultiBlockRenderer(ICoreClientAPI capi, MechanicalPowerMod mechanicalPowerMod, Block textureSourceBlock, CompositeShape shapeLoc)
            : base(capi, mechanicalPowerMod)
        {
            Shape shape = Shape.TryGet(capi, "shapes/block/wood/mechanics/spurgear16.json");
            if (shape == null) return;

            foreach (BlockFacing face in BlockFacing.ALLFACES)
            {
                MeshData mesh;
                capi.Tesselator.TesselateShape(textureSourceBlock, shape, out mesh, DiscRotation(face), null, null);
                if (mesh == null) continue;

                CustomMeshDataPartFloat floats = NewMatrixPart();
                mesh.CustomFloats = floats;
                floats.SetAllocationSize(202000);

                matrixAndLightFloats[face.Index] = floats;
                meshRefs[face.Index] = capi.Render.UploadMesh(mesh);
            }
        }

        static CustomMeshDataPartFloat NewMatrixPart()
        {
            return new CustomMeshDataPartFloat(202000)
            {
                Instanced = true,
                InterleaveOffsets = new[] { 0, 16, 32, 48, 64 },
                InterleaveSizes = new[] { 4, 4, 4, 4, 4 },
                InterleaveStride = 80,
                StaticDraw = false
            };
        }

        static Vec3f DiscRotation(BlockFacing face)
        {
            if (face == BlockFacing.EAST) return new Vec3f(0, 270, 0);
            if (face == BlockFacing.SOUTH) return new Vec3f(0, 180, 0);
            if (face == BlockFacing.WEST) return new Vec3f(0, 90, 0);
            if (face == BlockFacing.UP) return new Vec3f(90, 0, 0);
            if (face == BlockFacing.DOWN) return new Vec3f(270, 0, 0);

            return new Vec3f(0, 0, 0);
        }

        static Vec3f AxisSign(BlockFacing face)
        {
            if (face == BlockFacing.EAST || face == BlockFacing.WEST) return new Vec3f(-1, 0, 0);
            if (face == BlockFacing.UP || face == BlockFacing.DOWN) return new Vec3f(0, 1, 0);

            return new Vec3f(0, 0, -1);
        }

        protected override void UpdateLightAndTransformMatrix(int index, Vec3f distToCamera, float rotRad, IMechanicalPowerRenderable dev)
        {
        }

        protected override void UpdateCustomFloatBuffer()
        {
            Array.Clear(faceInstanceCounts, 0, faceInstanceCounts.Length);

            Vec3d cameraPos = capi.World.Player.Entity.CameraPos;
            foreach (IMechanicalPowerRenderable device in renderedDevices.Values)
            {
                if (!(device is BEBehaviorMPSpurGear gear) || !gear.IsMultiBlock) continue;

                tmp.Set(
                    (float)(device.Position.X - cameraPos.X),
                    (float)(device.Position.InternalY - cameraPos.Y),
                    (float)(device.Position.Z - cameraPos.Z)
                );

                foreach (BlockFacing face in BlockFacing.ALLFACES)
                {
                    if (!gear.HasDisc(face) || matrixAndLightFloats[face.Index] == null) continue;

                    BlockPos axlePos = device.Position.AddCopy(face);
                    BEBehaviorMPBase axleBeh = capi.World.BlockAccessor.GetBlockEntity(axlePos)?.GetBehavior<BEBehaviorMPBase>();
                    float rotation = (axleBeh?.AngleRad ?? device.AngleRad) % GameMath.TWOPI;

                    Vec3f axis = AxisSign(face);
                    int instanceIndex = faceInstanceCounts[face.Index]++;
                    UpdateLightAndTransformMatrix(
                        matrixAndLightFloats[face.Index].Values,
                        instanceIndex,
                        tmp,
                        device.LightRgba,
                        rotation * axis.X,
                        rotation * axis.Y,
                        rotation * axis.Z
                    );
                }
            }
        }

        public override void OnRenderFrame(float deltaTime, IShaderProgram prog)
        {
            UpdateCustomFloatBuffer();

            foreach (BlockFacing face in BlockFacing.ALLFACES)
            {
                int count = faceInstanceCounts[face.Index];
                if (count <= 0 || meshRefs[face.Index] == null) continue;

                matrixAndLightFloats[face.Index].Count = count * 20;
                updateMesh.CustomFloats = matrixAndLightFloats[face.Index];
                capi.Render.UpdateMesh(meshRefs[face.Index], updateMesh);
                capi.Render.RenderMeshInstanced(meshRefs[face.Index], count);
            }
        }

        public override void Dispose()
        {
            base.Dispose();
            foreach (MeshRef meshRef in meshRefs)
            {
                meshRef?.Dispose();
            }
        }
    }
}
