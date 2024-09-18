using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class EntityBehaviorHideWaterSurface : EntityBehavior, IRenderer, ITexPositionSource
    {
        MultiTextureMeshRef meshref;
        ICoreClientAPI capi;
        string hideWaterElement;

        public EntityBehaviorHideWaterSurface(Entity entity) : base(entity) { }

        public override void Initialize(EntityProperties properties, JsonObject attributes)
        {
            capi = entity.World.Api as ICoreClientAPI;
            capi.Event.RegisterRenderer(this, EnumRenderStage.OIT, "re-ebhhws");

            hideWaterElement = attributes["hideWaterElement"].AsString();
        }


        public override void OnTesselated()
        {
            var compositeShape = entity.Properties.Client.Shape;
            Shape entityShape = entity.Properties.Client.LoadedShapeForEntity;

            try
            {
                TesselationMetaData meta = new TesselationMetaData()
                {
                    QuantityElements = compositeShape.QuantityElements,
                    SelectiveElements = new string[] { hideWaterElement },
                    TexSource = this,
                    WithJointIds = true,
                    WithDamageEffect = true,
                    TypeForLogging = "entity",
                    Rotation = new Vec3f(compositeShape.rotateX, compositeShape.rotateY, compositeShape.rotateZ)
                };

                capi.Tesselator.TesselateShape(meta, entityShape, out var meshdata);

                meshdata.Translate(compositeShape.offsetX, compositeShape.offsetY, compositeShape.offsetZ);

                meshref?.Dispose();
                this.meshref = capi.Render.UploadMultiTextureMesh(meshdata);
            }
            catch (Exception e)
            {
                capi.World.Logger.Fatal("Failed tesselating entity {0} with id {1}. Entity will probably be invisible!.", entity.Code, entity.EntityId);
                capi.World.Logger.Fatal(e);
                return;
            }
                
        }

        public double RenderOrder => 0.36; // Liquid render is at 0.37
        public int RenderRange => 99;

        Size2i dummysize = new Size2i(2048,2048);
        TextureAtlasPosition dummyPos = new TextureAtlasPosition() { x1 = 0, y1 = 0, x2 = 1, y2 = 1 };
        public Size2i AtlasSize => dummysize;
        public TextureAtlasPosition this[string textureCode] => dummyPos;

        public void Dispose()
        {
            meshref?.Dispose();
        }

        public override void OnEntityDespawn(EntityDespawnData despawn)
        {
            base.OnEntityDespawn(despawn);

            meshref?.Dispose();
        }

        protected float[] tmpMvMat = Mat4f.Create();

        public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
        {
            if (meshref == null) return;
            var esr = entity.Properties.Client.Renderer as EntityShapeRenderer;
            if (esr == null) return;

            capi.Render.GLDepthMask(true);

            // We only render into the depth texture
            // We can abuse the shadow map shader for this
            var prog = capi.Shader.GetProgram((int)EnumShaderProgram.Shadowmapgeneric);

            prog.Use();
            
            var modelMat = esr.ModelMat;

            Mat4f.Mul(tmpMvMat, capi.Render.CurrentProjectionMatrix, capi.Render.CameraMatrixOriginf);
            Mat4f.Mul(tmpMvMat, tmpMvMat, modelMat);
            prog.BindTexture2D("tex2d", 0, 0);
            prog.UniformMatrix("mvpMatrix", tmpMvMat);
            prog.Uniform("origin", new Vec3f(0,0,0));
            
            capi.Render.RenderMultiTextureMesh(meshref, "tex2d");

            prog.Stop();

            capi.Render.GLDepthMask(false);
            capi.Render.GLEnableDepthTest();
        }

        public override string PropertyName() => "hidewatersurface";
    }

}
