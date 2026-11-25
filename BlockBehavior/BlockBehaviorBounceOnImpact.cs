using System;
using System.Linq;
using Vintagestory.API;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent;

public class BlockBehaviorBounceOnImpact(Block block) : BlockBehavior(block)
{

    /// <summary>
    /// The sound made when an entity bounces on the block
    /// </summary>
    [DocumentAsJson("Optional")]
    public SoundAttributes? Sound { get; set; } = block.Sounds?.Break;

    /// <summary>
    /// The necessary speed to trigger the sound when bouncing
    /// </summary>
    [DocumentAsJson("Optional", "0.2")]
    public float SoundSpeed { get; set; } = 0.2f;

    /// <summary>
    /// The factor by which speed is multiplied when bouncing
    /// </summary>
    [DocumentAsJson("Optional", "0.8")]
    public float SpeedMultiplier { get; set; } = 0.8f;

    /// <summary>
    /// The maximum speed the entity can bounce at when hitting the block
    /// </summary>
    [DocumentAsJson("Optional", "0.5")]
    public float MaxSpeed { get; set; } = 0.5f;

    /// <summary>
    /// Which faces are bouncy when impacted
    /// </summary>
    [DocumentAsJson("Optional", "Up")]
    public BlockFacing[] BounceFaces { get; set; } = [BlockFacing.UP];

    public override void Initialize(JsonObject properties)
    {
        base.Initialize(properties);

        Sound = properties["sound"].AsObject<SoundAttributes?>(null, block.Code.Domain, true);
        SoundSpeed = properties["soundSpeed"].AsFloat(SoundSpeed);
        SpeedMultiplier = properties["speedMultiplier"].AsFloat(SpeedMultiplier);
        MaxSpeed = properties["maxSpeed"].AsFloat(MaxSpeed);
        BounceFaces = properties["bounceFaces"].AsArray<string>()?.OfType<string>().Select(BlockFacing.FromFirstLetter).ToArray() ?? BounceFaces;
    }

    public override void OnEntityCollide(IWorldAccessor world, Entity entity, BlockPos pos, BlockFacing facing, Vec3d collideSpeed, bool isImpact, ref EnumHandling handling)
    {
        if (isImpact && BounceFaces.Contains(facing))
        {
            handling = EnumHandling.PreventDefault;
            double absSpeed = Math.Abs(facing.IsAxisNS ? collideSpeed.Z : (facing.IsAxisWE ? collideSpeed.X : collideSpeed.Y));
            if (Sound != null && absSpeed >= SoundSpeed)
            {
                float speedMultiplier = (float)GameMath.Clamp(absSpeed * 2, 0, 1);

                world.PlaySoundAt(Sound.Value, entity, null, speedMultiplier);
            }

            switch (facing.Axis)
            {
                case EnumAxis.X:
                    entity.Pos.Motion.X = GameMath.Clamp(-entity.Pos.Motion.X * SpeedMultiplier, -MaxSpeed, MaxSpeed);
                    break;
                case EnumAxis.Y:
                    entity.Pos.Motion.Y = GameMath.Clamp(-entity.Pos.Motion.Y * SpeedMultiplier, -MaxSpeed, MaxSpeed);
                    break;
                case EnumAxis.Z:
                    entity.Pos.Motion.Y = GameMath.Clamp(-entity.Pos.Motion.Z * SpeedMultiplier, -MaxSpeed, MaxSpeed);
                    break;
            }
        }
    }

}
