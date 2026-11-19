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

        readonly Dictionary<CollectibleObject, MaterialMeshPool> togglePools = [];
        readonly Dictionary<(CollectibleObject, int), MaterialMeshPool> lPounderPools = [];
        readonly Dictionary<(CollectibleObject, int), MaterialMeshPool> rPounderPools = [];
        readonly Block textureSourceBlock;
        readonly Vec3f rotation;
        readonly Vec3f axisCenter = new Vec3f(0.5f, 0.5f, 0.5f);
        readonly float[] lightAndTransform = new float[InstanceDataSize];
        readonly Vec2f materialUvOffset = new();

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
            this.textureSourceBlock = textureSoureBlock;
            rotation = new Vec3f(shapeLoc.rotateX, shapeLoc.rotateY + 90F, shapeLoc.rotateZ);
            texSource = capi.Tesselator.GetTextureSource(textureSoureBlock);
        }
        
        static MechanicalMaterialProperties getPartMaterial(CollectibleObject part)
        {
            return part.GetCollectibleBehavior<CollectibleBehaviorMechanicalMaterial>(true)?.MaterialProperties;
        }

        MaterialMeshPool getTogglePool(ItemStack toggle)
        {
            if (togglePools.TryGetValue(toggle.Collectible, out MaterialMeshPool pool)) return pool;

            pool = createMaterialMeshPool(textureSourceBlock, capi.TesselatorManager.GetCachedShape(((Item)toggle.Collectible).Shape.Base), rotation, materialProperties: getPartMaterial(toggle.Collectible));
            togglePools[toggle.Collectible] = pool;
            return pool;
        }

        MaterialMeshPool getPounderPool(Dictionary<(CollectibleObject, int), MaterialMeshPool> pools, string side, ItemStack pounder, int metalIndex)
        {
            if (pools.TryGetValue((pounder.Collectible, metalIndex), out MaterialMeshPool pool)) return pool;

            string shapeCode = pounder.Collectible.Attributes?["pulverizerShapes"][side].AsString()
                               ?? throw new InvalidOperationException($"Pounder {pounder.Collectible.Code} has no pulverizerShapes {side} shape");
            Shape shape = API.Common.Shape.TryGet(capi, AssetLocation.Create(shapeCode, pounder.Collectible.Code.Domain).WithPathPrefixOnce("shapes/").WithPathAppendixOnce(".json"));

            // The caps faces resolve their metal through this texture source.
            metal = metals[metalIndex];
            pool = uploadMaterialMeshPool(tesselateMaterialPieces(textureSourceBlock, shape, rotation, materialProperties: getPartMaterial(pounder.Collectible), textureSource: this));
            pools[(pounder.Collectible, metalIndex)] = pool;
            return pool;
        }

        void addInstance(MaterialMeshPool pool, ItemStack part)
        {
            MechanicalMaterialProperties materialProperties = getPartMaterial(part.Collectible);
            foreach (var piece in pool.Pieces)
            {
                materialUvOffset.X = materialUvOffset.Y = 0;
                if (piece.MaterialTextureCode != null) materialProperties?.GetMaterialUvOffset(capi, part, piece.MaterialTextureCode, materialUvOffset);
                setMaterialInstance(piece.Floats.Values, pool.Count, lightAndTransform, materialUvOffset);
            }
            pool.Count++;
        }

        protected override void UpdateLightAndTransformMatrix(int index, Vec3f distToCamera, float rotation, IMechanicalPowerRenderable dev)
        {
            BEBehaviorMPPulverizer bhpu = dev as BEBehaviorMPPulverizer;

            float rot = bhpu.bepu.ToggleStack != null ? dev.AngleRad : 0;
            float axX = -Math.Abs(dev.AxisSign[0]);
            float axZ = -Math.Abs(dev.AxisSign[2]);

            // Axle
            if (bhpu.bepu.ToggleStack != null)
            {
                float rotX = rot * axX;
                float rotZ = rot * axZ;
                UpdateLightAndTransformMatrix(lightAndTransform, 0, distToCamera, dev.LightRgba, rotX, rotZ, axisCenter, 0f);
                addInstance(getTogglePool(bhpu.bepu.ToggleStack), bhpu.bepu.ToggleStack);
            }

            //if ((dev.Block as BlockPulverizer).InvertPoundersOnRender) rot = -rot; - creates inverted animation. This should instead rather cause tons of resistance 
            if (bhpu.IsRotationReversed()) rot = -rot;

            // Pounder-left
            int metalIndexLeft = bhpu.bepu.CapMetalIndexL;
            if (bhpu.bepu.LeftPounderStack != null && metalIndexLeft >= 0)
            {
                bool leftEmpty = bhpu.bepu.Inventory[1].Empty;

                float progress = GetProgress(bhpu.bepu.ToggleStack != null ? rot - 0.45f + GameMath.PIHALF / 2f : 0f, 0f);
                UpdateLightAndTransformMatrix(lightAndTransform, 0, distToCamera, dev.LightRgba, 0f, 0f, axisCenter, Math.Max(progress / 6f + 0.0071f, leftEmpty ? -1 : 1 / 32f));
                addInstance(getPounderPool(lPounderPools, "left", bhpu.bepu.LeftPounderStack, metalIndexLeft), bhpu.bepu.LeftPounderStack);

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
            }


            // Pounder-right
            int metalIndexRight = bhpu.bepu.CapMetalIndexR;
            if (bhpu.bepu.RightPounderStack != null && metalIndexRight >= 0)
            {
                bool rightEmpty = bhpu.bepu.Inventory[0].Empty;

                float progress = GetProgress(bhpu.bepu.ToggleStack != null ? rot - 0.45f : 0f, 0f);
                UpdateLightAndTransformMatrix(lightAndTransform, 0, distToCamera, dev.LightRgba, 0f, 0f, axisCenter, Math.Max(progress / 6f + 0.0071f, rightEmpty ? -1 : 1 / 32f));
                addInstance(getPounderPool(rPounderPools, "right", bhpu.bepu.RightPounderStack, metalIndexRight), bhpu.bepu.RightPounderStack);

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

            Mat4f.MulQuat(tmpMat, quat);

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
            foreach (MaterialMeshPool pool in togglePools.Values) pool.Count = 0;
            foreach (MaterialMeshPool pool in lPounderPools.Values) pool.Count = 0;
            foreach (MaterialMeshPool pool in rPounderPools.Values) pool.Count = 0;

            UpdateCustomFloatBuffer();

            foreach (MaterialMeshPool pool in togglePools.Values) renderMaterialMeshPool(prog, pool);
            foreach (MaterialMeshPool pool in lPounderPools.Values) renderMaterialMeshPool(prog, pool);
            foreach (MaterialMeshPool pool in rPounderPools.Values) renderMaterialMeshPool(prog, pool);
        }

        public override void Dispose()
        {
            base.Dispose();

            foreach (MaterialMeshPool pool in togglePools.Values) disposeMaterialMeshPool(pool);
            foreach (MaterialMeshPool pool in lPounderPools.Values) disposeMaterialMeshPool(pool);
            foreach (MaterialMeshPool pool in rPounderPools.Values) disposeMaterialMeshPool(pool);
        }
    }
}

