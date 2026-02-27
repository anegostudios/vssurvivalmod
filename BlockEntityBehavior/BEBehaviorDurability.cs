using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;

#nullable disable

namespace Vintagestory.GameContent
{
    public class BEBehaviorDurability : BlockEntityBehavior
    {
        public int RemainingDurability;
        public int MaxDurability;

        public BEBehaviorDurability(BlockEntity blockentity) : base(blockentity)
        {
        }

        public override void Initialize(ICoreAPI api, JsonObject properties)
        {
            base.Initialize(api, properties);
        }

        public override void OnBlockPlaced(ItemStack byItemStack = null)
        {
            RemainingDurability = byItemStack.Collectible.GetRemainingDurability(byItemStack);
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);
            RemainingDurability = tree.GetInt("durability");
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            tree.SetInt("durability", RemainingDurability);
        }

        public void DamageBlock(int damage = 1)
        {
            RemainingDurability -= damage;
            if (RemainingDurability <= 0)
            {
                Api.World.PlaySoundAt(HeldSounds.ToolBreak, Pos, 0.5f);
                Api.World.BlockAccessor.BreakBlock(Pos, null, 0);
            }
            else
            {
                Blockentity.MarkDirty();
            }
        }

        public override void OnBlockBroken(IPlayer byPlayer = null)
        {
            if (RemainingDurability > 0)
            {
                ItemStack stack = new ItemStack(Block);
                stack.Collectible.SetDurability(stack, RemainingDurability);
                Api.World.SpawnItemEntity(stack, Pos);
            }
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            dsc.AppendLine(Lang.Get("Durability: {0}/{1}", RemainingDurability, Block.Durability));
        }
    }
}
