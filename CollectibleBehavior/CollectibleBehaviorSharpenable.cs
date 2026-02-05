using System.Reflection.Metadata;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
#nullable disable

namespace Vintagestory.GameContent
{
    public class CollectibleBehaviorSharpenable : CollectibleBehavior
    {
        public override string GetHeldTpUseAnimation(ItemSlot activeHotbarSlot, Entity forEntity, ref EnumHandling bhHandling)
        {
            var blockSel = (forEntity as EntityPlayer)?.BlockSelection;
            if (blockSel != null)
            {
                var begw = api.World.BlockAccessor.GetBlockEntity<BlockEntityGrindingWheel>(blockSel.Position);
                if (begw != null && begw.PlayersGrinding.ContainsKey((forEntity as EntityPlayer).PlayerUID))
                {
                    bhHandling = EnumHandling.PreventDefault;
                    return "bladegrinding";
                }
            }
            return base.GetHeldTpUseAnimation(activeHotbarSlot, forEntity, ref bhHandling);
        }

        public CollectibleBehaviorSharpenable(CollectibleObject collObj) : base(collObj)
        {
        }

        ICoreAPI api;
        public override void OnLoaded(ICoreAPI api)
        {
            this.api = api;
        }

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling, ref EnumHandling handling)
        {
            if (blockSel == null) return;
            var begw = api.World.BlockAccessor.GetBlockEntity<BlockEntityGrindingWheel>(blockSel.Position);
            if (begw == null) return;

            if (begw.OnInteractStart((byEntity as EntityPlayer).Player, blockSel))
            {
                handling = EnumHandling.Handled;
                handHandling = EnumHandHandling.Handled;
                return;
            }
        }

        public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandling handling)
        {
            var begw = api.World.BlockAccessor.GetBlockEntity<BlockEntityGrindingWheel>(blockSel.Position);
            if (begw == null) return false;

            if (begw.OnInteractStep(secondsUsed, (byEntity as EntityPlayer).Player, blockSel))
            {
                handling = EnumHandling.Handled;
                return true;
            }

            return false;
        }

        public override bool OnHeldInteractCancel(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, EnumItemUseCancelReason cancelReason, ref EnumHandling handled)
        {
            if (blockSel == null) return false;
            var begw = api.World.BlockAccessor.GetBlockEntity<BlockEntityGrindingWheel>(blockSel.Position);
            if (begw == null) return false;
            begw.OnInteractStop(secondsUsed, (byEntity as EntityPlayer).Player, blockSel);
            handled = EnumHandling.Handled;
            return true;
        }

        public override void OnHeldInteractStop(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandling handling)
        {
            var begw = api.World.BlockAccessor.GetBlockEntity<BlockEntityGrindingWheel>(blockSel.Position);
            if (begw == null) return;
            begw.OnInteractStop(secondsUsed, (byEntity as EntityPlayer).Player, blockSel);
            handling = EnumHandling.Handled;
        }


    }
}
