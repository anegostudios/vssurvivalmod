using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;


namespace Vintagestory.GameContent
{
    /// <summary>
    /// Collectible behavior that flips between items on hotkey inside character inventory
    /// </summary>
    public class CollectibleBehaviorFlippableArmorBit : CollectibleBehavior, IFlippableWearable
    {
        public CollectibleBehaviorFlippableArmorBit(CollectibleObject collObj) : base(collObj)
        {
        }



        public AssetLocation? FlipToItemCode { get; set; }

        public AssetLocation? UnflipToItemCode { get; set; }

        public AssetLocation? FlipSound { get; set; }

        public string? FlipAnimation { get; set; }



        public override void Initialize(JsonObject properties)
        {
            base.Initialize(properties);

            FlipToItemCode = properties["flipToItemCode"].AsString() ?? null;
            UnflipToItemCode = properties["unflipToItemCode"].AsString() ?? null;
            FlipSound = properties["flipSound"].AsString() ?? null;
            FlipAnimation = properties["flipAnimation"].AsString() ?? null;
        }

        public virtual bool Flip(Entity wearer, ItemSlot armorBitSlot)
        {
            if (FlipToItemCode == null || armorBitSlot.Itemstack == null) return false;

            if (wearer.Api is ICoreServerAPI serverApi)
            {
                serverApi.World.PlaySoundAt(FlipSound, wearer);
                if (FlipAnimation != null)
                {
                    wearer.StartAnimation(FlipAnimation);
                }
            }

            return ChangeItemCode(wearer, armorBitSlot, FlipToItemCode);
        }

        public virtual bool Unflip(Entity wearer, ItemSlot armorBitSlot)
        {
            if (UnflipToItemCode == null || armorBitSlot.Itemstack == null) return false;

            return ChangeItemCode(wearer, armorBitSlot, UnflipToItemCode);
        }

        public virtual bool ChangeItemCode(Entity wearer, ItemSlot armorBitSlot, AssetLocation toCode)
        {
            if (armorBitSlot.Itemstack == null) return false;

            ItemStack oldStack = armorBitSlot.TakeOut(1);

            JsonItemStack newJsonStack = JsonItemStack.FromItemStack(wearer.Api.World, oldStack);

            newJsonStack.Code = toCode.Clone();

            newJsonStack.Resolve(wearer.Api.World, "CollectibleBehaviorFlippableArmorBit.Flip");

            DummySlot dummySlot = new(newJsonStack.ResolvedItemStack);

            int transfered = dummySlot.TryPutInto(wearer.Api.World, armorBitSlot);

            if (transfered <= 0)
            {
                if (!wearer.TryGiveItemStack(dummySlot.Itemstack))
                {
                    wearer.World.SpawnItemEntity(dummySlot.Itemstack, wearer.Pos.AsBlockPos);
                }
            }

            return true;
        }
    }
}
