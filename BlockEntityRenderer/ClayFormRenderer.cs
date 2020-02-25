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
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class ClayFormRenderer : IRenderer
    {
        private ICoreClientAPI api;
        private BlockPos pos;

        MeshRef workItemMeshRef;
        MeshRef recipeOutlineMeshRef;

        ItemStack workItem;
        int texId;
        Matrixf ModelMat = new Matrixf();

        Vec4f outLineColorMul = new Vec4f(1, 1, 1, 1);

        public ClayFormRenderer(BlockPos pos, ICoreClientAPI capi)
        {
            this.pos = pos;
            this.api = capi;
        }

        public double RenderOrder
        {
            get { return 0.5; }
        }

        public int RenderRange
        {
            get { return 24; }
        }

        public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
        {
            if (workItemMeshRef == null) return;
            if (stage == EnumRenderStage.AfterFinalComposition)
            {
                RenderRecipeOutLine();
                return;
            }

            IRenderAPI rpi = api.Render;
            IClientWorldAccessor worldAccess = api.World;
            Vec3d camPos = worldAccess.Player.Entity.CameraPos;

            rpi.GlDisableCullFace();
            IStandardShaderProgram prog = rpi.PreparedStandardShader(pos.X, pos.Y, pos.Z);
            rpi.BindTexture2d(texId);

            prog.ModelMatrix = ModelMat.Identity().Translate(pos.X - camPos.X, pos.Y - camPos.Y, pos.Z - camPos.Z).Values;
            prog.ViewMatrix = rpi.CameraMatrixOriginf;
            prog.ProjectionMatrix = rpi.CurrentProjectionMatrix;

            rpi.RenderMesh(workItemMeshRef);
            
            prog.Stop();
        }



        private void RenderRecipeOutLine()
        {
            if (recipeOutlineMeshRef == null || api.HideGuis) return;
            bool isClay = api.World.Player?.InventoryManager?.ActiveHotbarSlot?.Itemstack?.Collectible is ItemClay;
            if (!isClay) return;

            IRenderAPI rpi = api.Render;
            IClientWorldAccessor worldAccess = api.World;
            EntityPos plrPos = worldAccess.Player.Entity.Pos;
            Vec3d camPos = worldAccess.Player.Entity.CameraPos;

            ModelMat.Set(rpi.CameraMatrixOriginf).Translate(pos.X - camPos.X, pos.Y - camPos.Y, pos.Z - camPos.Z);
            outLineColorMul.A = 1 - GameMath.Clamp((float)Math.Sqrt(plrPos.SquareDistanceTo(pos.X, pos.Y, pos.Z)) / 5 - 1f, 0, 1);

            rpi.GLEnableDepthTest();
            rpi.GlToggleBlend(true);
            IShaderProgram prog = rpi.GetEngineShader(EnumShaderProgram.Wireframe);
            prog.Use();
            prog.UniformMatrix("projectionMatrix", rpi.CurrentProjectionMatrix);
            prog.UniformMatrix("modelViewMatrix", ModelMat.Values);
            prog.Uniform("colorIn", outLineColorMul);
            rpi.RenderMesh(recipeOutlineMeshRef);
            prog.Stop();
        }



        public void RegenMesh(ItemStack workitem, bool[,,] Voxels, ClayFormingRecipe recipeToOutline, int recipeLayer)
        {
            workItemMeshRef?.Dispose();
            workItemMeshRef = null;

            if (workitem == null) return;

            if (recipeToOutline != null)
            {
                RegenOutlineMesh(recipeToOutline, Voxels, recipeLayer);
            }

            this.workItem = workitem;
            MeshData workItemMesh = new MeshData(24, 36, false);
            
            workItemMesh.Rgba2 = null;

            float subPixelPaddingx = api.BlockTextureAtlas.SubPixelPaddingX;
            float subPixelPaddingy = api.BlockTextureAtlas.SubPixelPaddingY;

            TextureAtlasPosition tpos = api.BlockTextureAtlas.GetPosition(api.World.GetBlock(new AssetLocation("clayform")), workitem.Collectible.Code.ToShortString());
            MeshData singleVoxelMesh = CubeMeshUtil.GetCubeOnlyScaleXyz(1 / 32f, 1 / 32f, new Vec3f(1 / 32f, 1 / 32f, 1 / 32f));
            singleVoxelMesh.Rgba = new byte[6 * 4 * 4].Fill((byte)255);
            CubeMeshUtil.SetXyzFacesAndPacketNormals(singleVoxelMesh);

            texId = tpos.atlasTextureId;

            for (int i = 0; i < singleVoxelMesh.Uv.Length; i++)
            {
                if (i % 2 > 0)
                {
                    singleVoxelMesh.Uv[i] = tpos.y1 + singleVoxelMesh.Uv[i] * 2f / api.BlockTextureAtlas.Size.Height - subPixelPaddingy;
                } else
                {
                    singleVoxelMesh.Uv[i] = tpos.x1 + singleVoxelMesh.Uv[i] * 2f / api.BlockTextureAtlas.Size.Width - subPixelPaddingx;
                }
                
            }

            singleVoxelMesh.XyzFaces = (int[])CubeMeshUtil.CubeFaceIndices.Clone();
            singleVoxelMesh.XyzFacesCount = 6;
            singleVoxelMesh.Tints = new int[6];
            
            singleVoxelMesh.TintsCount = 6;


            MeshData voxelMeshOffset = singleVoxelMesh.Clone();

            for (int x = 0; x < 16; x++)
            {
                for (int y = 0; y < 16; y++)
                {
                    for (int z = 0; z < 16; z++)
                    {
                        if (!Voxels[x, y, z]) continue;

                        float px = x / 16f;
                        float py = y / 16f;
                        float pz = z / 16f;

                        for (int i = 0; i < singleVoxelMesh.xyz.Length; i += 3)
                        {
                            voxelMeshOffset.xyz[i] = px + singleVoxelMesh.xyz[i];
                            voxelMeshOffset.xyz[i + 1] = py + singleVoxelMesh.xyz[i + 1];
                            voxelMeshOffset.xyz[i + 2] = pz + singleVoxelMesh.xyz[i + 2];
                        }

                        float offsetX = ((((x+4*y) % 16f / 16f)) * 32f) / api.BlockTextureAtlas.Size.Width;
                        float offsetY = (pz * 32f) / api.BlockTextureAtlas.Size.Height;

                        for (int i = 0; i < singleVoxelMesh.Uv.Length; i += 2)
                        {
                            voxelMeshOffset.Uv[i] = singleVoxelMesh.Uv[i] + offsetX;
                            voxelMeshOffset.Uv[i + 1] = singleVoxelMesh.Uv[i + 1] + offsetY;
                        }

                        workItemMesh.AddMeshData(voxelMeshOffset);
                    }
                }
            }

            workItemMeshRef = api.Render.UploadMesh(workItemMesh);
        }

        private void RegenOutlineMesh(ClayFormingRecipe recipeToOutline, bool[,,] Voxels, int recipeLayer)
        {

            MeshData recipeOutlineMesh = new MeshData(24, 36, false, false, true, false, false);
            recipeOutlineMesh.SetMode(EnumDrawMode.Lines);

            int greenCol = (156 << 24) | (100 << 16) | (200 << 8) | (100);
            int orangeCol = (156 << 24) | (226 << 16) | (171 << 8) | (92);

            MeshData greenVoxelMesh = LineMeshUtil.GetCube(greenCol);
            MeshData orangeVoxelMesh = LineMeshUtil.GetCube(orangeCol);
            for (int i = 0; i < greenVoxelMesh.xyz.Length; i++)
            {
                greenVoxelMesh.xyz[i] = greenVoxelMesh.xyz[i] / 32f + 1 / 32f;
                orangeVoxelMesh.xyz[i] = orangeVoxelMesh.xyz[i] / 32f + 1 / 32f;
            }
            MeshData voxelMeshOffset = greenVoxelMesh.Clone();

            for (int x = 0; x < 16; x++)
            {
                int y = recipeLayer;
                for (int z = 0; z < 16; z++)
                {
                    bool shouldFill = recipeToOutline.Voxels[x, y, z];
                    bool didFill = Voxels[x, y, z];

                    if (shouldFill == didFill) continue;

                    float px = x / 16f;
                    float py = y / 16f + 0.001f;
                    float pz = z / 16f;

                    for (int i = 0; i < greenVoxelMesh.xyz.Length; i += 3)
                    {
                        voxelMeshOffset.xyz[i] = px + greenVoxelMesh.xyz[i];
                        voxelMeshOffset.xyz[i + 1] = py + greenVoxelMesh.xyz[i + 1];
                        voxelMeshOffset.xyz[i + 2] = pz + greenVoxelMesh.xyz[i + 2];
                    }

                    voxelMeshOffset.Rgba = (shouldFill && !didFill) ? greenVoxelMesh.Rgba : orangeVoxelMesh.Rgba;

                    recipeOutlineMesh.AddMeshData(voxelMeshOffset);
                }
            }

            recipeOutlineMeshRef?.Dispose();
            recipeOutlineMeshRef = api.Render.UploadMesh(recipeOutlineMesh);
        }

        public void Dispose()
        {
            api.Event.UnregisterRenderer(this, EnumRenderStage.Opaque);
            api.Event.UnregisterRenderer(this, EnumRenderStage.AfterFinalComposition);
            recipeOutlineMeshRef?.Dispose();
            workItemMeshRef?.Dispose();
        }
    }
}
