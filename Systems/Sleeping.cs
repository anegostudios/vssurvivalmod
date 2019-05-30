using ProtoBuf;
using System;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace Vintagestory.GameContent
{
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class NetworksMessageAllSleepMode
    {
        public bool On;
    }

    /// <summary>
    /// This class contains core settings for the Vintagestory server
    /// </summary>
    public class ModSleeping : ModSystem
    {
        // Common
        ICoreAPI api;
        public bool AllSleeping = false;
        public float GameSpeedBoost = 0;

        // Server
        ICoreServerAPI sapi;
        IServerNetworkChannel serverChannel;

        // Client
        ICoreClientAPI capi;
        IClientNetworkChannel clientChannel;
        EyesOverlayRenderer renderer;
        IShaderProgram eyeShaderProg;

        float sleepLevel;


        public override bool ShouldLoad(EnumAppSide side)
        {
            return true;
        }

        public override void Start(ICoreAPI api)
        {
            base.Start(api);
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            base.StartServerSide(api);
            this.api = api;
            this.sapi = api;

            api.Event.RegisterGameTickListener(ServerSlowTick, 200);
            api.Event.ServerRunPhase(EnumServerRunPhase.Shutdown, OnServerShutDown);
            api.Event.RegisterGameTickListener(FastTick, 20);
            
            serverChannel =
               api.Network.RegisterChannel("sleeping")
               .RegisterMessageType(typeof(NetworksMessageAllSleepMode))
            ;
            
        }


        private void OnServerShutDown()
        {
            bool nowAllSleeping = AreAllPlayersSleeping();

            if (!nowAllSleeping)
            {
                api.World.Calendar?.RemoveTimeSpeedModifier("sleeping");
                GameSpeedBoost = 0;
            }
        }

        private void FastTick(float dt)
        {
            if (api.Side == EnumAppSide.Client)
            {
                renderer.Level = sleepLevel;

                bool sleeping = capi.World?.Player?.Entity?.MountedOn is BlockEntityBed;
                sleepLevel = GameMath.Clamp(sleepLevel + dt * (sleeping && AllSleeping ? 0.1f : -0.35f), 0, 0.99f);
            }

            if (GameSpeedBoost <= 0 && !AllSleeping) return;

            GameSpeedBoost = GameMath.Clamp(GameSpeedBoost + dt * (AllSleeping ? 400 : -2000), 0, 17000);
            api.World.Calendar.SetTimeSpeedModifier("sleeping", (int)GameSpeedBoost);
        }

        private void ServerSlowTick(float dt)
        {
            bool nowAllSleeping = AreAllPlayersSleeping();
            if (nowAllSleeping == AllSleeping) return;

            // Start
            if (nowAllSleeping)
            {
                serverChannel.BroadcastPacket(new NetworksMessageAllSleepMode() { On = true });
            } else
            // Stop
            {
                serverChannel.BroadcastPacket(new NetworksMessageAllSleepMode() { On = false });
            }

            AllSleeping = nowAllSleeping;
        }


        public bool AreAllPlayersSleeping()
        {
            int quantitySleeping = 0;
            int quantityAwake = 0;

            foreach (IPlayer player in sapi.World.AllOnlinePlayers)
            {
                IServerPlayer splr = player as IServerPlayer;
                if (splr.ConnectionState != EnumClientState.Playing || splr.WorldData.CurrentGameMode == EnumGameMode.Spectator) continue;

                IMountable mount = player.Entity?.MountedOn;
                if (mount != null && mount is BlockEntityBed)
                {
                    quantitySleeping++;
                } else
                {
                    quantityAwake++;
                }
            }

            return quantitySleeping > 0 && quantityAwake == 0;
        }



        public string VertexShaderCode = @"
#version 330 core
#extension GL_ARB_explicit_attrib_location: enable

layout(location = 0) in vec3 vertex;

out vec2 uv;

void main(void)
{
    gl_Position = vec4(vertex.xy, 0, 1);
    uv = (vertex.xy + 1.0) / 2.0;
}
";


        public string FragmentShaderCode = @"
#version 330 core

in vec2 uv;

out vec4 outColor;

uniform float level;

void main () {
    vec2 uvOffseted = vec2(uv.x - 0.5, 2 * (1 + 2*level) * (uv.y - 0.5));
	float strength = 1 - smoothstep(1.1 - level, 0, length(uvOffseted));
	outColor = vec4(0, 0, 0, min(1, (4 * level - 0.8) + level * strength));
}
";



        public override void StartClientSide(ICoreClientAPI api)
        {
            base.StartClientSide(api);
            this.api = api;
            this.capi = api;

            api.Event.RegisterGameTickListener(FastTick, 20);

            api.Event.ReloadShader += LoadShader;
            LoadShader();
            
            renderer = new EyesOverlayRenderer(api, eyeShaderProg);
            api.Event.RegisterRenderer(renderer, EnumRenderStage.Ortho);
            api.Event.LeaveWorld += () =>
            {
                renderer?.Dispose();
            };

            clientChannel =
                api.Network.RegisterChannel("sleeping")
                .RegisterMessageType(typeof(NetworksMessageAllSleepMode))
                .SetMessageHandler<NetworksMessageAllSleepMode>(OnAllSleepingStateChanged)
            ;
        }

        public bool LoadShader()
        {
            eyeShaderProg = capi.Shader.NewShaderProgram();

            eyeShaderProg.VertexShader = capi.Shader.NewShader(EnumShaderType.VertexShader);
            eyeShaderProg.FragmentShader = capi.Shader.NewShader(EnumShaderType.FragmentShader);

            eyeShaderProg.VertexShader.Code = VertexShaderCode;
            eyeShaderProg.FragmentShader.Code = FragmentShaderCode;

            capi.Shader.RegisterMemoryShaderProgram("sleepoverlay", eyeShaderProg);
            eyeShaderProg.PrepareUniformLocations("level");
            

            if (renderer != null) renderer.eyeShaderProg = eyeShaderProg;

            return eyeShaderProg.Compile();
        }

        private void OnAllSleepingStateChanged(NetworksMessageAllSleepMode networkMessage)
        {
            AllSleeping = networkMessage.On;

            if (!AllSleeping && GameSpeedBoost <= 0)
            {
                api.World.Calendar.SetTimeSpeedModifier("sleeping", 0);
            }
        }
    }
    

    public class EyesOverlayRenderer : IRenderer
    {
        internal MeshRef quadRef;
        ICoreClientAPI capi;
        public IShaderProgram eyeShaderProg;

        public bool ShouldRender;
        public float Level;

        public float rndTarget;
        public float curRndVal;

        LoadedTexture exitHelpTexture;

        public EyesOverlayRenderer(ICoreClientAPI capi, IShaderProgram eyeShaderProg)
        {
            this.capi = capi;
            this.eyeShaderProg = eyeShaderProg;

            MeshData quadMesh = QuadMeshUtil.GetCustomQuadModelData(-1, -1, -20000 + 151 + 1, 2, 2);
            quadMesh.Rgba = null;

            quadRef = capi.Render.UploadMesh(quadMesh);

            string hotkey = capi.Input.HotKeys["sneak"].CurrentMapping.ToString();
            exitHelpTexture = capi.Gui.TextTexture.GenTextTexture(Lang.Get("bed-exithint", hotkey), CairoFont.WhiteSmallishText());
        }

        public double RenderOrder
        {
            get { return 0.95; }
        }

        public int RenderRange { get { return 1; } }

        public void Dispose()
        {
            capi.Render.DeleteMesh(quadRef);
            exitHelpTexture?.Dispose();
            eyeShaderProg.Dispose();
        }

        public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
        {
            if (Level <= 0 || capi.World.Player.CameraMode != EnumCameraMode.FirstPerson) return;

            if (Level > 0.2 &&  capi.World.Rand.Next(60) == 0)
            {
                rndTarget = (float)capi.World.Rand.NextDouble() / 5f - 1 / 10f;
            }

            curRndVal += (rndTarget - curRndVal) * deltaTime;

            capi.Render.Render2DLoadedTexture(exitHelpTexture, capi.Render.FrameWidth / 2 - exitHelpTexture.Width / 2, capi.Render.FrameHeight * 3/4f);

            IShaderProgram curShader = capi.Render.CurrentActiveShader;
            curShader.Stop();

            eyeShaderProg.Use();

            capi.Render.GlToggleBlend(true);
            capi.Render.GLDepthMask(false);
            eyeShaderProg.Uniform("level", Level + curRndVal);
            
            capi.Render.RenderMesh(quadRef);
            eyeShaderProg.Stop();

            capi.Render.GLDepthMask(true);
            curShader.Use();
        }
    }
}