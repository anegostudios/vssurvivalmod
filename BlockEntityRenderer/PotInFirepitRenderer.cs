using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.GameContent
{
    public class PotInFirepitRenderer : IInFirepitRenderer
    {
        public double RenderOrder => 0.5;
        public int RenderRange => 200;

        ICoreClientAPI capi;
        MultiTextureMeshRef potWithFoodRef;
        MultiTextureMeshRef potRef;
        MultiTextureMeshRef lidRef;
        BlockPos pos;
        float temp;

        Vec3d potPos;
        Cuboidd aabb;

        ILoadedSound cookingSound;

        bool isInOutputSlot;
        Matrixf ModelMat = new Matrixf();

        public PotInFirepitRenderer(ICoreClientAPI capi, ItemStack stack, BlockPos pos, bool isInOutputSlot)
        {
            this.capi = capi;
            this.pos = pos;
            this.isInOutputSlot = isInOutputSlot;

            potPos = new Vec3d(pos.X + 0.5d, pos.Y + 0.5d, pos.Z + 0.5d);  // Firepit center
            aabb = new Cuboidd(pos.X, pos.Y, pos.Z, pos.X + 1, pos.Y + 1, pos.Z + 1);

            BlockCookedContainer potBlock = capi.World.GetBlock(stack.Collectible.CodeWithVariant("type", "cooked")) as BlockCookedContainer;

            if (isInOutputSlot)
            {
                MealMeshCache meshcache = capi.ModLoader.GetModSystem<MealMeshCache>();

                potWithFoodRef = meshcache.GetOrCreateMealInContainerMeshRef(potBlock, potBlock.GetCookingRecipe(capi.World, stack), potBlock.GetNonEmptyContents(capi.World, stack), new Vec3f(0, 2.5f/16f, 0));
            }
            else
            {
                string basePath = "shapes/block/clay/pot-";    // hard-coding for dirty pot seems reasonable here, as the shape paths are already hard-coded
                capi.Tesselator.TesselateShape(potBlock, Shape.TryGet(capi, basePath + "opened-empty.json"), out MeshData potMesh);
                potRef = capi.Render.UploadMultiTextureMesh(potMesh);

                capi.Tesselator.TesselateShape(potBlock, Shape.TryGet(capi, basePath + "part-lid.json"), out MeshData lidMesh);
                lidRef = capi.Render.UploadMultiTextureMesh(lidMesh);
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
            if (capi?.Render == null)
                return;


            IRenderAPI rpi = capi.Render;
            Vec3d camPos = capi.World.Player.Entity.CameraPos;

            // Checking the distance
            double distanceSq = potPos.SquareDistanceTo(camPos);
            if (distanceSq > RenderRange * RenderRange)
                return;  // Skip if outside RenderRange

            // Frustum-culling according to AABB
            FrustumCulling culler = rpi.DefaultFrustumCuller;

            if (!IsAABBInFrustum(culler, aabb))
                return; // Skip if outside frustum

            // Rest of rendering
            rpi.GlDisableCullFace();
            rpi.GlToggleBlend(true);

            IStandardShaderProgram prog = rpi.PreparedStandardShader(pos.X, pos.Y, pos.Z);

            prog.DontWarpVertices = 0;
            prog.AddRenderFlags = 0;
            prog.RgbaAmbientIn = rpi.AmbientColor;
            prog.RgbaFogIn = rpi.FogColor;
            prog.FogMinIn = rpi.FogMin;
            prog.FogDensityIn = rpi.FogDensity;
            prog.RgbaTint = ColorUtil.WhiteArgbVec;
            prog.NormalShaded = 1;
            prog.ExtraGodray = 0;
            prog.SsaoAttn = 0;
            prog.AlphaTest = 0.05f;
            prog.OverlayOpacity = 0;


            prog.ModelMatrix = ModelMat
                    .Identity()
                    .Translate(pos.X - camPos.X + 0.001f, pos.Y - camPos.Y, pos.Z - camPos.Z - 0.001f)
                    .Translate(0f, 1 / 16f, 0f)
                    .Values;

            prog.ViewMatrix = rpi.CameraMatrixOriginf;
            prog.ProjectionMatrix = rpi.CurrentProjectionMatrix;

            rpi.RenderMultiTextureMesh(potRef == null ? potWithFoodRef : potRef, "tex");

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
                        .Values;
                prog.ViewMatrix = rpi.CameraMatrixOriginf;
                prog.ProjectionMatrix = rpi.CurrentProjectionMatrix;


                rpi.RenderMultiTextureMesh(lidRef, "tex");
            }

            prog.Stop();
        }

        // Auxiliary method for the AABB vs Frustum test
        private bool IsAABBInFrustum(FrustumCulling culler, Cuboidd aabb)
        {
            double centerX = (aabb.MinX + aabb.MaxX) * 0.5;
            double centerY = (aabb.MinY + aabb.MaxY) * 0.5;
            double centerZ = (aabb.MinZ + aabb.MaxZ) * 0.5;
            double dx = aabb.MaxX - aabb.MinX;
            double dy = aabb.MaxY - aabb.MinY;
            double dz = aabb.MaxZ - aabb.MinZ;

            Sphere sphere = new Sphere(
                (float)centerX,   
                (float)centerY,
                (float)centerZ,
                (float)dx,
                (float)dy,
                (float)dz
            );

            return culler.InFrustum(sphere);
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
                        Range = 10f,
                        ReferenceDistance = 3f,
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
