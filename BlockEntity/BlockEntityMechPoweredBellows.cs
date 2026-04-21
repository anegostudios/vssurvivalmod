using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent.Mechanics;

#nullable disable

namespace Vintagestory.GameContent
{
    public class BlockEntityMechPoweredBellows : BlockEntityBellows, IRenderer
    {
        protected BEBehaviorMPConsumer mpc;
        protected AnimatableRenderer renderer;
        protected BlockEntityAnimationUtil animUtil;
        protected ICoreClientAPI capi;
        protected bool connected;

        public double RenderOrder => 0;
        public int RenderRange => 999;

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            capi = api as ICoreClientAPI;
            animUtil = new BlockEntityAnimationUtil(api, this);
            RegisterGameTickListener(onTick, 500, 2);
        }

        private void onTick(float dt)
        {
            var facing = BlockFacing.FromCode(Block.Variant["side"]);
            var powerSourcePos = Pos.DownCopy().AddCopy(facing.Opposite);
            var armBlock = Api.World.BlockAccessor.GetBlock(powerSourcePos);

            if (armBlock is BlockMPConsumer)
            {
                if (!connected)
                {

                    mpc = armBlock.GetBEBehavior<BEBehaviorMPConsumer>(powerSourcePos);
                    connected = true;
                    capi?.Event.RegisterRenderer(this, EnumRenderStage.Before, "mechpoweredbellows");
                    animation.AnimationSpeed = 0;
                    animation.StartFrameOnce = mpc.AngleRad / GameMath.TWOPI * 60;
                    animUtil.StartAnimation(animation);
                }
            } else
            {
                if (connected)
                {
                    mpc = null;
                    capi?.Event.UnregisterRenderer(this, EnumRenderStage.Before);
                    connected = false;
                    animUtil.StopAnimation(animation.Code);
                }
            }
        }

        public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
        {
            // 60 frames
            if (mpc?.Network != null && animUtil.animator != null)
            {
                var animState = animUtil.animator.GetAnimationState("bellowing");
                if (animState != null)
                {
                    animation.AnimationSpeed = mpc.Network.Speed * 2f; // Otherwise doesn't animate or sound doesn't play properly
                    animState.CurrentFrame = mpc.AngleRad / GameMath.TWOPI * 60;
                }
            } else
            {
                animation.AnimationSpeed = 0;
            }
        }


        public override bool Interact(IPlayer byPlayer)
        {
            return false;
        }

        public override void OnBlockUnloaded()
        {
            base.OnBlockUnloaded();
            animUtil?.Dispose();
        }

        public override void OnBlockBroken(IPlayer byPlayer = null)
        {
            base.OnBlockBroken(byPlayer);
            animUtil?.Dispose();
        }

        public override void OnBlockRemoved()
        {
            base.OnBlockRemoved();
            animUtil?.Dispose();
        }

        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tessThreadTesselator)
        {
            if (animUtil?.animator == null)
            {
                var facing = BlockFacing.FromCode(Block.Variant["side"]);
                var rot = new Vec3f(0, -facing.HorizontalAngleIndex * 90, 0);
                animUtil?.InitializeAnimator("mechpoweredbellows", null, null, rot);
            }

            return animUtil.activeAnimationsByAnimCode.Count > 0 || (animUtil.animator != null && animUtil.animator.ActiveAnimationCount > 0);
        }

        public void Dispose()
        {
            
        }
    }
}
