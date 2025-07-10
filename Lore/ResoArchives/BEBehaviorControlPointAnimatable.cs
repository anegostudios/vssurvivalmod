using System;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

#nullable disable

namespace Vintagestory.GameContent
{
    public class BEBehaviorControlPointAnimatable : BEBehaviorAnimatable
    {
        protected ModSystemControlPoints modSys;
        protected float moveSpeedMul;
        protected ILoadedSound activeSound;
        protected bool active;
        protected ControlPoint animControlPoint;

        protected virtual Shape AnimationShape => null;

        public BEBehaviorControlPointAnimatable(BlockEntity blockentity) : base(blockentity)
        {

        }

        public override void Initialize(ICoreAPI api, JsonObject properties)
        {
            base.Initialize(api, properties);

            var controlpointcode = AssetLocation.Create(properties["controlpointcode"].ToString(), Block.Code.Domain);

            moveSpeedMul = properties["animSpeedMul"].AsFloat(1);

            string soundloc = properties["activeSound"].AsString();
            if (soundloc != null && api is ICoreClientAPI capi)
            {
                var loc = AssetLocation.Create(soundloc, Block.Code.Domain).WithPathPrefixOnce("sounds/");
                activeSound = capi.World.LoadSound(new SoundParams()
                {
                    Location = loc,
                    DisposeOnFinish = false,
                    ShouldLoop = true,
                    SoundType = EnumSoundType.Ambient,
                    Volume = 0.25f,
                    Range = 16,
                    RelativePosition = false,
                    Position = this.Pos.ToVec3f().Add(0.5f, 0.5f, 0.5f)
                });
            }

            modSys = api.ModLoader.GetModSystem<ModSystemControlPoints>();

            animControlPoint = modSys[controlpointcode];
            animControlPoint.Activate += BEBehaviorControlPointAnimatable_Activate;

            if (api.Side == EnumAppSide.Client)
            {
                animUtil.InitializeAnimator(Block.Code.ToShortString(), AnimationShape, null, new API.MathTools.Vec3f(0, Block.Shape.rotateY, 0));
                BEBehaviorControlPointAnimatable_Activate(animControlPoint);
            }
        }

        protected virtual void BEBehaviorControlPointAnimatable_Activate(ControlPoint cpoint)
        {
            updateAnimationstate();
        }

        protected void updateAnimationstate()
        {
            if (animControlPoint == null) return;

            active = false;
            var animData = animControlPoint.ControlData as AnimationMetaData;

            if (animData == null) return;

            if (animData.AnimationSpeed == 0)
            {
                activeSound?.FadeOutAndStop(2);
                animUtil.StopAnimation(animData.Animation);
                animUtil.StopAnimation(animData.Animation + "-inverse");
                return;
            }

            active = true;

            if (moveSpeedMul != 1)
            {
                animData = animData.Clone();
                if (moveSpeedMul < 0)
                {
                    animData.Animation += "-inverse";
                    animData.Code += "-inverse";
                }
                animData.AnimationSpeed *= Math.Abs(moveSpeedMul);
            }

            if (!animUtil.StartAnimation(animData))
            {
                animUtil.activeAnimationsByAnimCode[animData.Animation].AnimationSpeed = animData.AnimationSpeed;
            }
            else
            {
                activeSound?.Start();
                activeSound?.FadeIn(2, null);
            }

            Blockentity.MarkDirty(true);
        }

        public override void OnBlockUnloaded()
        {
            base.OnBlockUnloaded();

            activeSound?.Stop();
            activeSound?.Dispose();
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            base.GetBlockInfo(forPlayer, dsc);
        }


    }


}
