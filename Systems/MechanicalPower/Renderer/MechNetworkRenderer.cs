using System;
using System.Collections.Generic;
using Vintagestory.API.Client;

#nullable disable

namespace Vintagestory.GameContent.Mechanics
{
    /// <summary>
    /// 1. Use instanced rendering to issue one draw call for all mech.power blocks of one type. (i.e. 1 draw call for all axles)
    /// 2. Each block type has only 1 default shape per orientation. Different orientations are made with transformation matrixes
    /// 3. Each frame, upload a transformation matrix for the full block
    /// </summary>
    public class MechNetworkRenderer : IRenderer
    {
        MechanicalPowerMod mechanicalPowerMod;
        ICoreClientAPI capi;
        IShaderProgram prog;

        List<MechBlockRenderer> MechBlockRenderer = new List<MechBlockRenderer>();
        Dictionary<int, int> MechBlockRendererByShape = new Dictionary<int, int>();

        public static Dictionary<string, Type> RendererByCode = new Dictionary<string, Type>()
        {
            { "generic", typeof(GenericMechBlockRenderer) },
            { "angledgears", typeof(AngledGearsBlockRenderer) },
            { "angledgearcage", typeof(AngledCageGearRenderer) },
            { "transmission", typeof(TransmissionBlockRenderer) },
            { "clutch", typeof(ClutchBlockRenderer) },
            { "pulverizer", typeof(PulverizerRenderer) },
            { "autorotor", typeof(CreativeRotorRenderer) }
        };
        
        public MechNetworkRenderer(ICoreClientAPI capi, MechanicalPowerMod mechanicalPowerMod)
        {
            this.mechanicalPowerMod = mechanicalPowerMod;
            this.capi = capi;

            capi.Event.RegisterRenderer(this, EnumRenderStage.Opaque, "mechnetwork");
            capi.Event.RegisterRenderer(this, EnumRenderStage.ShadowFar, "mechnetwork");
            capi.Event.RegisterRenderer(this, EnumRenderStage.ShadowNear, "mechnetwork");

            // This shader is created by the essentials mod in Core.cs
            prog = capi.Shader.GetProgramByName("instanced");
        }



        public void AddDevice(IMechanicalPowerRenderable device)
        {
            if (device.Shape == null) return;

            int index = -1;
            string rendererCode = "generic";
            if (device.Block.Attributes?["mechanicalPower"]?["renderer"].Exists == true)
            {
                rendererCode = device.Block.Attributes?["mechanicalPower"]?["renderer"].AsString("generic");
            }

            int hashCode = device.Shape.GetHashCode() + device.Block.Textures.Values.GetHashCode() + rendererCode.GetHashCode();

            if (!MechBlockRendererByShape.TryGetValue(hashCode, out index))
            {
                object obj = Activator.CreateInstance(RendererByCode[rendererCode], capi, mechanicalPowerMod, device.Block, device.Shape);
                MechBlockRenderer.Add((MechBlockRenderer)obj);
                MechBlockRendererByShape[hashCode] = index = MechBlockRenderer.Count - 1;
            }

            MechBlockRenderer[index].AddDevice(device);
        }

        

        public void RemoveDevice(IMechanicalPowerRenderable device)
        {
            if (device.Shape == null) return;

            foreach (var val in MechBlockRenderer)
            {
                if (val.RemoveDevice(device)) return;
            }
        }

        
        public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
        {
            if (prog.Disposed) prog = capi.Shader.GetProgramByName("instanced");

            capi.Render.GlDisableCullFace();

            if (stage == EnumRenderStage.Opaque)
            {
                capi.Render.GlToggleBlend(false); // Seems to break SSAO without
                prog.Use();
                prog.BindTexture2D("tex", capi.BlockTextureAtlas.Positions[0].atlasTextureId, 0);
                prog.Uniform("rgbaFogIn", capi.Render.FogColor);
                prog.Uniform("rgbaAmbientIn", capi.Render.AmbientColor);
                prog.Uniform("fogMinIn", capi.Render.FogMin);
                prog.Uniform("fogDensityIn", capi.Render.FogDensity);
                prog.UniformMatrix("projectionMatrix", capi.Render.CurrentProjectionMatrix);
                prog.UniformMatrix("modelViewMatrix", capi.Render.CameraMatrixOriginf);

                for (int i = 0; i < MechBlockRenderer.Count; i++)
                {
                    MechBlockRenderer[i].OnRenderFrame(deltaTime, prog);
                }

                prog.Stop();
            }
            else
            {
                // TODO: Needs a custom shadow map shader
                /*IRenderAPI rapi = capi.Render;
                rapi.CurrentActiveShader.BindTexture2D("tex2d", capi.BlockTextureAtlas.Positions[0].atlasTextureId, 0);

                float[] mvpMat = Mat4f.Mul(Mat4f.Create(), capi.Render.CurrentProjectionMatrix, capi.Render.CurrentModelviewMatrix);
                
                capi.Render.CurrentActiveShader.UniformMatrix("mvpMatrix", mvpMat);
                //capi.Render.CurrentActiveShader.Uniform("origin", new Vec3f());

                for (int i = 0; i < MechBlockRenderer.Count; i++)
                {
                    MechBlockRenderer[i].OnRenderFrame(deltaTime, prog);
                }*/
            }


            capi.Render.GlEnableCullFace();
        }


        public double RenderOrder
        {
            get
            {
                return 0.5;
            }
        }

        public int RenderRange
        {
            get { return 100; }
        }

        public void Dispose()
        {
            for (int i = 0; i < MechBlockRenderer.Count; i++)
            {
                MechBlockRenderer[i].Dispose();
            }
        }

    }
}

