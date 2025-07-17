using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace Vintagestory.GameContent;

public class BEBehaviorDoorBarLock : BlockEntityBehavior
{
    public bool IsLocked { get; set; } = true;

    public BEBehaviorDoorBarLock(BlockEntity blockentity) : base(blockentity)
    {
    }

    public override void Initialize(ICoreAPI api, JsonObject properties)
    {
        base.Initialize(api, properties);
        if (IsLocked)
        {
            var beBehaviorDoor = Blockentity.GetBehavior<BEBehaviorDoor>();
            var easingSpeed = Block.Attributes?["easingSpeed"].AsFloat(10) ?? 10;
            var animMeta = new AnimationMetaData() { Animation = "lock", Code = "lock", EaseInSpeed = 10, EaseOutSpeed = easingSpeed,AnimationSpeed = 0.6f};
            beBehaviorDoor.animUtil.StartAnimation(animMeta);
        }
    }

    public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
    {
        base.FromTreeAttributes(tree, worldAccessForResolve);
        IsLocked = tree.GetBool("isLocked", true);

    }

    public override void ToTreeAttributes(ITreeAttribute tree)
    {
        base.ToTreeAttributes(tree);

        tree.SetBool("isLocked", IsLocked);
    }

    public bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ref EnumHandling handling)
    {
        var beBehaviorDoor = Blockentity.GetBehavior<BEBehaviorDoor>();
        var rot = beBehaviorDoor.RotateYRad;

        var dx = blockSel.Position.X + blockSel.HitPosition.X - byPlayer.Entity.Pos.X;
        var dz = blockSel.Position.Z + blockSel.HitPosition.Z - byPlayer.Entity.Pos.Z;
        var angleHor = (float)Math.Atan2(dx, dz);
        var relAngel = GameMath.Mod(angleHor - rot, GameMath.TWOPI);

        if (IsLocked && relAngel > GameMath.PIHALF && relAngel < 3 * GameMath.PIHALF)
        {
            Api.Logger.Notification("open");
            beBehaviorDoor.animUtil.StopAnimation("lock");

            IsLocked = false;
            Blockentity.MarkDirty(true);
        }
        else if (IsLocked)
        {
            if (Api is ICoreClientAPI capi)
            {
                capi.TriggerIngameError(this, "doorBarLocked", Lang.Get("ingameerror-doorbarlocked"));
            }
        }

        if (IsLocked)
        {
            handling = EnumHandling.PreventSubsequent;
        }

        return !IsLocked;
    }
}
