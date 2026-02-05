using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace Vintagestory.GameContent
{
    public class CollectibleBehaviorGroundStoredHeldBag : CollectibleBehavior, IContainedInteractable
    {
        public string DialogTitleLangCode { get; set; } = "";
        public SoundAttributes? OpenSound { get; set; }
        public SoundAttributes? CloseSound { get; set; }

        public CollectibleBehaviorGroundStoredHeldBag(CollectibleObject collObj) : base(collObj)
        {

        }

        public override void Initialize(JsonObject properties)
        {
            base.Initialize(properties);

            DialogTitleLangCode = properties["dialogTitleLangCode"].AsString(collObj.GetHeldItemName(null));
            OpenSound = properties["openSound"].AsObject<SoundAttributes?>(null, collObj.Code.Domain, true);
            CloseSound = properties["closeSound"].AsObject<SoundAttributes?>(null, collObj.Code.Domain, true);
        }

        public virtual bool OnContainedInteractStart(BlockEntityContainer be, ItemSlot slot, IPlayer byPlayer, BlockSelection blockSel)
        {
            var bebh = be.GetBehavior<BEBehaviorContainedBagInventory>();
            
            if (bebh == null || !byPlayer.Entity.Controls.CtrlKey) return false;
            var bagInv = bebh.BagInventories[slot.Inventory.GetSlotId(slot)];

            if (bagInv.TryLoadBagInv(slot, this)) bagInv.OpenHeldBag(byPlayer);

            return true;
        }

        public bool OnContainedInteractStep(float secondsUsed, BlockEntityContainer be, ItemSlot slot, IPlayer byPlayer, BlockSelection blockSel)
        {
            return false;
        }
        public void OnContainedInteractStop(float secondsUsed, BlockEntityContainer be, ItemSlot slot, IPlayer byPlayer, BlockSelection blockSel)
        {

        }

        public WorldInteraction[] GetContainedInteractionHelp(BlockEntityContainer be, ItemSlot slot, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (be.GetBehavior<BEBehaviorContainedBagInventory>() == null) return [];

            return
            [
                new ()
                {
                    ActionLangCode = "blockhelp-chest-open",
                    MouseButton = EnumMouseButton.Right,
                    HotKeyCode = "ctrl"
                }
            ];
        }
    }
}
