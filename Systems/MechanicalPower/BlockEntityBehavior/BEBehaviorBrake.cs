using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.GameContent.Mechanics
{
    public class BEBehaviorMPBrake : BEBehaviorMPAxle
    {
        BEBrake bebrake;
        float resistance;
        ILoadedSound brakeSound;

        public override CompositeShape Shape { 
            get {
                string side = Block.Variant["side"];

                CompositeShape shape = new CompositeShape() { Base = new AssetLocation("shapes/block/wood/mechanics/axle.json") };

                if (side == "east" || side == "west")
                {
                    shape.rotateY = 90;
                }

                return shape;
            }
            set {

            }
        }

        public BEBehaviorMPBrake(BlockEntity blockentity) : base(blockentity)
        {
            bebrake = blockentity as BEBrake;

        }

        public override void Initialize(ICoreAPI api, JsonObject properties)
        {
            base.Initialize(api, properties);

            bebrake.RegisterGameTickListener(OnEvery50Ms, 100);

            string side = Block.Variant["side"];
            switch (side)
            {
                case "north":
                case "south":
                    AxisSign = new int[] { -1, 0, 0 };
                    break;

                case "east":
                case "west":
                    AxisSign = new int[] { 0, 0, -1 };
                    break;
            }

        }

        protected override bool AddStands => false;




        private void OnEvery50Ms(float dt)
        {
            resistance = GameMath.Clamp(resistance + dt / (bebrake.Engaged ? 20 : -10), 0, 3);

            if (bebrake.Engaged && network != null && network.Speed > 0.1)
            {
                Api.World.SpawnParticles(
                    network.Speed * 1.7f, 
                    ColorUtil.ColorFromRgba(60, 60, 60, 100),
                    Position.ToVec3d().Add(0.1f, 0.5f, 0.1f), 
                    Position.ToVec3d().Add(0.8f, 0.3f, 0.8f), 
                    new Vec3f(-0.1f, 0.1f, -0.1f), 
                    new Vec3f(0.2f, 0.2f, 0.2f), 
                    2, 0, 0.3f);
            }

            UpdateBreakSounds();
        }

        public void UpdateBreakSounds()
        {
            if (Api.Side != EnumAppSide.Client) return;

            if (resistance > 0 && bebrake.Engaged && network != null && network.Speed > 0.1)
            {
                if (brakeSound == null || !brakeSound.IsPlaying)
                {
                    brakeSound = ((IClientWorldAccessor)Api.World).LoadSound(new SoundParams()
                    {
                        Location = new AssetLocation("sounds/effect/woodgrind.ogg"),
                        ShouldLoop = true,
                        Position = Position.ToVec3f().Add(0.5f, 0.25f, 0.5f),
                        DisposeOnFinish = false,
                        Volume = 1
                    });

                    brakeSound.Start();
                }

                brakeSound.SetPitch(GameMath.Clamp(network.Speed * 1.5f + 0.2f, 0.5f, 1));
            }
            else
            {
                brakeSound?.FadeOut(1, (s) => { brakeSound.Stop(); });
            }

        }


        public override float GetResistance()
        {
            return resistance;
        }



        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tesselator)
        {
            return base.OnTesselation(mesher, tesselator);
        }
    }
}
