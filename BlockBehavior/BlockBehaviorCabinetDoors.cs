using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent;

public class BlockBehaviorCabinetDoors : StrongBlockBehavior
{
    public Cuboidf[]? DoorSelectionBoxOpened;
    public Cuboidf[]? DoorSelectionBoxClosed;
    public string?[]? DoorElements;

    public BlockBehaviorCabinetDoors(Block block) : base(block)
    {
    }

    public override void Initialize(JsonObject properties)
    {
        base.Initialize(properties);
        DoorSelectionBoxOpened = properties["selectionBoxesOpened"].AsObject<Cuboidf[]>();
        DoorSelectionBoxClosed = properties["selectionBoxesClosed"].AsObject<Cuboidf[]>();


        if (DoorSelectionBoxClosed == null) throw new NullReferenceException("selectionBoxesClosed cannot be null! Block code " + block.Code);
    }

    public override Cuboidf[] GetSelectionBoxes(IBlockAccessor blockAccessor, BlockPos pos, ref EnumHandling handled)
    {
        handled = EnumHandling.Handled;
        var be = blockAccessor.GetBlockEntity(pos);
        if (be == null) return base.GetSelectionBoxes(blockAccessor, pos, ref handled);

        return be.GetBehavior<BEBehaviorCabinetDoors>().GetSelectionBoxes();
    }

    public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer, ref EnumHandling handling)
    {
        if (selection.SelectionBoxId == null)
        {
            var doorStacks = ObjectCacheUtil.GetOrCreate(world.Api, "cabinetDoors", () =>
            {
                List<ItemStack> stacks = new List<ItemStack>();
                foreach (var collObj in world.Collectibles)
                {
                    if (collObj.Code.PathStartsWith("cabinetdoor"))
                    {
                        stacks.Add(new ItemStack(collObj));
                    }
                }
                return stacks.ToArray();
            });

            return base.GetPlacedBlockInteractionHelp(world, selection, forPlayer, ref handling).Append(new WorldInteraction()
            {
                ActionLangCode = "addcabinetdoors",
                HotKeyCode = "sprint",
                MouseButton = EnumMouseButton.Right,
                Itemstacks = doorStacks
            });
        }

        return base.GetPlacedBlockInteractionHelp(world, selection, forPlayer, ref handling);
    }

    public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, ref float dropChanceMultiplier, ref EnumHandling handling)
    {
        if (block.GetBEBehavior<BEBehaviorCabinetDoors>(pos)?.DoorStack is { } doorStack)
        {
            handling = EnumHandling.PreventDefault;

            return [doorStack];
        }

        return base.GetDrops(world, pos, byPlayer, ref dropChanceMultiplier, ref handling);
    }
}
