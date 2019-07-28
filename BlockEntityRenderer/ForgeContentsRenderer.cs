using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace Vintagestory.GameContent
{
    public class ForgeContentsRenderer : IRenderer, ITexPositionSource
    {
        private ICoreClientAPI capi;
        private BlockPos pos;

        MeshRef workItemMeshRef;

        MeshRef emberQuadRef;
        MeshRef coalQuadRef;


        ItemStack stack;
        float fuelLevel;
        bool burning;
        MetalProperty metals;

        TextureAtlasPosition coaltexpos;
        TextureAtlasPosition embertexpos;
        TextureAtlasPosition workItemTexPos;


        string tmpMetal;
        ITexPositionSource tmpTextureSource;

        Matrixf ModelMat = new Matrixf();

        public double RenderOrder
        {
            get { return 0.5; }
        }

        public int RenderRange
        {
            get { return 24; }
        }

        public int AtlasSize
        {
            get { return capi.BlockTextureAtlas.Size; }
        }

        public TextureAtlasPosition this[string textureCode]
        {
            get { return tmpTextureSource[tmpMetal]; }
        }




        public ForgeContentsRenderer(BlockPos pos, ICoreClientAPI capi)
        {
            this.pos = pos;
            this.capi = capi;

            metals = capi.Assets.TryGet("worldproperties/block/metal.json").ToObject<MetalProperty>();

            Block block = capi.World.GetBlock(new AssetLocation("forge"));

            coaltexpos = capi.BlockTextureAtlas.GetPosition(block, "coal");
            embertexpos = capi.BlockTextureAtlas.GetPosition(block, "ember");

            MeshData emberMesh = QuadMeshUtil.GetCustomQuadHorizontal(3 / 16f, 0, 3 / 16f, 10 / 16f, 10 / 16f, 255, 255, 255, 255);

            for (int i = 0; i < emberMesh.Uv.Length; i+=2)
            {
                emberMesh.Uv[i + 0] = embertexpos.x1 + emberMesh.Uv[i + 0] * 32f / AtlasSize;
                emberMesh.Uv[i + 1] = embertexpos.y1 + emberMesh.Uv[i + 1] * 32f / AtlasSize;
            }
            emberMesh.Flags = new int[] { 128, 128, 128, 128 };

            MeshData coalMesh = QuadMeshUtil.GetCustomQuadHorizontal(3 / 16f, 0, 3 / 16f, 10 / 16f, 10 / 16f, 255, 255, 255, 255);

            for (int i = 0; i < coalMesh.Uv.Length; i += 2)
            {
                coalMesh.Uv[i + 0] = coaltexpos.x1 + coalMesh.Uv[i + 0] * 32f / AtlasSize; ;
                coalMesh.Uv[i + 1] = coaltexpos.y1 + coalMesh.Uv[i + 1] * 32f / AtlasSize; ;
            }


            emberQuadRef = capi.Render.UploadMesh(emberMesh);
            coalQuadRef = capi.Render.UploadMesh(coalMesh);
        }

        public void SetContents(ItemStack stack, float fuelLevel, bool burning, bool regen)
        {
            ItemStack beforeStack = stack;

            this.stack = stack;
            this.fuelLevel = fuelLevel;
            this.burning = burning;

            if (regen) RegenMesh();
        }


        void RegenMesh()
        {
            workItemMeshRef?.Dispose();
            workItemMeshRef = null;
            if (stack == null) return;

            Shape shape;

            tmpMetal = stack.Collectible.LastCodePart();

            if (stack.Collectible.FirstCodePart() == "metalplate")
            {
                tmpTextureSource = capi.Tesselator.GetTexSource(capi.World.GetBlock(new AssetLocation("platepile")));
                shape = capi.Assets.TryGet("shapes/block/stone/forge/platepile.json").ToObject<Shape>();
            } else
            {
                tmpTextureSource = capi.Tesselator.GetTexSource(capi.World.GetBlock(new AssetLocation("ingotpile")));
                shape = capi.Assets.TryGet("shapes/block/stone/forge/ingotpile.json").ToObject<Shape>();
            }

            workItemTexPos = tmpTextureSource[tmpMetal];


            MeshData mesh;
            capi.Tesselator.TesselateShape("block-fcr", shape, out mesh, this, null, 0, 0, stack.StackSize);
            mesh.Rgba2 = null;

            workItemMeshRef = capi.Render.UploadMesh(mesh);
        }



        public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
        {
            if (stack == null && fuelLevel == 0) return;

            IRenderAPI rpi = capi.Render;
            IClientWorldAccessor worldAccess = capi.World;
            EntityPos plrPos = worldAccess.Player.Entity.Pos;
            Vec3d camPos = worldAccess.Player.Entity.CameraPos;

            rpi.GlDisableCullFace();
            IStandardShaderProgram prog = rpi.StandardShader;
            prog.Use();
            prog.RgbaAmbientIn = rpi.AmbientColor;
            prog.RgbaFogIn = rpi.FogColor;
            prog.FogMinIn = rpi.FogMin;
            prog.FogDensityIn = rpi.FogDensity;
            prog.RgbaTint = ColorUtil.WhiteArgbVec;
            prog.DontWarpVertices = 0;
            prog.AddRenderFlags = 0;
            //rpi.GlMatrixModeModelView();


            if (stack != null && workItemMeshRef != null)
            {
                int temp = (int)stack.Collectible.GetTemperature(capi.World, stack);

                prog.ExtraGlow = GameMath.Clamp((temp - 700) / 2, 0, 255);

                Vec4f lightrgbs = capi.World.BlockAccessor.GetLightRGBs(pos.X, pos.Y, pos.Z);
                
                float[] glowColor = ColorUtil.GetIncandescenceColorAsColor4f(temp);
                lightrgbs[0] += 2 * glowColor[0];
                lightrgbs[1] += 2 * glowColor[1];
                lightrgbs[2] += 2 * glowColor[2];

                prog.RgbaLightIn = lightrgbs;
                prog.RgbaBlockIn = ColorUtil.WhiteArgbVec;

                // The work item
                rpi.BindTexture2d(workItemTexPos.atlasTextureId);

                prog.ModelMatrix = ModelMat.Identity().Translate(pos.X - camPos.X, pos.Y - camPos.Y + 10 / 16f + fuelLevel * 0.65f, pos.Z - camPos.Z).Values;
                prog.ViewMatrix = rpi.CameraMatrixOriginf;
                prog.ProjectionMatrix = rpi.CurrentProjectionMatrix;
                
                rpi.RenderMesh(workItemMeshRef);

                
            }

            if (fuelLevel > 0)
            {
                Vec4f lightrgbs = capi.World.BlockAccessor.GetLightRGBs(pos.X, pos.Y, pos.Z);

                prog.RgbaLightIn = lightrgbs;
                prog.RgbaBlockIn = ColorUtil.WhiteArgbVec;
                prog.ExtraGlow = 0;

                // The coal or embers
                rpi.BindTexture2d(burning ? embertexpos.atlasTextureId : coaltexpos.atlasTextureId);

                prog.ModelMatrix = ModelMat.Identity().Translate(pos.X - camPos.X, pos.Y - camPos.Y + 10 / 16f + fuelLevel * 0.65f, pos.Z - camPos.Z).Values;
                prog.ViewMatrix = rpi.CameraMatrixOriginf;
                prog.ProjectionMatrix = rpi.CurrentProjectionMatrix;

                rpi.RenderMesh(burning ? emberQuadRef : coalQuadRef);
                
            }


            prog.Stop();
        }

        public void Unregister()
        {
            capi.Event.UnregisterRenderer(this, EnumRenderStage.Opaque);
        }

        // Called by UnregisterRenderer
        public void Dispose()
        {
            emberQuadRef?.Dispose();
            coalQuadRef?.Dispose();
            workItemMeshRef?.Dispose();
        }
    }
}
