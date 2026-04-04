using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.GameContent.Mechanics
{
    public class GenericMechBlockRenderer : MechBlockRenderer
    {
        private CustomMeshDataPartFloat matrixAndLightFloats;
        private readonly List<MeshRef> blockMeshRefs = new List<MeshRef>();
        private readonly List<int> blockMeshTextureIds = new List<int>();

        public GenericMechBlockRenderer(ICoreClientAPI capi, MechanicalPowerMod mechanicalPowerMod, Block textureSoureBlock, CompositeShape shapeLoc)
            : base(capi, mechanicalPowerMod)
        {
            AssetLocation loc = shapeLoc.Base.Clone().WithPathPrefixOnce("shapes/").WithPathAppendixOnce(".json");

            Shape shape = API.Common.Shape.TryGet(capi, loc);
            Vec3f rot = new Vec3f(shapeLoc.rotateX, shapeLoc.rotateY, shapeLoc.rotateZ);

            capi.Tesselator.TesselateShape(textureSoureBlock, shape, out MeshData blockMesh, rot, shapeLoc.QuantityElements, shapeLoc.SelectiveElements);

            if (shapeLoc.Overlays != null)
            {
                for (int i = 0; i < shapeLoc.Overlays.Length; i++)
                {
                    CompositeShape ovShapeCmp = shapeLoc.Overlays[i];
                    rot = new Vec3f(ovShapeCmp.rotateX, ovShapeCmp.rotateY, ovShapeCmp.rotateZ);

                    Shape ovshape = API.Common.Shape.TryGet(capi, ovShapeCmp.Base.Clone().WithPathPrefixOnce("shapes/").WithPathAppendixOnce(".json"));
                    capi.Tesselator.TesselateShape(textureSoureBlock, ovshape, out MeshData overlayMesh, rot);
                    blockMesh.AddMeshData(overlayMesh);
                }
            }

            // 16 floats matrix, 4 floats light rgbs
            blockMesh.CustomFloats = matrixAndLightFloats = new CustomMeshDataPartFloat((16 + 4) * 10100)
            {
                Instanced = true,
                InterleaveOffsets = new int[] { 0, 16, 32, 48, 64 },
                InterleaveSizes = new int[] { 4, 4, 4, 4, 4 },
                InterleaveStride = 16 + 4 * 16,
                StaticDraw = false,
            };
            blockMesh.CustomFloats.SetAllocationSize((16 + 4) * 10100);

            MeshData[] splitMeshes = blockMesh.SplitByTextureId();

            for (int i = 0; i < splitMeshes.Length; i++)
            {
                MeshData mesh = splitMeshes[i];
                if (mesh == null || mesh.VerticesCount == 0) continue;

                if (mesh.CustomFloats == null)
                {
                    mesh.CustomFloats = matrixAndLightFloats;
                }
                else
                {
                    // keep the same instanced float layout for every split mesh
                    mesh.CustomFloats = matrixAndLightFloats;
                }

                int atlasTextureId = (mesh.TextureIds != null && mesh.TextureIds.Length > 0)
                    ? mesh.TextureIds[0]
                    : capi.BlockTextureAtlas.Positions[0].atlasTextureId;

                blockMeshTextureIds.Add(atlasTextureId);
                blockMeshRefs.Add(capi.Render.UploadMesh(mesh));
            }
        }

        protected override void UpdateLightAndTransformMatrix(int index, Vec3f distToCamera, float rotation, IMechanicalPowerRenderable dev)
        {
            float rotX = rotation * dev.AxisSign[0];
            float rotY = rotation * dev.AxisSign[1];
            float rotZ = rotation * dev.AxisSign[2];

            if (dev is BEBehaviorMPToggle tog && (rotX == 0 ^ tog.IsRotationReversed()))
            {
                rotY = GameMath.PI;
                rotZ = -rotZ;
            }

            UpdateLightAndTransformMatrix(matrixAndLightFloats.Values, index, distToCamera, dev.LightRgba, rotX, rotY, rotZ);
        }

        public override void OnRenderFrame(float deltaTime, IShaderProgram prog)
        {
            UpdateCustomFloatBuffer();

            if (quantityBlocks <= 0 || blockMeshRefs.Count == 0) return;

            matrixAndLightFloats.Count = quantityBlocks * 20;
            updateMesh.CustomFloats = matrixAndLightFloats;

            for (int i = 0; i < blockMeshRefs.Count; i++)
            {
                prog.BindTexture2D("tex", blockMeshTextureIds[i], 0);
                capi.Render.UpdateMesh(blockMeshRefs[i], updateMesh);
                capi.Render.RenderMeshInstanced(blockMeshRefs[i], quantityBlocks);
            }
        }

        public override void Dispose()
        {
            base.Dispose();

            for (int i = 0; i < blockMeshRefs.Count; i++)
            {
                blockMeshRefs[i]?.Dispose();
            }

            blockMeshRefs.Clear();
            blockMeshTextureIds.Clear();
        }
    }
}
