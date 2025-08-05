using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.GameContent
{
    public class BlockContainer : Block
    {
        public virtual float GetContainingTransitionModifierContained(IWorldAccessor world, ItemSlot inSlot, EnumTransitionType transType)
        {
            return 1;
        }

        public virtual float GetContainingTransitionModifierPlaced(IWorldAccessor world, BlockPos pos, EnumTransitionType transType)
        {
            return 1;
        }
        

        public virtual void SetContents(ItemStack containerStack, ItemStack[] stacks)
        {
            if (stacks == null || stacks.Length == 0 || stacks.All(x => x == null))
            {
                containerStack.Attributes.RemoveAttribute("contents");
                return;
            }

            TreeAttribute stacksTree = new TreeAttribute();
            for (int i = 0; i < stacks.Length; i++)
            {
                stacksTree[i + ""] = new ItemstackAttribute(stacks[i]);
            }

            containerStack.Attributes["contents"] = stacksTree;
        }

        public virtual ItemStack[] GetContents(IWorldAccessor world, ItemStack itemstack)
        {
            ITreeAttribute treeAttr = itemstack?.Attributes?.GetTreeAttribute("contents");
            if (treeAttr == null)
            {
                return ResolveUcontents(world, itemstack);
            }

            ItemStack[] stacks = new ItemStack[treeAttr.Count];
            foreach (var val in treeAttr)
            {
                ItemStack stack = (val.Value as ItemstackAttribute).value;
                if (stack != null) stack.ResolveBlockOrItem(world);

                if (int.TryParse(val.Key, out int index)) stacks[index] = stack;
            }

            return stacks;
        }

        public override bool Equals(ItemStack thisStack, ItemStack otherStack, params string[] ignoreAttributeSubTrees)
        {
            ResolveUcontents(api.World, thisStack);
            if (otherStack.Collectible is BlockContainer) ResolveUcontents(api.World, otherStack);

            return base.Equals(thisStack, otherStack, ignoreAttributeSubTrees);
        }

        protected ItemStack[] ResolveUcontents(IWorldAccessor world, ItemStack itemstack)
        {
            if (itemstack?.Attributes.HasAttribute("ucontents") == true)
            {
                List<ItemStack> stacks = new List<ItemStack>();

                var attrs = itemstack.Attributes["ucontents"] as TreeArrayAttribute;

                foreach (ITreeAttribute stackAttr in attrs.value)
                {
                    stacks.Add(CreateItemStackFromJson(stackAttr, world, Code.Domain));
                }
                ItemStack[] stacksAsArray = stacks.ToArray();
                SetContents(itemstack, stacksAsArray);
                itemstack.Attributes.RemoveAttribute("ucontents");

                return stacksAsArray;
            }
            else
            {
                return System.Array.Empty<ItemStack>();
            }
        }

        public virtual ItemStack CreateItemStackFromJson(ITreeAttribute stackAttr, IWorldAccessor world, string defaultDomain)
        {
            CollectibleObject collObj;
            var loc = AssetLocation.Create(stackAttr.GetString("code"), defaultDomain);
            if (stackAttr.GetString("type") == "item")
            {
                collObj = world.GetItem(loc);
            }
            else
            {
                collObj = world.GetBlock(loc);
            }

            ItemStack stack = new ItemStack(collObj, (int)stackAttr.GetDecimal("quantity", 1));
            var attr = (stackAttr["attributes"] as TreeAttribute)?.Clone();
            if (attr != null) stack.Attributes = attr;

            return stack;
        }


        public bool IsEmpty(ItemStack itemstack)
        {
            ITreeAttribute treeAttr = itemstack?.Attributes?.GetTreeAttribute("contents");

            if (treeAttr == null) return true;

            foreach (var val in treeAttr)
            {
                ItemStack stack = (val.Value as ItemstackAttribute).value;
                if (stack != null) return false;
            }

            return true;
        }


        public virtual ItemStack[] GetNonEmptyContents(IWorldAccessor world, ItemStack itemstack)
        {
            return GetContents(world, itemstack)?.Where(stack => stack != null).ToArray();
        }

        public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos)
        {
            ItemStack stack = base.OnPickBlock(world, pos);

            BlockEntityContainer bec = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityContainer;

            if (bec != null)
            {
                SetContents(stack, bec.GetContentStacks());
            }

            return stack;
        }

        public override void OnBlockBroken(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1)
        {
            bool preventDefault = false;
            foreach (BlockBehavior behavior in BlockBehaviors)
            {
                EnumHandling handled = EnumHandling.PassThrough;

                behavior.OnBlockBroken(world, pos, byPlayer, ref handled);
                if (handled == EnumHandling.PreventDefault) preventDefault = true;
                if (handled == EnumHandling.PreventSubsequent) return;
            }

            if (preventDefault) return;


            if (world.Side == EnumAppSide.Server && (byPlayer == null || byPlayer.WorldData.CurrentGameMode != EnumGameMode.Creative))
            {
                ItemStack[] drops = new ItemStack[] { OnPickBlock(world, pos) };

                for (int i = 0; i < drops.Length; i++)
                {
                    world.SpawnItemEntity(drops[i], pos, null);
                }

                world.PlaySoundAt(Sounds.GetBreakSound(byPlayer), pos, -0.5, byPlayer);
            }

            if (EntityClass != null)
            {
                BlockEntity entity = world.BlockAccessor.GetBlockEntity(pos);
                if (entity != null)
                {
                    entity.OnBlockBroken(byPlayer);
                }
            }

            world.BlockAccessor.SetBlock(0, pos);
        }


        public override TransitionState[] UpdateAndGetTransitionStates(IWorldAccessor world, ItemSlot inslot)
        {
            if (inslot is ItemSlotCreative) return base.UpdateAndGetTransitionStates(world, inslot);

            ItemStack[] stacks = GetContents(world, inslot.Itemstack);

            if (inslot.Itemstack.Attributes.GetBool("timeFrozen"))
            {
                foreach (var stack in stacks) stack?.Attributes.SetBool("timeFrozen", true);
                return null;
            }

            if (stacks != null)
            {
                for (int i = 0; i < stacks.Length; i++)
                {
                    var stack = stacks[i];
                    if (stack == null) continue;

                    ItemSlot dummySlot = GetContentInDummySlot(inslot, stack);
                    stack.Collectible.UpdateAndGetTransitionStates(world, dummySlot);
                    if (dummySlot.Itemstack == null)
                    {
                        stacks[i] = null;
                    }
                }
            }

            SetContents(inslot.Itemstack, stacks);

            return base.UpdateAndGetTransitionStates(world, inslot);
        }

        /// <summary>
        /// Puts supplied stack in a dummy slot that respects the spoilage rate of this container as well as the inventory this container resides in
        /// </summary>
        /// <param name="inslot"></param>
        /// <param name="itemstack"></param>
        /// <returns></returns>
        protected virtual ItemSlot GetContentInDummySlot(ItemSlot inslot, ItemStack itemstack)
        {
            ItemSlot dummySlot;
            DummyInventory dummyInv = new DummyInventory(api);
            dummySlot = new DummySlot(itemstack, dummyInv);
            dummySlot.MarkedDirty += () => { inslot.Inventory?.DidModifyItemSlot(inslot); return true; };

            dummyInv.OnAcquireTransitionSpeed += (transType, stack, mulByConfig) =>
            {
                float mul = mulByConfig;

                if (inslot.Inventory != null)
                {
                    mul = inslot.Inventory.InvokeTransitionSpeedDelegates(transType, stack, mulByConfig);
                }

                return mul * GetContainingTransitionModifierContained(api.World, inslot, transType);
            };

            

            return dummySlot;
        }


        public override void SetTemperature(IWorldAccessor world, ItemStack itemstack, float temperature, bool delayCooldown = true)
        {
            ItemStack[] stacks = GetContents(world, itemstack);

            if (stacks != null)
            {
                for (int i = 0; i < stacks.Length; i++)
                {
                    stacks[i]?.Collectible.SetTemperature(world, stacks[i], temperature, delayCooldown);
                }
            }

            base.SetTemperature(world, itemstack, temperature, delayCooldown);
        }


        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);
        }

        public string PerishableInfoCompactContainer(ICoreAPI api, ItemSlot inSlot)
        {
            var world = api.World;
            ItemStack[] stacks = GetNonEmptyContents(world, inSlot.Itemstack);
            DummyInventory dummyInv = new DummyInventory(api);
            ItemSlot slot = BlockCrock.GetDummySlotForFirstPerishableStack(api.World, stacks, null, dummyInv);
            dummyInv.OnAcquireTransitionSpeed += (transType, stack, mul) =>
            {
                float val = mul * GetContainingTransitionModifierContained(world, inSlot, transType);

                if (inSlot.Inventory != null) val *= inSlot.Inventory.GetTransitionSpeedMul(transType, inSlot.Itemstack);

                return val;
            };

            return BlockEntityShelf.PerishableInfoCompact(api, slot, 0, false).Replace("\r\n", "");
        }

        public override bool RequiresTransitionableTicking(IWorldAccessor world, ItemStack itemstack)
        {
            ItemStack[] stacks = GetNonEmptyContents(world, itemstack);
            for (int i = 0; i < stacks.Length; i++)
            {
                var props = stacks[i].Collectible.GetTransitionableProperties(world, stacks[i], null);
                if (props != null && props.Length > 0) return true;
            }
            return base.RequiresTransitionableTicking(world, itemstack);
        }
    }
}
