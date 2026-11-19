using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;


namespace Vintagestory.GameContent
{
    public class CollectibleBehaviorDropOnBreak : CollectibleBehavior
    {
        public CollectibleBehaviorDropOnBreak(CollectibleObject collObj) : base(collObj)
        {
        }



        public BlockDropItemStack[] Drops { get; set; } = [];

        public bool TryGiveItem { get; set; } = true;



        public override void Initialize(JsonObject properties)
        {
            base.Initialize(properties);

            Drops = properties["drops"]?.AsObject<BlockDropItemStack[]>() ?? [];
            TryGiveItem = properties["trygiveitem"]?.AsBool(TryGiveItem) ?? TryGiveItem;
        }

        public override void OnDestroyItem(IWorldAccessor world, Entity byEntity, ItemSlot itemSlot, ref EnumHandling bhHandling)
        {
            List<ItemStack> stacksToDrop = [];

            foreach (BlockDropItemStack drop in Drops)
            {
                drop.Resolve(world, "CollectibleBehaviorDropOnBreak", new());

                ItemStack? stack = drop.GetNextItemStack();

                if (stack == null || stack.StackSize == 0) continue;

                if (stack.Collectible is IResolvableCollectible resolvableCollectible)
                {
                    DummySlot slot = new(stack);
                    resolvableCollectible.Resolve(slot, world);
                    stack = slot.Itemstack;

                    if (stack == null || stack.StackSize == 0) continue;
                }

                while (stack.StackSize > stack.Collectible.MaxStackSize)
                {
                    ItemStack overflow = stack.GetEmptyClone();
                    overflow.StackSize = stack.Collectible.MaxStackSize;
                    stack.StackSize -= stack.Collectible.MaxStackSize;
                    stacksToDrop.Add(overflow);
                }

                stacksToDrop.Add(stack);
                if (drop.LastDrop) break;
            }

            BlockPos positionToDrop = byEntity.Pos.AsBlockPos;
            foreach (ItemStack stack in stacksToDrop)
            {
                if (TryGiveItem && byEntity is EntityPlayer)
                {
                    if (!byEntity.TryGiveItemStack(stack))
                    {
                        world.SpawnItemEntity(stack, positionToDrop);
                    }
                }
                else
                {
                    world.SpawnItemEntity(stack, positionToDrop);
                }
            }
        }
    }
}
