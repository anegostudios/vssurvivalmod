using OpenTK.Graphics.ES11;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class BlockEntityBellows : BlockEntity
    {
        public float PumpingSpeed = 1;

        protected float animationDuration = 4f / 3f;
        protected long interactStartTotalMs;
        protected float originalAnimationSpeed = 1;
        protected AnimationMetaData? animation;

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            animation = Block.Attributes?["bellowAnimation"].AsObject<AnimationMetaData>();
            originalAnimationSpeed = animation?.AnimationSpeed ?? 1;
        }

        public bool Interact(IPlayer byPlayer)
        {
            if ((Api.World.ElapsedMilliseconds - interactStartTotalMs) / 1000f < animationDuration / PumpingSpeed) return false;
            interactStartTotalMs = Api.World.ElapsedMilliseconds;
            
            var facing = BlockFacing.FromCode(Block.Variant["side"]);

            Api.World.PlaySoundAt(new AssetLocation("sounds/effect/bellows"), Pos, 0.5f, byPlayer, false, 8);

            RegisterDelayedCallback((dt) =>
            {
                var beforge = Block.GetBlockEntity<BlockEntityForge>(Pos.AddCopy(facing));
                if (beforge != null)
                {
                    beforge.BlowAirInto(0.2f, facing);
                }
                GetBehavior<BEBehaviorDurability>()?.DamageBlock(1);

            }, (int)(733 / PumpingSpeed));

            BlockEntityAnimationUtil? animUtil = GetBehavior<BEBehaviorAnimatable>()?.animUtil;
            if (animUtil == null || animation == null) return true;

            animation.AnimationSpeed = originalAnimationSpeed * PumpingSpeed;
            animUtil.StartAnimation(animation);

            var ranim = animUtil.animator?.GetAnimationState(animation.Code);
            if (ranim != null)
            {
                ranim.CurrentFrame = 0;
                ranim.Iterations = 0;
            }

            

            return true;
        }

        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tessThreadTesselator)
        {
            BlockEntityAnimationUtil? animUtil = GetBehavior<BEBehaviorAnimatable>()?.animUtil;
            if (animUtil?.animator == null)
            {
                float rotY = BlockFacing.FromCode(Block.Variant["side"]).HorizontalAngleIndex * 90 + 180;
                animUtil?.InitializeAnimator("bellows" + Block.Code, null, null, new Vec3f(0, rotY, 0));
            }

            return base.OnTesselation(mesher, tessThreadTesselator);
        }
    }
}
