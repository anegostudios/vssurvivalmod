using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class BlockOre : Block
    {
        public override void OnBlockBroken(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1)
        {
            //base.OnBlockBroken(world, pos, byPlayer, dropQuantityMultiplier);
            // Ugly: copy pasted from Block so EnumHandled.Last prevents placement of empty rock

            EnumHandling handled = EnumHandling.NotHandled;

            foreach (BlockBehavior behavior in BlockBehaviors)
            {
                behavior.OnBlockBroken(world, pos, byPlayer, ref handled);
                if (handled == EnumHandling.Last) return;
            }

            if (handled == EnumHandling.PreventDefault) return;

            if (world.Side == EnumAppSide.Server && (byPlayer == null || byPlayer.WorldData.CurrentGameMode != EnumGameMode.Creative))
            {
                ItemStack[] drops = GetDrops(world, pos, byPlayer, dropQuantityMultiplier);

                if (drops != null)
                {
                    for (int i = 0; i < drops.Length; i++)
                    {
                        world.SpawnItemEntity(drops[i], new Vec3d(pos.X + 0.5, pos.Y + 0.5, pos.Z + 0.5), null);
                    }
                }

                if (Sounds?.Break != null)
                {
                    world.PlaySoundAt(Sounds.Break, pos.X, pos.Y, pos.Z, byPlayer);
                }
            }

            world.BlockAccessor.SetBlock(0, pos);


            if (byPlayer != null && byPlayer.WorldData.CurrentGameMode != EnumGameMode.Creative)
            {
                CollectibleObject coll = byPlayer?.InventoryManager.ActiveHotbarSlot.Itemstack?.Collectible;
                if (LastCodePart(1) == "flint" && (coll == null || coll.Tool != EnumTool.Pickaxe || coll.MiningTier < this.MiningTier))
                {
                    world.BlockAccessor.SetBlock(world.GetBlock(new AssetLocation("rock-" + LastCodePart())).BlockId, pos);
                }
            }

        }

    }
}
