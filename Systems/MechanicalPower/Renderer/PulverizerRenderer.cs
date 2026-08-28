using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.GameContent.Mechanics
{
    public class PulverizerRenderer : MechBlockRenderer, ITexPositionSource
    {
        // this is pretty lame, make not hardcoded
        public static string[] metals = new string[] { "nometal", "tinbronze", "bismuthbronze", "blackbronze", "iron", "meteoriciron", "steel" };

        CustomMeshDataPartFloat matrixAndLightFloatsAxle;
        CustomMeshDataPartFloat[] matrixAndLightFloatsLPounder = new CustomMeshDataPartFloat[metals.Length];
        CustomMeshDataPartFloat[] matrixAndLightFloatsRPounder = new CustomMeshDataPartFloat[metals.Length];

        readonly List<MeshGroup> toggleGroups;
        readonly List<MeshGroup>[] lPoundGroups = new List<MeshGroup>[metals.Length];
        readonly List<MeshGroup>[] rPoundGroups = new List<MeshGroup>[metals.Length];

        readonly Vec3f axisCenter = new Vec3f(0.5f, 0.5f, 0.5f);

        int quantityAxles = 0;
        int[] quantityLPounders = new int[metals.Length];
        int[] quantityRPounders = new int[metals.Length];

        public Size2i AtlasSize => capi.BlockTextureAtlas.Size;

        public TextureAtlasPosition this[string textureCode]
        {
            get
            {
                if (textureCode == "cap")
                    return texSource["capmetal-" + metal];
                return texSource[textureCode];
            }
        }

        ITexPositionSource texSource;
        string metal;

        public PulverizerRenderer(ICoreClientAPI capi, MechanicalPowerMod mechanicalPowerMod, Block textureSoureBlock, CompositeShape shapeLoc) : base(capi, mechanicalPowerMod)
        {
            int count = (16 + 4) * 200;

            // Axle / toggle mesh
            Shape shape = API.Common.Shape.TryGet(capi, new AssetLocation("shapes/block/wood/mechanics/pulverizer-moving.json"));
            Vec3f rot = new Vec3f(shapeLoc.rotateX, shapeLoc.rotateY + 90f, shapeLoc.rotateZ);
            capi.Tesselator.TesselateShape(textureSoureBlock, shape, out MeshData toggleMesh, rot);
            toggleMesh.CustomFloats = matrixAndLightFloatsAxle = CreateCustomFloats(count);
            toggleGroups = UploadMeshGrouped(toggleMesh, matrixAndLightFloatsAxle);

            // Pounder meshes – one set per metal cap variant
            Shape shapel = API.Common.Shape.TryGet(capi, new AssetLocation("shapes/block/wood/mechanics/pulverizer-pounder-l.json"));
            Shape shaper = API.Common.Shape.TryGet(capi, new AssetLocation("shapes/block/wood/mechanics/pulverizer-pounder-r.json"));

            texSource = capi.Tesselator.GetTextureSource(textureSoureBlock);

            for (int i = 0; i < metals.Length; i++)
            {
                metal = metals[i];

                matrixAndLightFloatsLPounder[i] = CreateCustomFloats(count);
                matrixAndLightFloatsRPounder[i] = CreateCustomFloats(count);

                capi.Tesselator.TesselateShape("pulverizer-pounder-l", shapel, out MeshData lPounderMesh, this, rot);
                capi.Tesselator.TesselateShape("pulverizer-pounder-r", shaper, out MeshData rPounderMesh, this, rot);

                lPounderMesh.CustomFloats = matrixAndLightFloatsLPounder[i];
                rPounderMesh.CustomFloats = matrixAndLightFloatsRPounder[i];

                lPoundGroups[i] = UploadMeshGrouped(lPounderMesh, matrixAndLightFloatsLPounder[i]);
                rPoundGroups[i] = UploadMeshGrouped(rPounderMesh, matrixAndLightFloatsRPounder[i]);
            }
        }

        private CustomMeshDataPartFloat CreateCustomFloats(int count)
        {
            var result = new CustomMeshDataPartFloat(count)
            {
                Instanced = true,
                InterleaveOffsets = new int[] { 0, 16, 32, 48, 64 },
                InterleaveSizes = new int[] { 4, 4, 4, 4, 4 },
                InterleaveStride = 16 + 4 * 16,
                StaticDraw = false,
            };
            result.SetAllocationSize(count);
            return result;
        }

        protected override void UpdateLightAndTransformMatrix(int index, Vec3f distToCamera, float rotation, IMechanicalPowerRenderable dev)
        {
            BEBehaviorMPPulverizer bhpu = dev as BEBehaviorMPPulverizer;

            float rot = bhpu.bepu.hasAxle ? dev.AngleRad : 0;
            float axX = -Math.Abs(dev.AxisSign[0]);
            float axZ = -Math.Abs(dev.AxisSign[2]);

            // Axle
            if (bhpu.bepu.hasAxle)
            {
                UpdateLightAndTransformMatrix(matrixAndLightFloatsAxle.Values, quantityAxles, distToCamera, dev.LightRgba, rot * axX, rot * axZ, axisCenter, 0f);
                quantityAxles++;
            }

            if (bhpu.IsRotationReversed()) rot = -rot;

            // Left pounder
            int metalIndexLeft = bhpu.bepu.CapMetalIndexL;
            if (bhpu.bepu.hasLPounder && metalIndexLeft >= 0)
            {
                bool leftEmpty = bhpu.bepu.Inventory[1].Empty;
                float progress = GetProgress(bhpu.bepu.hasAxle ? rot - 0.45f + GameMath.PIHALF / 2f : 0f, 0f);

                UpdateLightAndTransformMatrix(matrixAndLightFloatsLPounder[metalIndexLeft].Values, quantityLPounders[metalIndexLeft], distToCamera, dev.LightRgba, 0f, 0f, axisCenter, Math.Max(progress / 6f + 0.0071f, leftEmpty ? -1 : 1 / 32f));

                if (progress < bhpu.prevProgressLeft && progress < 0.25f)
                {
                    if (bhpu.leftDir == 1) bhpu.OnClientSideImpact(false);
                    bhpu.leftDir = -1;
                }
                else bhpu.leftDir = 1;
                bhpu.prevProgressLeft = progress;

                quantityLPounders[metalIndexLeft]++;
            }

            // Right pounder
            int metalIndexRight = bhpu.bepu.CapMetalIndexR;
            if (bhpu.bepu.hasRPounder && metalIndexRight >= 0)
            {
                bool rightEmpty = bhpu.bepu.Inventory[0].Empty;
                float progress = GetProgress(bhpu.bepu.hasAxle ? rot - 0.45f : 0f, 0f);

                UpdateLightAndTransformMatrix(matrixAndLightFloatsRPounder[metalIndexRight].Values, quantityRPounders[metalIndexRight], distToCamera, dev.LightRgba, 0f, 0f, axisCenter, Math.Max(progress / 6f + 0.0071f, rightEmpty ? -1 : 1 / 32f));

                if (progress < bhpu.prevProgressRight && progress < 0.25f)
                {
                    if (bhpu.rightDir == 1) bhpu.OnClientSideImpact(true);
                    bhpu.rightDir = -1;
                }
                else bhpu.rightDir = 1;
                bhpu.prevProgressRight = progress;

                quantityRPounders[metalIndexRight]++;
            }
        }

        /// <summary>
        /// Converts axle angle to a vertical translation value for pounder motion.
        /// Applies a slight curve to mimic the physical toggle mechanism.
        /// </summary>
        private float GetProgress(float rot, float offset)
        {
            float progress = (rot % GameMath.PIHALF) / GameMath.PIHALF;
            if (progress < 0f) progress += 1f;
            progress = 0.6355f * (float)Math.Atan(2.2f * progress - 1.2f) + 0.5f;
            if (progress > 0.9f)
            {
                progress = 2.7f - 3 * progress;
                progress = 0.9f - progress * progress * 10f;
            }
            if (progress < 0f) progress = 0f;
            return progress;
        }

        /// <summary>
        /// Overload used by pounder sub-elements: applies a vertical translation in addition to
        /// the standard rotation, to animate the up/down pounding motion.
        /// </summary>
        protected void UpdateLightAndTransformMatrix(float[] values, int index, Vec3f distToCamera, Vec4f lightRgba, float rotX, float rotZ, Vec3f axis, float translate)
        {
            Mat4f.Identity(tmpMat);
            Mat4f.Translate(tmpMat, tmpMat, distToCamera.X + axis.X, distToCamera.Y + axis.Y + translate, distToCamera.Z + axis.Z);

            quat[0] = 0; quat[1] = 0; quat[2] = 0; quat[3] = 1;
            if (rotX != 0f) Quaterniond.RotateX(quat, quat, rotX);
            if (rotZ != 0f) Quaterniond.RotateZ(quat, quat, rotZ);

            Mat4f.MulQuat(tmpMat, quat);
            Mat4f.Translate(tmpMat, tmpMat, -axis.X, -axis.Y, -axis.Z);

            int j = index * 20;
            values[j] = lightRgba.R;
            values[++j] = lightRgba.G;
            values[++j] = lightRgba.B;
            values[++j] = lightRgba.A;
            for (int i = 0; i < 16; i++) values[++j] = tmpMat[i];
        }

        public override void OnRenderFrame(float deltaTime, IShaderProgram prog)
        {
            quantityAxles = 0;
            for (int i = 0; i < metals.Length; i++)
            {
                quantityLPounders[i] = 0;
                quantityRPounders[i] = 0;
            }

            UpdateCustomFloatBuffer();

            // Axle
            if (quantityAxles > 0)
                RenderGroups(prog, toggleGroups, matrixAndLightFloatsAxle, quantityAxles);

            // Pounders per metal variant
            for (int i = 0; i < metals.Length; i++)
            {
                if (quantityLPounders[i] > 0)
                    RenderGroups(prog, lPoundGroups[i], matrixAndLightFloatsLPounder[i], quantityLPounders[i]);

                if (quantityRPounders[i] > 0)
                    RenderGroups(prog, rPoundGroups[i], matrixAndLightFloatsRPounder[i], quantityRPounders[i]);
            }
        }

        public override void Dispose()
        {
            base.Dispose();
            DisposeGroups(toggleGroups);
            for (int i = 0; i < metals.Length; i++)
            {
                DisposeGroups(lPoundGroups[i]);
                DisposeGroups(rPoundGroups[i]);
            }
        }
    }
}
