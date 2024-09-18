using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent;

public class BlockBeeHiveKilnDoor : BlockGeneric
{
    public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel, ref string failureCode)
    {
        var placed = base.TryPlaceBlock(world, byPlayer, itemstack, blockSel, ref failureCode);
        if (placed)
        {
            var blockEntityBeeHiveKiln = world.BlockAccessor.GetBlockEntity<BlockEntityBeeHiveKiln>(blockSel.Position);
            var behaviorDoor = blockEntityBeeHiveKiln.GetBehavior<BEBehaviorDoor>();
            // rotate door by 180° so it faces the player, needs to be done here since it uses BEBehaviorDoor which sets up the rotation
            // and the beehive klin rotates the other way round as other doors
            behaviorDoor.RotateYRad += GameMath.PI;
            behaviorDoor.SetupRotationsAndColSelBoxes(false);

            blockEntityBeeHiveKiln.Orientation = BlockFacing.HorizontalFromAngle(behaviorDoor.RotateYRad - GameMath.PIHALF);
            blockEntityBeeHiveKiln.Init();
            var totalHoursHeatReceived = itemstack.Attributes.GetDouble("totalHoursHeatReceived");

            blockEntityBeeHiveKiln.TotalHoursHeatReceived = totalHoursHeatReceived;
            blockEntityBeeHiveKiln.TotalHoursLastUpdate = world.Calendar.TotalHours;
        }

        return placed;
    }

    public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
    {
        BlockPos pos = blockSel.Position;

        if (byPlayer.WorldData.EntityControls.Sprint)
        {
            if (world.BlockAccessor.GetBlockEntity(pos) is BlockEntityBeeHiveKiln besc)
            {
                besc.Interact(byPlayer);
                (byPlayer as IClientPlayer)?.TriggerFpAnimation(EnumHandInteract.HeldItemInteract);
                return true;
            }
        }

        return base.OnBlockInteractStart(world, byPlayer, blockSel);
    }

    public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1)
    {
        var itemStacks = base.GetDrops(world, pos, byPlayer, dropQuantityMultiplier);
        var blockEntityGroundStorage = world.BlockAccessor.GetBlockEntity<BlockEntityBeeHiveKiln>(pos);
        itemStacks[0].Attributes["totalHoursHeatReceived"] = new DoubleAttribute(blockEntityGroundStorage.TotalHoursHeatReceived);

        return itemStacks;
    }
}
