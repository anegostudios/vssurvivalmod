using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class BlockContainer : Block
    {
        public virtual void SetContents(ItemStack containerStack, ItemStack[] stacks)
        {
            TreeAttribute stacksTree = new TreeAttribute();
            for (int i = 0; i < stacks.Length; i++)
            {
                stacksTree[i + ""] = new ItemstackAttribute(stacks[i]);
            }

            containerStack.Attributes["contents"] = stacksTree;
        }

        public virtual ItemStack[] GetContents(IWorldAccessor world, ItemStack itemstack)
        {
            List<ItemStack> stacks = new List<ItemStack>();
            ITreeAttribute treeAttr = itemstack.Attributes.GetTreeAttribute("contents");
            if (treeAttr == null) return null;

            foreach (var val in treeAttr)
            {
                ItemStack stack = (val.Value as ItemstackAttribute).value;
                if (stack == null) continue;

                stack.ResolveBlockOrItem(world);
                stacks.Add(stack);
            }

            return stacks.ToArray();
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
            if (world.Side == EnumAppSide.Server && (byPlayer == null || byPlayer.WorldData.CurrentGameMode != EnumGameMode.Creative))
            {
                ItemStack[] drops = new ItemStack[] { OnPickBlock(world, pos) };

                for (int i = 0; i < drops.Length; i++)
                {
                    world.SpawnItemEntity(drops[i], new Vec3d(pos.X + 0.5, pos.Y + 0.5, pos.Z + 0.5), null);
                }

                world.PlaySoundAt(Sounds.GetBreakSound(byPlayer), pos.X, pos.Y, pos.Z, byPlayer);
            }

            if (EntityClass != null)
            {
                BlockEntity entity = world.BlockAccessor.GetBlockEntity(pos);
                if (entity != null)
                {
                    entity.OnBlockBroken();
                }
            }

            world.BlockAccessor.SetBlock(0, pos);
        }
        
    }
}
