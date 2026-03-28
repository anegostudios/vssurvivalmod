using System;
using Vintagestory.API;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.GameContent
{
    /// <summary>
    /// Allows a block to be used as a container for milking an entity. Must be on a block that has the "BlockLiquidContainerBase" class.
    /// Uses the code "MilkingContainer", and has no properties.
    /// </summary>
    /// <example><code lang="json">
    ///"behaviors": [
	///	{
	///		"name": "MilkingContainer"
	///	}
	///]
    /// </code></example>
    [DocumentAsJson]
    public class BlockBehaviorMilkingContainer : BlockBehavior
    {
        ICoreAPI api;
        BlockLiquidContainerBase lcblock;

        public BlockBehaviorMilkingContainer(Block block) : base(block) { }

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            this.api = api;

            lcblock = block as BlockLiquidContainerBase;
            if (lcblock == null)
            {
                throw new InvalidOperationException(string.Format("Block with code {0} has behavior MilkingContainer, but its block class does not inherit from BlockLiquidContainerBase, which is required", block.Code));
            }
        }

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling, ref EnumHandling handling)
        {
            if (entitySel?.Entity.GetBehavior<EntityBehaviorMilkable>() is not { } bh) return;

            if (lcblock.GetContent(slot.Itemstack) != null)
            {
                (api as ICoreClientAPI)?.TriggerIngameError(this, "useemptybucket", Lang.Get("Use an empty bucket for milking"));
                return;
            }

            if (firstEvent) bh.TryBeginMilking(); // We don't want to start milking again if we fail

            handling = EnumHandling.PreventDefault;
            handHandling = EnumHandHandling.PreventDefault;
        }

        public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandling handling)
        {
            if (entitySel?.Entity.GetBehavior<EntityBehaviorMilkable>() is not { } bh) return false;

            IPlayer player = (byEntity as EntityPlayer)?.Player;
            if (player == null) return false;

            if (!bh.CanContinueMilking(player, secondsUsed)) return false;

            handling = EnumHandling.PreventDefault;

            return api.Side == EnumAppSide.Server || secondsUsed < 3;
        }

        public override void OnHeldInteractStop(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandling handling)
        {
            if (secondsUsed <= 2.95f || entitySel?.Entity.GetBehavior<EntityBehaviorMilkable>() is not { } bh) return;

            bh.MilkingComplete(slot, byEntity);
            handling = EnumHandling.PreventDefault;
        }

    }
}
