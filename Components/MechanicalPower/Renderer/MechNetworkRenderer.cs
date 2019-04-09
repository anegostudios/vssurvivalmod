using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

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
        Dictionary<AssetLocation, int> MechBlockRendererIndexByBlock = new Dictionary<AssetLocation, int>();

        public static Dictionary<string, Type> RendererByCode = new Dictionary<string, Type>()
        {
            { "generic", typeof(GenericMechBlockRenderer) },
            { "angledgears", typeof(AngledGearsBlockRenderer) }
        };
        
        public MechNetworkRenderer(ICoreClientAPI capi, MechanicalPowerMod mechanicalPowerMod)
        {
            this.mechanicalPowerMod = mechanicalPowerMod;
            this.capi = capi;

            capi.Event.RegisterRenderer(this, EnumRenderStage.Opaque);
            capi.Event.ReloadShader += LoadShader;
            LoadShader();
        }

        public bool LoadShader()
        {
            prog = capi.Shader.NewShaderProgram();

            prog.VertexShader = capi.Shader.NewShader(EnumShaderType.VertexShader);
            prog.FragmentShader = capi.Shader.NewShader(EnumShaderType.FragmentShader);

            capi.Shader.RegisterFileShaderProgram("mechpower", prog);

            prog.PrepareUniformLocations("rgbaFogIn", "rgbaAmbientIn", "fogMinIn", "fogDensityIn", "projectionMatrix", "modelViewMatrix", "tex");
            return prog.Compile();
        }


        public void AddDevice(IMechanicalPowerDevice device)
        {
            int index = -1;
            if (!MechBlockRendererIndexByBlock.TryGetValue(device.Block.Code, out index))
            {
                string rendererCode = "generic";
                if (device.Block.Attributes?["mechanicalPower"]?["renderer"].Exists == true) {
                    rendererCode = device.Block.Attributes?["mechanicalPower"]?["renderer"].AsString("generic");
                }

                object obj = Activator.CreateInstance(RendererByCode[rendererCode], capi, mechanicalPowerMod, device.Block);
                MechBlockRenderer.Add((MechBlockRenderer)obj);                
                MechBlockRendererIndexByBlock[device.Block.Code] = index = MechBlockRenderer.Count - 1;
            }

            MechBlockRenderer[index].AddDevice(device);
        }

        public void RemoveDevice(IMechanicalPowerDevice device)
        {
            int index = 0;
            if (MechBlockRendererIndexByBlock.TryGetValue(device.Block.Code, out index))
            {
                MechBlockRenderer[index].RemoveDevice(device);
            }
        }

        
        public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
        {
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

