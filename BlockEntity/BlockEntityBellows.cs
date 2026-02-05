using OpenTK.Graphics.ES11;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.GameContent
{
    public class BlockEntityBellows : BlockEntity
    {
        BlockEntityAnimationUtil animUtil
        {
            get { return GetBehavior<BEBehaviorAnimatable>().animUtil; }
        }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
        }

        float animationDuration = 2f;
        float animSpeed = 1.5f;
        long interactStartTotalMs;

        public bool Interact(IPlayer byPlayer)
        {
            if ((Api.World.ElapsedMilliseconds - interactStartTotalMs) / 1000f < animationDuration / animSpeed) return false;

            interactStartTotalMs = Api.World.ElapsedMilliseconds;

            animUtil.StartAnimation(new AnimationMetaData() { Animation = "bellowing", Code = "bellowing", AnimationSpeed = animSpeed });

            var ranim = animUtil.animator?.GetAnimationState("bellowing");
            if (ranim != null)
            {
                ranim.CurrentFrame = 0;
                ranim.Iterations = 0;
            }
            
            var facing = BlockFacing.FromCode(Block.Variant["side"]);

            Api.World.PlaySoundAt(new AssetLocation("sounds/effect/bellows"), Pos, 0.5f, byPlayer, false, 8);

            RegisterDelayedCallback((dt) =>
            {
                var beforge = Block.GetBlockEntity<BlockEntityForge>(Pos.AddCopy(facing));
                if (beforge != null)
                {
                    beforge.BlowAirInto(0.2f, facing);
                }
            }, (int)(1100 / animSpeed));

            return true;
        }

        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tessThreadTesselator)
        {
            if (animUtil?.animator == null)
            {
                float rotY = BlockFacing.FromCode(Block.Variant["side"]).HorizontalAngleIndex * 90 + 180;
                animUtil?.InitializeAnimator("bellows" + Block.Code, null, null, new Vec3f(0, rotY, 0));
            }

            return base.OnTesselation(mesher, tessThreadTesselator);
        }
    }
}
