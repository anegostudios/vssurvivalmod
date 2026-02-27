using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.GameContent
{
    public class ForgeContentsRenderer : IRenderer, ITexPositionSource
    {
        protected ICoreClientAPI capi;
        protected Block block;
        protected BlockPos pos;
        protected MultiTextureMeshRef workItemMeshRef;
        protected MultiTextureMeshRef coalMeshRef;
        protected ItemStack stack;
        protected float fuelLevel;
        protected bool burning;
        protected string tmpMetal;
        protected ITexPositionSource tmpTextureSource;
        protected Matrixf ModelMat = new Matrixf();
        public double RenderOrder => 0.5;
        public int RenderRange => 24;
        public Size2i AtlasSize => capi.BlockTextureAtlas.Size; 
        public TextureAtlasPosition this[string textureCode] => tmpTextureSource[tmpMetal];
        float extraOxygenRate;
        float targetExtraOxygenRate;
        protected Vec3f rotationRad;

        public ForgeContentsRenderer(Block block, BlockPos pos, ICoreClientAPI capi, Vec3f rotationRad)
        {
            this.pos = pos;
            this.block = block;
            this.capi = capi;
            this.rotationRad = rotationRad;
        }

        public void SetContents(ItemStack stack, float fuelLevel, bool burning, bool regen, float extraOxygenRate)
        {
            this.stack = stack;
            this.fuelLevel = fuelLevel;
            this.extraOxygenRate = extraOxygenRate;

            if (fuelLevel <= 0)
            {
                coalMeshRef?.Dispose();
                coalMeshRef = null;
            }
            else
            {
                if (this.burning != burning || coalMeshRef == null)
                {
                    RegenCoalMesh(burning);
                }
            }

            this.burning = burning;
            if (regen) RegenMesh();
        }

        private void RegenCoalMesh(bool burning)
        {
            coalMeshRef?.Dispose();
            var shapeloc = AssetLocation.Create(block.Attributes["coalshapeloc"].ToString(), block.Code.Domain).WithPathAppendixOnce(".json").WithPathPrefixOnce("shapes/");
            var coalshape = capi.Assets.TryGet(shapeloc)?.ToObject<Shape>();
            if (coalshape == null)
            {
                capi.Logger.Warning("Forge contents coal shape {0} not found", shapeloc);
                return;
            }

            coalshape.Textures["coal"] = block.Textures[burning ? "fuel-ember" : "fuel-coal"].Base;

            capi.Tesselator.TesselateShape("forge coal", coalshape, out var meshdata, new ShapeTextureSource(capi, coalshape, shapeloc));

            meshdata.Rotate(new Vec3f(0.5f, 0, 0.5f), rotationRad.X, rotationRad.Y, rotationRad.Z);

            coalMeshRef = capi.Render.UploadMultiTextureMesh(meshdata);
        }

        public void RegenMesh()
        {
            workItemMeshRef?.Dispose();
            workItemMeshRef = null;
            if (stack == null) return;

            Shape shape;

            tmpMetal = stack.Collectible.LastCodePart();
            MeshData mesh=null;

            string firstCodePart = stack.Collectible.FirstCodePart();
            if (firstCodePart == "metalplate")
            {
                tmpTextureSource = capi.Tesselator.GetTextureSource(capi.World.GetBlock(new AssetLocation("platepile")));
                shape = Shape.TryGet(capi, "shapes/block/stone/forge/platepile.json");
                capi.Tesselator.TesselateShape("block-fcr", shape, out mesh, this, null, 0, 0, 0, stack.StackSize);

            }
            else if (firstCodePart == "workitem" || (firstCodePart == "ironbloom" && stack.Attributes.HasAttribute("voxels")))
            {
                MeshData workItemMesh = ItemWorkItem.GenMesh(capi, stack, ItemWorkItem.GetVoxels(stack));
                if (workItemMesh != null)
                {
                    workItemMesh.Scale(0.9f, 0.9f, 0.9f);
                    workItemMesh.Translate(0, -9f / 16f, 0);
                    workItemMesh.Rotate(new Vec3f(0.5f, 0, 0.5f), rotationRad.X, rotationRad.Y, rotationRad.Z);
                    workItemMeshRef = capi.Render.UploadMultiTextureMesh(workItemMesh);
                }
            }
            else if (firstCodePart == "ingot")
            {
                tmpTextureSource = capi.Tesselator.GetTextureSource(capi.World.GetBlock(new AssetLocation("ingotpile")));
                shape = Shape.TryGet(capi, "shapes/block/stone/forge/ingotpile.json");
                capi.Tesselator.TesselateShape("block-fcr", shape, out mesh, this, null, 0, 0, 0, stack.StackSize);
            }
            else if (stack.Collectible.Attributes?.IsTrue("forgable") == true)
            {
                if (stack.Class == EnumItemClass.Block)
                {
                    mesh = capi.TesselatorManager.GetDefaultBlockMesh(stack.Block).Clone();
                } else
                {
                    capi.Tesselator.TesselateItem(stack.Item, out mesh);
                }

                ModelTransform tf = stack.Collectible.Attributes["inForgeTransform"].AsObject<ModelTransform>();
                if (tf != null)
                {
                    tf.EnsureDefaultValues();
                    mesh.ModelTransform(tf);
                }
            }

            if (mesh != null)
            {
                mesh.Rotate(new Vec3f(0.5f, 0, 0.5f), rotationRad.X, rotationRad.Y, rotationRad.Z);
                workItemMeshRef = capi.Render.UploadMultiTextureMesh(mesh);
            }
        }



        public void OnRenderFrame(float dt, EnumRenderStage stage)
        {
            if (stack == null && fuelLevel == 0) return;

            IRenderAPI rpi = capi.Render;
            IClientWorldAccessor worldAccess = capi.World;
            Vec3d camPos = worldAccess.Player.Entity.CameraPos;

            rpi.GlDisableCullFace();

            if (stack != null && workItemMeshRef != null)
            {
                int temp = (int)stack.Collectible.GetTemperature(capi.World, stack);

                Vec4f lightrgbs = capi.World.BlockAccessor.GetLightRGBs(pos.X, pos.Y, pos.Z);
                float[] glowColor = ColorUtil.GetIncandescenceColorAsColor4f(temp);
                int extraGlow = GameMath.Clamp((temp - 550) / 2, 0, 255);

                Vec4f glowRgb = new Vec4f();
                glowRgb.R = glowColor[0];
                glowRgb.G = glowColor[1];
                glowRgb.B = glowColor[2];
                glowRgb.A = extraGlow / 255f;

                var coreMod = capi.ModLoader.GetModSystem<SurvivalCoreSystem>();
                IShaderProgram prog = coreMod.smithingWorkItemShader;
                prog.Use();
                prog.Uniform("rgbaAmbientIn", rpi.AmbientColor);
                prog.Uniform("rgbaFogIn", rpi.FogColor);
                prog.Uniform("fogMinIn", rpi.FogMin);
                prog.Uniform("dontWarpVertices", (int)0);
                prog.Uniform("addRenderFlags", (int)0);
                prog.Uniform("fogDensityIn", rpi.FogDensity);
                prog.Uniform("rgbaTint", ColorUtil.WhiteArgbVec);
                prog.Uniform("rgbaLightIn", lightrgbs);
                prog.Uniform("rgbaGlowIn", glowRgb);
                prog.Uniform("extraGlow", extraGlow);
                prog.Uniform("tempGlowMode", 0);

                prog.UniformMatrix("modelMatrix", ModelMat
                    .Identity()
                    .Translate(pos.X - camPos.X, pos.Y - camPos.Y + 11 / 16f + (fuelLevel-1) / 16f / 4f, pos.Z - camPos.Z)
                    .Values
                );
                prog.UniformMatrix("viewMatrix", rpi.CameraMatrixOriginf);
                prog.UniformMatrix("projectionMatrix", rpi.CurrentProjectionMatrix);

                rpi.RenderMultiTextureMesh(workItemMeshRef, "tex");

                prog.Stop();
            }

            if (fuelLevel > 0)
            {
                IStandardShaderProgram prog = rpi.StandardShader;
                prog.Use();
                prog.RgbaAmbientIn = rpi.AmbientColor;
                prog.RgbaFogIn = rpi.FogColor;
                prog.FogMinIn = rpi.FogMin;
                prog.FogDensityIn = rpi.FogDensity;
                prog.RgbaTint = ColorUtil.WhiteArgbVec;
                prog.DontWarpVertices = 0;
                prog.AddRenderFlags = 0;
                prog.ExtraGodray = 0;
                prog.OverlayOpacity = 0;

                Vec4f lightrgbs = capi.World.BlockAccessor.GetLightRGBs(pos.X, pos.Y, pos.Z);

                long seed = capi.World.ElapsedMilliseconds/10 + pos.GetHashCode();
                float flicker = (float)(Math.Sin(seed / 10.0) * 0.2f + Math.Sin(seed / 20.0) * 0.2f + Math.Cos(seed / 110.0) * 0.6f + Math.Sin(seed / 50.0) + 1) / 4f;

                if (burning)
                {
                    targetExtraOxygenRate += (extraOxygenRate - targetExtraOxygenRate) * dt * 10;

                    float[] glowColor = ColorUtil.GetIncandescenceColorAsColor4f((int)(1100 * (1 + extraOxygenRate)));

                    glowColor[0] *= 1f - flicker * 0.15f;
                    glowColor[1] *= 1f - flicker * 0.15f;
                    glowColor[2] *= 1f - flicker * 0.15f;

                    prog.RgbaGlowIn = new Vec4f(glowColor[0], glowColor[1], glowColor[2], 1);
                } else
                {
                    prog.RgbaGlowIn = new Vec4f(0,0,0,0);
                }

                prog.NormalShaded = 0;
                prog.RgbaLightIn = new Vec4f(1, 1, 1, 1);
                prog.TempGlowMode = 1;

                int glow = (int)(255 * targetExtraOxygenRate);
                
                prog.ExtraGlow = burning ? glow : 0;
                prog.ModelMatrix = ModelMat.Identity().Translate(pos.X - camPos.X, pos.Y - camPos.Y + (fuelLevel - 1) / 16f / 4f, pos.Z - camPos.Z).Values;
                prog.ViewMatrix = rpi.CameraMatrixOriginf;
                prog.ProjectionMatrix = rpi.CurrentProjectionMatrix;

                rpi.RenderMultiTextureMesh(coalMeshRef, "tex");

                prog.Stop();
            }            
        }



        public void Dispose()
        {
            capi.Event.UnregisterRenderer(this, EnumRenderStage.Opaque);
            coalMeshRef?.Dispose();
            workItemMeshRef?.Dispose();
        }
    }
}
