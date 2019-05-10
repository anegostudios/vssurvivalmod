
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class BlockOre : Block
    {
        public string Grade
        {
            get
            {
                string part = FirstCodePart(1);
                if (part == "poor" || part == "medium" || part == "rich" || part == "bountiful") return part;
                return null;
            }
        }

        public string MotherRock
        {
            get
            {
                return LastCodePart();
            }
        }

        public string OreName
        {
            get
            {
                return LastCodePart(1);
            }
        }

        public string InfoText
        {
            get
            {
                StringBuilder dsc = new StringBuilder();
                if (Grade != null) dsc.AppendLine(Lang.Get("ore-grade-" + Grade));
                dsc.AppendLine(Lang.Get("ore-in-rock", Lang.Get("ore-" + OreName), Lang.Get("rock-" + MotherRock)));

                return dsc.ToString();
            }
        }


        public override void OnBlockBroken(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1)
        {
            //base.OnBlockBroken(world, pos, byPlayer, dropQuantityMultiplier);
            // Ugly: copy pasted from Block so EnumHandled.Last prevents placement of empty rock

            EnumHandling handled = EnumHandling.PassThrough;

            foreach (BlockBehavior behavior in BlockBehaviors)
            {
                behavior.OnBlockBroken(world, pos, byPlayer, ref handled);
                if (handled == EnumHandling.PreventSubsequent) return;
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

                world.PlaySoundAt(Sounds.GetBreakSound(byPlayer), pos.X, pos.Y, pos.Z, byPlayer);
            }

            world.BlockAccessor.SetBlock(0, pos);


            if (byPlayer != null && byPlayer.WorldData.CurrentGameMode != EnumGameMode.Creative)
            {
                CollectibleObject coll = byPlayer?.InventoryManager.ActiveHotbarSlot.Itemstack?.Collectible;
                if (LastCodePart(1) == "flint" && (coll == null || coll.MiningTier == 0))
                {
                    world.BlockAccessor.SetBlock(world.GetBlock(new AssetLocation("rock-" + LastCodePart())).BlockId, pos);
                }
            }

        }


        public override void GetHeldItemInfo(ItemStack stack, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(stack, dsc, world, withDebugInfo);
            dsc.AppendLine(InfoText);
        }

        public override string GetPlacedBlockInfo(IWorldAccessor world, BlockPos pos, IPlayer forPlayer)
        {
            return InfoText + "\n" + (LastCodePart(1) == "flint" ? Lang.Get("Break with bare hands to extract flint") + "\n" : "") + base.GetPlacedBlockInfo(world, pos, forPlayer);
        }



    }
}
