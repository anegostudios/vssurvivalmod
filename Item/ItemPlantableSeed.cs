using System;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class ItemPlantableSeed : Item
    {

        public override bool OnHeldInteractStart(IItemSlot itemslot, IEntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            if (blockSel == null) return false;

            BlockPos pos = blockSel.Position;

            string lastCodePart = itemslot.Itemstack.Collectible.LastCodePart();

            BlockEntity be = byEntity.World.BlockAccessor.GetBlockEntity(pos);
            if (be is BlockEntityFarmland)
            {
                Block cropBlock = byEntity.World.GetBlock(CodeWithPath("crop-" + lastCodePart + "-1"));
                if (cropBlock == null) return false;

                IPlayer byPlayer = null;
                if (byEntity is IEntityPlayer) byPlayer = byEntity.World.PlayerByUid(((IEntityPlayer)byEntity).PlayerUID);

                bool planted = ((BlockEntityFarmland)be).TryPlant(cropBlock);
                if (planted)
                {
                    byEntity.World.PlaySoundAt(new AssetLocation("sounds/block/plant"), pos.X, pos.Y, pos.Z, byPlayer);

                    if (byPlayer?.WorldData?.CurrentGameMode != EnumGameMode.Creative)
                    {
                        itemslot.TakeOut(1);
                        itemslot.MarkDirty();
                    }
                }

                return planted;
            }

            return false;
        }

        public override void GetHeldItemInfo(ItemStack stack, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(stack, dsc, world, withDebugInfo);

            Block cropBlock = world.GetBlock(CodeWithPath("crop-" + stack.Collectible.LastCodePart() + "-1"));
            if (cropBlock == null || cropBlock.CropProps == null) return;

            dsc.AppendLine("Required Nutrient: " + cropBlock.CropProps.RequiredNutrient);
            dsc.AppendLine("Nutrient Consumption: " + cropBlock.CropProps.NutrientConsumption);
            dsc.AppendLine("Growth Time: " + Math.Round(cropBlock.CropProps.TotalGrowthDays, 1) + " days");
        }
    }
}
