using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.GameContent
{
    public class ModSystemMeasuringRope : ModSystem, IRenderer
    {
        public double RenderOrder => 0.9;
        public int RenderRange => 99;

        LoadedTexture hudTexture;
        ICoreClientAPI capi;

        BlockPos blockstart;
        BlockPos blockend;
        Vec3d start;
        Vec3d end = new Vec3d();
        BlockSelection blockSel;
        float accum;
        bool requireRedraw;

        MeshData updateModel;
        MeshRef pathModelRef;

        public override bool ShouldLoad(EnumAppSide forSide) => forSide == EnumAppSide.Client;

        public override void StartClientSide(ICoreClientAPI api)
        {
            capi = api;
            api.Event.RegisterRenderer(this, EnumRenderStage.Ortho, "measuringRope");
            api.Event.RegisterRenderer(this, EnumRenderStage.Opaque, "measuringrope");

            hudTexture = new LoadedTexture(api);

            var pathModel = new MeshData(4, 4, false, false, true, true);
            pathModel.SetMode(EnumDrawMode.LineStrip);
            pathModelRef = null;

            pathModel.AddVertexSkipTex(0, 0, 0, ColorUtil.WhiteArgb);
            pathModel.AddIndex(0);
            pathModel.AddVertexSkipTex(1, 1, 1, ColorUtil.WhiteArgb);
            pathModel.AddIndex(1);

            updateModel = new MeshData(false);
            updateModel.xyz = new float[6];

            pathModelRef = capi.Render.UploadMesh(pathModel);
        }

        public void OnRenderFrame(float dt, EnumRenderStage stage)
        {
            var slot = capi.World.Player.Entity.RightHandItemSlot;
            if (!(slot.Itemstack?.Collectible is ItemMeasuringRope)) return;
            if (stage == EnumRenderStage.Opaque)
            {
                if (start != null && hudTexture.TextureId > 0) onRender3D(dt);
                return;
            }

            var attr = slot.Itemstack.Attributes;

            bool didHavePos = start != null;
            bool nowHasPos = attr.HasAttribute("startX");

            requireRedraw |= hudTexture.TextureId==0;
            requireRedraw |= (capi.World.Player.CurrentBlockSelection == null) != (blockSel == null);

            if (nowHasPos)
            {
                blockSel = capi.World.Player.CurrentBlockSelection;
                var newEndPos = blockSel?.FullPosition ?? capi.World.Player.Entity.Pos.XYZ;
                requireRedraw |= !end.Equals(newEndPos, 0.001);
                end = newEndPos;
                blockend = blockSel?.Position ?? capi.World.Player.Entity.Pos.AsBlockPos;

                if (!didHavePos)
                {
                    start = new Vec3d();
                    blockstart = new BlockPos();
                    requireRedraw |= true;
                } else
                {
                    var x = attr.GetDouble("startX");
                    var y = attr.GetDouble("startY");
                    var z = attr.GetDouble("startZ");
                    requireRedraw |= !start.Equals(new Vec3d(x, y, z), 0.001);
                    start.Set(x, y, z);

                    blockstart = new BlockPos(attr.GetInt("blockX"), attr.GetInt("blockY"), attr.GetInt("blockZ"));
                }
            } else
            {
                if (didHavePos)
                {
                    start = null;
                    requireRedraw |= true;
                }
            }

            accum += dt;

            if (requireRedraw && accum > 0.1)
            {
                redrawHud();
                accum = 0;
            }
            

            if (hudTexture.TextureId > 0)
            {
                capi.Render.Render2DLoadedTexture(
                    hudTexture,
                    capi.Render.FrameWidth / 2 - hudTexture.Width / 2,
                    capi.Render.FrameHeight / 2 - hudTexture.Height - 50
                );
            }
        }

        private void onRender3D(float dt)
        {
            var prog = capi.Shader.GetProgram((int)EnumShaderProgram.Autocamera);

            updateModel.xyz[3] = (float)(end.X - start.X);
            updateModel.xyz[4] = (float)(end.Y - start.Y);
            updateModel.xyz[5] = (float)(end.Z - start.Z);
            updateModel.VerticesCount = 2;
            capi.Render.UpdateMesh(pathModelRef, updateModel);

            prog.Use();
            capi.Render.LineWidth=2;
            capi.Render.BindTexture2d(0);

            capi.Render.GlPushMatrix();
            capi.Render.GlLoadMatrix(capi.Render.CameraMatrixOrigin);

            Vec3d cameraPos = capi.World.Player.Entity.CameraPos;
            capi.Render.GlTranslate(
                (float)(start.X - cameraPos.X),
                (float)(start.Y - cameraPos.Y),
                (float)(start.Z - cameraPos.Z)
            );

            prog.UniformMatrix("projectionMatrix", capi.Render.CurrentProjectionMatrix);
            prog.UniformMatrix("modelViewMatrix", capi.Render.CurrentModelviewMatrix);

            capi.Render.RenderMesh(pathModelRef);

            capi.Render.GlPopMatrix();

            prog.Stop();
        }

        private void redrawHud()
        {
            string text = "Right click to set starting point. Left click to clear starting point.";

            if (start != null)
            {
                text = string.Format("{0:0.#}\nDistance: {1}\nOffset: ~{2} ~{3} ~{4}", 
                    blockSel != null ? "Measuring Block to Block" : "Measuring Block to Player",
                    start.DistanceTo(end),
                    (int)(blockend.X - blockstart.X),
                    (int)(blockend.Y - blockstart.Y),
                    (int)(blockend.Z - blockstart.Z)
                );
            }

            capi.Gui.TextTexture.GenOrUpdateTextTexture(
                text, 
                CairoFont.WhiteSmallText(), 400, 110, ref hudTexture, 
                new TextBackground() { FillColor = GuiStyle.DialogLightBgColor, Padding = 5 }
            );

            requireRedraw = false;
        }

        public override void Dispose()
        {
            hudTexture?.Dispose();
            pathModelRef?.Dispose();
        }
    }

    public class ItemMeasuringRope : Item
    {
        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            if (blockSel == null) return;

            var attr = slot.Itemstack.Attributes;
            attr.SetDouble("startX", blockSel.Position.X + blockSel.HitPosition.X);
            attr.SetDouble("startY", blockSel.Position.Y + blockSel.HitPosition.Y);
            attr.SetDouble("startZ", blockSel.Position.Z + blockSel.HitPosition.Z);

            attr.SetInt("blockX", blockSel.Position.X);
            attr.SetInt("blockY", blockSel.Position.Y);
            attr.SetInt("blockZ", blockSel.Position.Z);

            handling = EnumHandHandling.PreventDefault; 
        }

        public override void OnHeldAttackStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandHandling handling)
        {
            slot.Itemstack.Attributes.RemoveAttribute("startX");
            slot.Itemstack.Attributes.RemoveAttribute("startY");
            slot.Itemstack.Attributes.RemoveAttribute("startZ");
            handling = EnumHandHandling.PreventDefault;
        }
    }
}
