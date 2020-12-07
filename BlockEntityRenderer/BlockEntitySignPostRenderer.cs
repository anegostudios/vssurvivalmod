using Cairo;
using System;
using System.Collections.Generic;
using Vintagestory.API;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class BlockEntitySignPostRenderer : IRenderer
    {
        protected static int TextWidth = 200;
        protected static int TextHeight = 25;

        protected static float QuadWidth = 0.7f;
        protected static float QuadHeight = 0.1f;


        protected CairoFont font;
        protected BlockPos pos;
        protected ICoreClientAPI api;

        protected LoadedTexture loadedTexture;
        protected MeshRef quadModelRef;
        public Matrixf ModelMat = new Matrixf();

        protected float rotY = 0;
        protected float translateX = 0;
        protected float translateY = 0.5625f;
        protected float translateZ = 0;

        string[] textByCardinal;

        public double RenderOrder
        {
            get { return 0.5; }
        }

        public int RenderRange
        {
            get { return 48; }
        }

        double fontSize;


        public BlockEntitySignPostRenderer(BlockPos pos, ICoreClientAPI api, CairoFont font)
        {
            this.api = api;
            this.pos = pos;
            this.font = font;

            fontSize = font.UnscaledFontsize;


            api.Event.RegisterRenderer(this, EnumRenderStage.Opaque, "signpost");
        }


        private void genMesh()
        {
            MeshData allMeshes = new MeshData(4, 6);

            int qsigns = 0;
            for (int i = 0; i < 8; i++)
            {
                if(textByCardinal[i].Length == 0) continue;
                qsigns++;
            }

            if (qsigns == 0)
            {
                quadModelRef?.Dispose();
                quadModelRef = null;
                return;
            }


            int snum = 0;
            for (int i = 0; i < 8; i++)
            {
                if (textByCardinal[i].Length == 0) continue;

                Cardinal dir = Cardinal.ALL[i];

                MeshData modeldata = QuadMeshUtil.GetQuad();

                float vStart = snum / (float)qsigns;
                float vEnd = (snum + 1) / (float)qsigns;
                snum++;

                modeldata.Uv = new float[]
                {
                    1, vEnd,
                    0, vEnd,
                    0, vStart,
                    1, vStart
                };

                modeldata.Rgba = new byte[4 * 4];
                modeldata.Rgba.Fill((byte)255);
                //modeldata.Rgba2 = null;

                Vec3f orig = new Vec3f(0.5f, 0.5f, 0.5f);

                switch (dir.Index)
                {
                    case 0: // N
                        rotY = 90;
                        break;
                    case 1: // NE
                        rotY = 45;
                        break;
                    case 2: // E
                        rotY = 0;
                        break;
                    case 3: // SE
                        rotY = 315;
                        break;
                    case 4: // S
                        rotY = 270;
                        break;
                    case 5: // SW
                        rotY = 225;
                        break;
                    case 6: // W
                        rotY = 180;
                        break;
                    case 7: // NW
                        rotY = 135;
                        break;
                }

                modeldata.Translate(1.6f, 0, 0.375f);

                MeshData front = modeldata.Clone();

                front.Scale(orig, 0.5f * QuadWidth, 0.4f * QuadHeight, 0.5f * QuadWidth);
                front.Rotate(orig, 0, rotY * GameMath.DEG2RAD, 0);
                front.Translate(0, 1.39f, 0);
                allMeshes.AddMeshData(front);


                MeshData back = modeldata;

                back.Uv = new float[]
                {
                    0, vEnd,
                    1, vEnd,
                    1, vStart,
                    0, vStart
                };
                back.Translate(0, 0, 0.26f);
                back.Scale(orig, 0.5f * QuadWidth, 0.4f * QuadHeight, 0.5f * QuadWidth);
                back.Rotate(orig, 0, rotY * GameMath.DEG2RAD, 0);
                back.Translate(0, 1.39f, 0);
                allMeshes.AddMeshData(back);
            }

            quadModelRef?.Dispose();
            quadModelRef = api.Render.UploadMesh(allMeshes);
        }


        public virtual void SetNewText(string[] textByCardinal, int color)
        {
            this.textByCardinal = textByCardinal;
            font.WithColor(ColorUtil.ToRGBADoubles(color));
            font.UnscaledFontsize = fontSize / RuntimeEnv.GUIScale;

            int lines = 0;
            for (int i = 0; i < textByCardinal.Length; i++)
            {
                if (textByCardinal[i].Length > 0)
                {
                    lines++;
                }
            }

            if (lines == 0)
            {
                loadedTexture?.Dispose();
                loadedTexture = null;
                return;
            }

            ImageSurface surface = new ImageSurface(Format.Argb32, TextWidth, TextHeight * lines);
            Context ctx = new Context(surface);
            font.SetupContext(ctx);

            int line = 0;
            for (int i = 0; i < textByCardinal.Length; i++)
            {
                if (textByCardinal[i].Length > 0)
                {
                    double linewidth = font.GetTextExtents(textByCardinal[i]).Width;

                    ctx.MoveTo((TextWidth - linewidth)/2, line * TextHeight + ctx.FontExtents.Ascent);
                    ctx.ShowText(textByCardinal[i]);
                    line++;
                }
            }


            if (loadedTexture == null) loadedTexture = new LoadedTexture(api);
            api.Gui.LoadOrUpdateCairoTexture(surface, true, ref loadedTexture);
            

            surface.Dispose();
            ctx.Dispose();


            genMesh();
        }
        


        public virtual void OnRenderFrame(float deltaTime, EnumRenderStage stage)
        {
            if (loadedTexture == null) return;

            IRenderAPI rpi = api.Render;
            Vec3d camPos = api.World.Player.Entity.CameraPos;

            rpi.GlDisableCullFace();
            rpi.GlToggleBlend(true, EnumBlendMode.PremultipliedAlpha);

            IStandardShaderProgram prog = rpi.PreparedStandardShader(pos.X, pos.Y, pos.Z);

            prog.Tex2D = loadedTexture.TextureId;

            prog.ModelMatrix = ModelMat
                .Identity()
                .Translate(pos.X - camPos.X, pos.Y - camPos.Y, pos.Z - camPos.Z)
                .Values
            ;

            prog.ViewMatrix = rpi.CameraMatrixOriginf;
            prog.ProjectionMatrix = rpi.CurrentProjectionMatrix;
            prog.NormalShaded = 0;

            rpi.RenderMesh(quadModelRef);
            prog.Stop();

            rpi.GlToggleBlend(true, EnumBlendMode.Standard);
        }

        public void Dispose()
        {
            api.Event.UnregisterRenderer(this, EnumRenderStage.Opaque);
            loadedTexture?.Dispose();
            quadModelRef?.Dispose();
        }

    }
}
