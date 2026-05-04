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
        private readonly MechanicalPowerMod mechanicalPowerMod;
        private readonly ICoreClientAPI capi;
        private IShaderProgram prog;

        private readonly List<MechBlockRenderer> mechBlockRenderers = new List<MechBlockRenderer>();
        private readonly Dictionary<int, int> mechBlockRendererByShape = new Dictionary<int, int>();

        public static Dictionary<string, Type> RendererByCode = new Dictionary<string, Type>()
        {
            { "generic",       typeof(GenericMechBlockRenderer) },
            { "angledgears",   typeof(AngledGearsBlockRenderer) },
            { "angledgearcage",typeof(AngledCageGearRenderer) },
            { "transmission",  typeof(TransmissionBlockRenderer) },
            { "clutch",        typeof(ClutchBlockRenderer) },
            { "pulverizer",    typeof(PulverizerRenderer) },
            { "autorotor",     typeof(CreativeRotorRenderer) }
        };

        public MechNetworkRenderer(ICoreClientAPI capi, MechanicalPowerMod mechanicalPowerMod)
        {
            this.mechanicalPowerMod = mechanicalPowerMod;
            this.capi = capi;

            capi.Event.RegisterRenderer(this, EnumRenderStage.Opaque, "mechnetwork");
            capi.Event.RegisterRenderer(this, EnumRenderStage.ShadowFar, "mechnetwork");
            capi.Event.RegisterRenderer(this, EnumRenderStage.ShadowNear, "mechnetwork");

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

            int hashCode = device.Shape.GetHashCode() + rendererCode.GetHashCode();

            if (!mechBlockRendererByShape.TryGetValue(hashCode, out index))
            {
                object obj = Activator.CreateInstance(RendererByCode[rendererCode], capi, mechanicalPowerMod, device.Block, device.Shape);
                mechBlockRenderers.Add((MechBlockRenderer)obj);
                mechBlockRendererByShape[hashCode] = index = mechBlockRenderers.Count - 1;
            }

            mechBlockRenderers[index].AddDevice(device);
        }

        public void RemoveDevice(IMechanicalPowerRenderable device)
        {
            if (device.Shape == null) return;

            foreach (var val in mechBlockRenderers)
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
                capi.Render.GlToggleBlend(false);
                prog.Use();

                prog.Uniform("rgbaFogIn", capi.Render.FogColor);
                prog.Uniform("rgbaAmbientIn", capi.Render.AmbientColor);
                prog.Uniform("fogMinIn", capi.Render.FogMin);
                prog.Uniform("fogDensityIn", capi.Render.FogDensity);
                prog.UniformMatrix("projectionMatrix", capi.Render.CurrentProjectionMatrix);
                prog.UniformMatrix("modelViewMatrix", capi.Render.CameraMatrixOriginf);

                // NOTE: BindTexture2D is intentionally NOT called here.
                // Each MechBlockRenderer is responsible for binding the correct atlas
                // per mesh group via RenderGroups(), which correctly handles meshes
                // whose faces span multiple texture atlases.
                for (int i = 0; i < mechBlockRenderers.Count; i++)
                {
                    mechBlockRenderers[i].OnRenderFrame(deltaTime, prog);
                }

                prog.Stop();
            }
            else
            {
                // TODO: Needs a custom shadow map shader
            }

            capi.Render.GlEnableCullFace();
        }

        public double RenderOrder => 0.5;

        public int RenderRange => 100;

        public void Dispose()
        {
            for (int i = 0; i < mechBlockRenderers.Count; i++)
            {
                mechBlockRenderers[i].Dispose();
            }
        }
    }
}
