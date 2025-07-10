using System;
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

        readonly MeshRef toggleMeshref;
        readonly MeshRef[] lPoundMeshrefs = new MeshRef[metals.Length];
        readonly MeshRef[] rPounderMeshrefs = new MeshRef[metals.Length];
        readonly Vec3f axisCenter = new Vec3f(0.5f, 0.5f, 0.5f);


        int quantityAxles = 0;
        int[] quantityLPounders = new int[metals.Length];
        int[] quantityRPounders = new int[metals.Length];

        public Size2i AtlasSize => capi.BlockTextureAtlas.Size;

        public TextureAtlasPosition this[string textureCode] {
            get
            {
                if (textureCode == "cap")
                {
                    return texSource["capmetal-" + metal];
                }

                return texSource[textureCode];
            }
        }


        ITexPositionSource texSource;
        string metal;

        public PulverizerRenderer(ICoreClientAPI capi, MechanicalPowerMod mechanicalPowerMod, Block textureSoureBlock, CompositeShape shapeLoc) : base(capi, mechanicalPowerMod)
        {

            // 16 floats matrix, 4 floats light rgbs
            int count = (16 + 4) * 200;


            AssetLocation loc = new AssetLocation("shapes/block/wood/mechanics/pulverizer-moving.json");
            Shape shape = API.Common.Shape.TryGet(capi, loc);
            Vec3f rot = new Vec3f(shapeLoc.rotateX, shapeLoc.rotateY + 90F, shapeLoc.rotateZ);
            capi.Tesselator.TesselateShape(textureSoureBlock, shape, out MeshData toggleMesh, rot);
            toggleMesh.CustomFloats = matrixAndLightFloatsAxle = createCustomFloats(count);
            toggleMeshref = capi.Render.UploadMesh(toggleMesh);


            AssetLocation locPounderL = new AssetLocation("shapes/block/wood/mechanics/pulverizer-pounder-l.json");
            AssetLocation locPounderR = new AssetLocation("shapes/block/wood/mechanics/pulverizer-pounder-r.json"); 
            
            Shape shapel = API.Common.Shape.TryGet(capi, locPounderL);
            Shape shaper = API.Common.Shape.TryGet(capi, locPounderR);

            texSource = capi.Tesselator.GetTextureSource(textureSoureBlock);

            

            for (int i = 0; i < metals.Length; i++)
            {
                metal = metals[i];

                matrixAndLightFloatsLPounder[i] = createCustomFloats(count);
                matrixAndLightFloatsRPounder[i] = createCustomFloats(count);

                capi.Tesselator.TesselateShape("pulverizer-pounder-l", shapel, out MeshData lPounderMesh, this, rot);
                capi.Tesselator.TesselateShape("pulverizer-pounder-r", shaper, out MeshData rPounderMesh, this, rot);
                lPounderMesh.CustomFloats = matrixAndLightFloatsLPounder[i];
                rPounderMesh.CustomFloats = matrixAndLightFloatsRPounder[i];
                lPoundMeshrefs[i] = capi.Render.UploadMesh(lPounderMesh);
                rPounderMeshrefs[i] = capi.Render.UploadMesh(rPounderMesh);
            }

        }

        private CustomMeshDataPartFloat createCustomFloats(int count)
        {
            CustomMeshDataPartFloat result = new CustomMeshDataPartFloat(count)
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
                float rotX = rot * axX;
                float rotZ = rot * axZ;
                UpdateLightAndTransformMatrix(matrixAndLightFloatsAxle.Values, quantityAxles, distToCamera, dev.LightRgba, rotX, rotZ, axisCenter, 0f);
                quantityAxles++;
            }

            //if ((dev.Block as BlockPulverizer).InvertPoundersOnRender) rot = -rot; - creates inverted animation. This should instead rather cause tons of resistance 
            if (bhpu.isRotationReversed()) rot = -rot;

            // Pounder-left
            int metalIndexLeft = bhpu.bepu.CapMetalIndexL;
            if (bhpu.bepu.hasLPounder && metalIndexLeft >= 0)
            {
                bool leftEmpty = bhpu.bepu.Inventory[1].Empty;

                float progress = GetProgress(bhpu.bepu.hasAxle ? rot - 0.45f + GameMath.PIHALF / 2f : 0f, 0f);
                UpdateLightAndTransformMatrix(matrixAndLightFloatsLPounder[metalIndexLeft].Values, quantityLPounders[metalIndexLeft], distToCamera, dev.LightRgba, 0f, 0f, axisCenter, Math.Max(progress / 6f + 0.0071f, leftEmpty ? -1 : 1 / 32f));

                if (progress < bhpu.prevProgressLeft && progress < 0.25f)
                {
                    if (bhpu.leftDir == 1)
                    {
                        bhpu.OnClientSideImpact(false);
                    }
                    bhpu.leftDir = -1;
                }
                else bhpu.leftDir = 1;
                bhpu.prevProgressLeft = progress;

                quantityLPounders[metalIndexLeft]++;
            }


            // Pounder-right
            int metalIndexRight = bhpu.bepu.CapMetalIndexR;
            if (bhpu.bepu.hasRPounder && metalIndexRight >= 0)
            {
                bool rightEmpty = bhpu.bepu.Inventory[0].Empty;

                float progress = GetProgress(bhpu.bepu.hasAxle ? rot - 0.45f : 0f, 0f);
                UpdateLightAndTransformMatrix(matrixAndLightFloatsRPounder[metalIndexRight].Values, quantityRPounders[metalIndexRight], distToCamera, dev.LightRgba, 0f, 0f, axisCenter, Math.Max(progress / 6f + 0.0071f, rightEmpty ? -1 : 1 / 32f));

                if (progress < bhpu.prevProgressRight && progress < 0.25f)
                {
                    if (bhpu.rightDir == 1)
                    {
                        bhpu.OnClientSideImpact(true);
                    }
                    bhpu.rightDir = -1;
                }
                else bhpu.rightDir = 1;
                bhpu.prevProgressRight = progress;

                quantityRPounders[metalIndexRight]++;
            }
        }

        /// <summary>
        /// Calculate pounder vertical motion from axle angle
        /// </summary>
        private float GetProgress(float rot, float offset)
        {
            float progress = (rot % GameMath.PIHALF) / GameMath.PIHALF;
            if (progress < 0f) progress += 1f;
            progress = 0.6355f * (float) Math.Atan(2.2f * progress - 1.2f) + 0.5f;   //give it a bit of a curve to reflect the toggle motion
            if (progress > 0.9f)
            {
                //drop it in 0.1 progress, accelerate in a parabola
                progress = 2.7f - 3 * progress;
                progress = 0.9f - progress * progress * 10f;
            }
            if (progress < 0f) progress = 0f;
            return progress;
        }

        /// <summary>
        /// The initialTransform parameter is either null, to start with the Identity matrix and apply the camera transform, or for movable+rotating sub-elements can pass in an existing matrix which represents the current positioning of the sub-element (prior to the sub-element's own rotation)
        /// </summary>
        protected void UpdateLightAndTransformMatrix(float[] values, int index, Vec3f distToCamera, Vec4f lightRgba, float rotX, float rotZ, Vec3f axis, float translate)
        {
            Mat4f.Identity(tmpMat);
            Mat4f.Translate(tmpMat, tmpMat, distToCamera.X + axis.X, distToCamera.Y + axis.Y + translate, distToCamera.Z + axis.Z);

            quat[0] = 0;
            quat[1] = 0;
            quat[2] = 0;
            quat[3] = 1;
            if (rotX != 0f) Quaterniond.RotateX(quat, quat, rotX);
            if (rotZ != 0f) Quaterniond.RotateZ(quat, quat, rotZ);

            for (int i = 0; i < quat.Length; i++) qf[i] = (float)quat[i];
            Mat4f.Mul(tmpMat, tmpMat, Mat4f.FromQuat(rotMat, qf));

            Mat4f.Translate(tmpMat, tmpMat, -axis.X, -axis.Y, -axis.Z);

            int j = index * 20;
            values[j] = lightRgba.R;
            values[++j] = lightRgba.G;
            values[++j] = lightRgba.B;
            values[++j] = lightRgba.A;

            for (int i = 0; i < 16; i++)
            {
                values[++j] = tmpMat[i];
            }
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

            // axles
            if (quantityAxles > 0)
            {
                matrixAndLightFloatsAxle.Count = quantityAxles * 20;
                updateMesh.CustomFloats = matrixAndLightFloatsAxle;
                capi.Render.UpdateMesh(toggleMeshref, updateMesh);
                capi.Render.RenderMeshInstanced(toggleMeshref, quantityAxles);
            }


            // pounders
            for (int i = 0; i < metals.Length; i++)
            {
                int qLpounder = quantityLPounders[i];
                int qRpounder = quantityRPounders[i];

                if (qLpounder > 0)
                {
                    matrixAndLightFloatsLPounder[i].Count = qLpounder * 20;
                    updateMesh.CustomFloats = matrixAndLightFloatsLPounder[i];
                    capi.Render.UpdateMesh(lPoundMeshrefs[i], updateMesh);
                    capi.Render.RenderMeshInstanced(lPoundMeshrefs[i], qLpounder);
                }

                if (qRpounder > 0)
                {
                    matrixAndLightFloatsRPounder[i].Count = qRpounder * 20;
                    updateMesh.CustomFloats = matrixAndLightFloatsRPounder[i];
                    capi.Render.UpdateMesh(rPounderMeshrefs[i], updateMesh);
                    capi.Render.RenderMeshInstanced(rPounderMeshrefs[i], qRpounder);
                }
            }
        }

        public override void Dispose()
        {
            base.Dispose();

            toggleMeshref?.Dispose();

            for (int i = 0; i < metals.Length; i++)
            {
                lPoundMeshrefs[i]?.Dispose();
                rPounderMeshrefs[i]?.Dispose();
            }
        }
    }
}

