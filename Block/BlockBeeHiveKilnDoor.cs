using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.GameContent;

public class BlockBeeHiveKilnDoor : BlockGeneric
{
    public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel, ref string failureCode)
    {
        BlockPos pos = blockSel.Position;
        IBlockAccessor ba = world.BlockAccessor;

        if (ba.GetBlock(pos, BlockLayersAccess.Solid).Id == 0 && CanPlaceBlock(world, byPlayer, blockSel, ref failureCode))
        {
            return placeDoor(world, byPlayer, itemstack, blockSel, pos, ba);
        }

        return false;
    }

    public bool placeDoor(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel, BlockPos pos, IBlockAccessor ba)
    {
        ba.SetBlock(BlockId, pos);
        var behaviorDoor = ba.GetBlockEntity(pos)?.GetBehavior<BEBehaviorDoor>();
        var blockEntityBeeHiveKiln = behaviorDoor.Blockentity as BlockEntityBeeHiveKiln;
        // rotate door by 180° so it faces the player, needs to be done here since it uses BEBehaviorDoor which sets up the rotation
        // and the beehive klin rotates the other way round as other doors
        behaviorDoor.RotateYRad = BEBehaviorDoor.getRotateYRad(byPlayer, blockSel);
        behaviorDoor.RotateYRad += (behaviorDoor.RotateYRad == -1 * GameMath.PI) ? -1 * GameMath.PI : GameMath.PI;
        behaviorDoor.SetupRotationsAndColSelBoxes(true);

        blockEntityBeeHiveKiln.Orientation = BlockFacing.HorizontalFromAngle(behaviorDoor.RotateYRad - GameMath.PIHALF);
        blockEntityBeeHiveKiln.Init();
        var totalHoursHeatReceived = itemstack.Attributes.GetDouble("totalHoursHeatReceived");

        blockEntityBeeHiveKiln.TotalHoursHeatReceived = totalHoursHeatReceived;
        blockEntityBeeHiveKiln.TotalHoursLastUpdate = world.Calendar.TotalHours;

        if (world.Side == EnumAppSide.Server)
        {
            GetBehavior<BlockBehaviorDoor>().placeMultiblockParts(world, pos);
        }

        return true;
    }

    public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
    {
        BlockPos pos = blockSel.Position;

        if (byPlayer.WorldData.EntityControls.CtrlKey)
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
    public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer)
    {
        var bebk = world.BlockAccessor.GetBlockEntity<BlockEntityBeeHiveKiln>(selection.Position);
        if (bebk?.StructureComplete == false)
        {
            return new WorldInteraction[]
            {
                new WorldInteraction()
                {
                    ActionLangCode = "blockhelp-mulblock-struc-show",
                    HotKeyCodes = new string[] {"ctrl" },
                    MouseButton = EnumMouseButton.Right,
                },
                new WorldInteraction()
                {
                    ActionLangCode = "blockhelp-mulblock-struc-hide",
                    HotKeyCodes = new string[] {"ctrl", "shift" },
                    MouseButton = EnumMouseButton.Right,
                }
            };
        }

        return base.GetPlacedBlockInteractionHelp(world, selection, forPlayer);
    }
}
