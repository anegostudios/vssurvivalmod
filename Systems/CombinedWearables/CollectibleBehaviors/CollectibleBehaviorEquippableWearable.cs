using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent;

public class CollectibleBehaviorEquippableWearable : CollectibleBehavior
{
    public CollectibleBehaviorEquippableWearable(CollectibleObject collObj) : base(collObj)
    {
    }

    public override void OnLoaded(ICoreAPI api)
    {
        wearable = collObj.GetCollectibleInterface<IWearableStatsSupplier>();
    }

    public override void Initialize(JsonObject properties)
    {
        base.Initialize(properties);

        excludedSelectedEntities = properties["excludedSelectedEntityTags"]?.AsObject<TagSetFast>() ?? TagSetFast.Empty;
        excludedSelectedBlocks = properties["excludedSelectedBlockTags"]?.AsObject<TagSet>() ?? TagSet.Empty;
    }

    public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection? blockSel, EntitySelection? entitySel, bool firstEvent, ref EnumHandHandling handHandling, ref EnumHandling handling)
    {
        if (wearable == null || byEntity.Controls.ShiftKey) return;

        if (entitySel?.Entity != null && entitySel.Entity.Tags.Overlaps(excludedSelectedEntities))
        {
            return;
        }

        if (blockSel?.Block != null && blockSel.Block.Tags.Overlaps(excludedSelectedBlocks))
        {
            return;
        }

        if (blockSel != null)
        {
            // Don't dress one-self if looking at a display or mannequin
            Block block = byEntity.Api.World.BlockAccessor.GetBlock(blockSel.Position);
            BlockPos worldPosition = blockSel.Position;
            Vec3i? relativePosistion = block.GetBehavior<BlockBehaviorMultiblock>()?.ControllerPositionRel;
            if (relativePosistion != null)
            {
                worldPosition = worldPosition.AddCopy(relativePosistion);
            }
            BlockEntity blockEntity = byEntity.Api.World.BlockAccessor.GetBlockEntity(worldPosition);
            if (blockEntity?.Behaviors.Any(bh => bh is BEBehaviorMannequin or BEBehaviorDisplay) == true)
            {
                return;
            }
        }

        IPlayer? byPlayer = (byEntity as EntityPlayer)?.Player;
        if (byPlayer == null) return;

        IInventory? characterInventory = byPlayer.InventoryManager.GetOwnInventory(GlobalConstants.characterInvClassName);
        if (characterInventory == null) return;

        EnumCharacterDressType dressType = wearable.GetDressType(slot);

        if (dressType == EnumCharacterDressType.Unknown) return;

        if (characterInventory[(int)dressType]?.TryFlipWith(slot) == true)
        {
            handHandling = EnumHandHandling.PreventDefault;
            handling = EnumHandling.PreventSubsequent;
        }
    }

    public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot, ref EnumHandling handling)
    {
        handling = EnumHandling.Handled;
        return [
            new WorldInteraction()
            {
                ActionLangCode = "heldhelp-dress",
                MouseButton = EnumMouseButton.Right,
            }
        ];
    }

    protected IWearableStatsSupplier? wearable;
    protected TagSet excludedSelectedBlocks;
    protected TagSetFast excludedSelectedEntities;
}
