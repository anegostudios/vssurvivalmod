using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class PotInFirepitRenderer : IInFirepitRenderer
    {
        public double RenderOrder => 0.5;
        public int RenderRange => 20;

        ICoreClientAPI capi;
        ItemStack stack;
        MeshRef potRef;
        MeshRef lidRef;
        BlockPos pos;
        float temp;

        ILoadedSound cookingSound;

        bool isInOutputSlot;
        Matrixf ModelMat = new Matrixf();

        public PotInFirepitRenderer(ICoreClientAPI capi, ItemStack stack, BlockPos pos, bool isInOutputSlot)
        {
            this.capi = capi;
            this.stack = stack;
            this.pos = pos;
            this.isInOutputSlot = isInOutputSlot;

            BlockCookedContainer potBlock = capi.World.GetBlock(new AssetLocation("claypot-cooked")) as BlockCookedContainer;

            if (isInOutputSlot)
            {
                MealMeshCache meshcache = capi.ModLoader.GetModSystem<MealMeshCache>();

                MeshData potMesh = meshcache.CreateMealMesh(potBlock.Shape, potBlock.GetCookingRecipe(capi.World, stack), potBlock.GetContents(capi.World, stack), new Vec3f(0, 2.5f/16f, 0)); 
                potRef = capi.Render.UploadMesh(potMesh);
            }
            else
            {
                MeshData potMesh;
                capi.Tesselator.TesselateShape(potBlock, capi.Assets.TryGet("shapes/block/clay/pot-opened-empty.json").ToObject<Shape>(), out potMesh);
                potRef = capi.Render.UploadMesh(potMesh);

                MeshData lidMesh;
                capi.Tesselator.TesselateShape(potBlock, capi.Assets.TryGet("shapes/block/clay/pot-part-lid.json").ToObject<Shape>(), out lidMesh);
                lidRef = capi.Render.UploadMesh(lidMesh);
            }
        }

        public void Dispose()
        {
            potRef?.Dispose();
            lidRef?.Dispose();

            cookingSound?.Stop();
            cookingSound?.Dispose();
        }

        public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
        {
            IRenderAPI rpi = capi.Render;
            Vec3d camPos = capi.World.Player.Entity.CameraPos;

            rpi.GlDisableCullFace();
            rpi.GlToggleBlend(true);

            IStandardShaderProgram prog = rpi.PreparedStandardShader(pos.X, pos.Y, pos.Z);
            capi.Render.BindTexture2d(capi.BlockTextureAtlas.AtlasTextureIds[0]);

            prog.WaterWave = 0;
            prog.ModelMatrix = ModelMat
                .Identity()
                .Translate(pos.X - camPos.X + 0.001f, pos.Y - camPos.Y, pos.Z - camPos.Z - 0.001f)
                .Translate(0f, 1 / 16f, 0f)
                .Values
            ;
            prog.ViewMatrix = rpi.CameraMatrixOriginf;
            prog.ProjectionMatrix = rpi.CurrentProjectionMatrix;

            rpi.RenderMesh(potRef);

            if (!isInOutputSlot)
            {
                float origx = GameMath.Sin(capi.World.ElapsedMilliseconds / 300f) * 5 / 16f;
                float origz = GameMath.Cos(capi.World.ElapsedMilliseconds / 300f) * 5 / 16f;

                float cookIntensity = GameMath.Clamp((temp - 50) / 50, 0, 1);

                prog.ModelMatrix = ModelMat
                    .Identity()
                    .Translate(pos.X - camPos.X, pos.Y - camPos.Y, pos.Z - camPos.Z)
                    .Translate(0, 6.5f / 16f, 0)
                    .Translate(-origx, 0, -origz)
                    .RotateX(cookIntensity * GameMath.Sin(capi.World.ElapsedMilliseconds / 50f) / 60)
                    .RotateZ(cookIntensity * GameMath.Sin(capi.World.ElapsedMilliseconds / 50f) / 60)
                    .Translate(origx, 0, origz)
                    .Values
                ;
                prog.ViewMatrix = rpi.CameraMatrixOriginf;
                prog.ProjectionMatrix = rpi.CurrentProjectionMatrix;


                rpi.RenderMesh(lidRef);
            }

            prog.Stop();
        }

        public void OnUpdate(float temperature)
        {
            temp = temperature;

            float soundIntensity = GameMath.Clamp((temp - 50) / 50, 0, 1);
            SetCookingSoundVolume(isInOutputSlot ? 0 : soundIntensity);
        }

        public void OnCookingComplete()
        {
            isInOutputSlot = true;
        }


        public void SetCookingSoundVolume(float volume)
        {
            if (volume > 0)
            {

                if (cookingSound == null)
                {
                    cookingSound = capi.World.LoadSound(new SoundParams()
                    {
                        Location = new AssetLocation("sounds/effect/cooking.ogg"),
                        ShouldLoop = true,
                        Position = pos.ToVec3f().Add(0.5f, 0.25f, 0.5f),
                        DisposeOnFinish = false,
                        Volume = volume
                    });
                    cookingSound.Start();
                }
                else
                {
                    cookingSound.SetVolume(volume);
                }

            }
            else
            {
                if (cookingSound != null)
                {
                    cookingSound.Stop();
                    cookingSound.Dispose();
                    cookingSound = null;
                }

            }

        }




    }
}
