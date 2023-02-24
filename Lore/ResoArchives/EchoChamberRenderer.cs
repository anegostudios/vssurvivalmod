using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class EchoChamberRenderer : EntityRenderer, ITexPositionSource
    {
        protected int renderRange = 999;
        protected MeshRef meshRefOpaque;
        protected Vec4f color = new Vec4f(1, 1, 1, 1);
        public Vec3f OriginPos = new Vec3f();
        public float[] ModelMat = Mat4f.Create();
        protected float[] tmpMvMat = Mat4f.Create();
        protected EntityAgent eagent;
        protected bool shapeFresh;
        Vec4f lightrgbs;

        LoadedTexture echoTexture;
        Size2i echoTextureSize = new Size2i(144,144);
        TextureAtlasPosition texPos = new TextureAtlasPosition() { x1=0, y1=0, y2 = 20, x2 = 20 };

        public Size2i AtlasSize { get { return echoTextureSize;/*capi.EntityTextureAtlas.Size; */} }
        protected TextureAtlasPosition skinTexPos;
        public virtual TextureAtlasPosition this[string textureCode]
        {
            get
            {
                return texPos;
            }
        }


        public EchoChamberRenderer(Entity entity, ICoreClientAPI api) : base(entity, api)
        {
            eagent = entity as EntityAgent;
            api.Event.ReloadShapes += MarkShapeModified;
        }

        public virtual void MarkShapeModified()
        {
            shapeFresh = false;
        }


        bool loaded = false;
        public override void OnEntityLoaded()
        {
            loaded = true;
            MarkShapeModified();
        }


        public virtual void TesselateShape()
        {
            if (!loaded)
            {
                return;
            }

            shapeFresh = true;
            CompositeShape compositeShape = entity.Properties.Client.Shape;

            Shape entityShape = entity.Properties.Client.LoadedShapeForEntity;

            if (entityShape == null)
            {
                return;
            }

            entity.OnTesselation(ref entityShape, compositeShape.Base.ToString());


            TyronThreadPool.QueueTask(() =>
            {
                MeshData meshdata;

                try
                {
                    TesselationMetaData meta = new TesselationMetaData()
                    {
                        QuantityElements = compositeShape.QuantityElements,
                        SelectiveElements = compositeShape.SelectiveElements,
                        TexSource = this,
                        WithJointIds = false,
                        WithDamageEffect = true,
                        TypeForLogging = "entity",
                        Rotation = new Vec3f(compositeShape.rotateX, compositeShape.rotateY, compositeShape.rotateZ)
                    };

                    capi.Tesselator.TesselateShape(meta, entityShape, out meshdata);

                    meshdata.Translate(compositeShape.offsetX, compositeShape.offsetY, compositeShape.offsetZ);

                }
                catch (Exception e)
                {
                    capi.World.Logger.Fatal("Failed tesselating entity {0} with id {1}. Entity will probably be invisible!. The teselator threw {2}", entity.Code, entity.EntityId, e);
                    return;
                }

                MeshData opaqueMesh = meshdata.Clone().Clear();
                opaqueMesh.AddMeshData(meshdata, EnumChunkRenderPass.Opaque);

                capi.Event.EnqueueMainThreadTask(() =>
                {
                    if (meshRefOpaque != null)
                    {
                        meshRefOpaque.Dispose();
                        meshRefOpaque = null;
                    }

                    if (capi.IsShuttingDown)
                    {
                        return;
                    }

                    if (opaqueMesh.VerticesCount > 0)
                    {
                        meshRefOpaque = capi.Render.UploadMesh(opaqueMesh);
                    }

                    echoTexture?.Dispose();
                    echoTexture = new LoadedTexture(capi);
                    byte[] assetData = capi.Assets.TryGet(new AssetLocation("textures/entity/echochamber.png"))?.Data;
                    if (assetData == null) return;
                    BitmapRef bmp = capi.Render.BitmapCreateFromPng(assetData);
                    capi.Render.LoadTexture(bmp, ref echoTexture, false, 2, true);
                    bmp.Dispose();

                }, "uploadentitymesh");

                capi.TesselatorManager.ThreadDispose();
            });
        }




        public override void BeforeRender(float dt)
        {
            if (!shapeFresh)
            {
                TesselateShape();
            }

            lightrgbs = capi.World.BlockAccessor.GetLightRGBs((int)(entity.Pos.X + entity.SelectionBox.X1 - entity.OriginSelectionBox.X1), (int)entity.Pos.Y, (int)(entity.Pos.Z + entity.SelectionBox.Z1 - entity.OriginSelectionBox.Z1));

            if (entity.SelectionBox.Y2 > 1)
            {
                Vec4f lightrgbs2 = capi.World.BlockAccessor.GetLightRGBs((int)(entity.Pos.X + entity.SelectionBox.X1 - entity.OriginSelectionBox.X1), (int)entity.Pos.Y + 1, (int)(entity.Pos.Z + entity.SelectionBox.Z1 - entity.OriginSelectionBox.Z1));
                if (lightrgbs2.W > lightrgbs.W) lightrgbs = lightrgbs2;
            }
        }


        public override void DoRender3DOpaqueBatched(float dt, bool isShadowPass)
        {
        }


        public override void DoRender3DOpaque(float dt, bool isShadowPass)
        {
            if (meshRefOpaque == null) return;
            if (isShadowPass) return;

            loadModelMatrix(entity);
            Vec3d camPos = capi.World.Player.Entity.CameraPos;
            OriginPos.Set((float)(entity.Pos.X - camPos.X), (float)(entity.Pos.Y - camPos.Y), (float)(entity.Pos.Z - camPos.Z));

            capi.Render.GlDisableCullFace();
            capi.Render.GlMatrixModeModelView();
            capi.Render.GlPushMatrix();
            capi.Render.GlLoadMatrix(capi.Render.CameraMatrixOrigin);
            capi.Render.GlToggleBlend(true, EnumBlendMode.Standard);

            var prog = capi.Shader.GetProgram((int)EnumShaderProgram.Entityanimated);
            prog.Use();
            prog.Uniform("rgbaAmbientIn", capi.Render.AmbientColor);
            prog.Uniform("rgbaFogIn", capi.Render.FogColor);
            prog.Uniform("fogMinIn", capi.Render.FogMin);
            prog.Uniform("fogDensityIn", capi.Render.FogDensity);
            prog.UniformMatrix("projectionMatrix", capi.Render.CurrentProjectionMatrix);
            prog.BindTexture2D("entityTex", echoTexture.TextureId/*capi.EntityTextureAtlas.AtlasTextureIds[0]*/, 0);
            prog.Uniform("alphaTest", 0.5f);
            prog.Uniform("lightPosition", capi.Render.ShaderUniforms.LightPosition3D);


            prog.Uniform("rgbaLightIn", lightrgbs);
            prog.Uniform("extraGlow", entity.Properties.Client.GlowLevel);
            prog.UniformMatrix("modelMatrix", ModelMat);
            prog.UniformMatrix("viewMatrix", capi.Render.CurrentModelviewMatrix);
            prog.Uniform("addRenderFlags", 0);
            prog.Uniform("windWaveIntensity", (float)0);
            prog.Uniform("skipRenderJointId", -1);
            prog.Uniform("skipRenderJointId2", -1);
            prog.Uniform("entityId", (int)entity.EntityId);
            prog.Uniform("glitchFlicker", 0);
            prog.Uniform("frostAlpha", 0f);
            prog.Uniform("waterWaveCounter", 0f);

            color[0] = (entity.RenderColor >> 16 & 0xff) / 255f;
            color[1] = ((entity.RenderColor >> 8) & 0xff) / 255f;
            color[2] = ((entity.RenderColor >> 0) & 0xff) / 255f;
            color[3] = ((entity.RenderColor >> 24) & 0xff) / 255f;

            prog.Uniform("renderColor", color);

            capi.Render.GlPopMatrix();

            /*double stab = entity.WatchedAttributes.GetDouble("temporalStability", 1);
            double plrStab = capi.World.Player.Entity.WatchedAttributes.GetDouble("temporalStability", 1);
            double stabMin = Math.Min(stab, plrStab);

            float strength = (float)(glitchAffected ? Math.Max(0, 1 - 1 / 0.4f * stabMin) : 0);
            prog.Uniform("glitchEffectStrength", strength);*/



            /*prog.UniformMatrices4x3(
                "elementTransforms", 
                GlobalConstants.MaxAnimatedElements, 
                entity.AnimManager.Animator.Matrices4x3
            );*/

            if (meshRefOpaque != null)
            {
                capi.Render.RenderMesh(meshRefOpaque);
            }

            prog.Stop();

        }


        public void loadModelMatrix(Entity entity)
        {
            EntityPlayer entityPlayer = capi.World.Player.Entity;
            Mat4f.Identity(ModelMat);
            Mat4f.Translate(ModelMat, ModelMat, (float)(entity.Pos.X - entityPlayer.CameraPos.X), (float)(entity.Pos.Y - entityPlayer.CameraPos.Y), (float)(entity.Pos.Z - entityPlayer.CameraPos.Z));
            float scale = entity.Properties.Client.Size;
            Mat4f.Scale(ModelMat, ModelMat, new float[] { scale, scale, scale });
            Mat4f.Translate(ModelMat, ModelMat, -0.5f, 0, -0.5f);
        }



        public override void Dispose()
        {
            if (meshRefOpaque != null)
            {
                meshRefOpaque.Dispose();
                meshRefOpaque = null;
            }

            capi.Event.ReloadShapes -= MarkShapeModified;
        }


    }
}
