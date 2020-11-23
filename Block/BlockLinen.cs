using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class BlockLinen : BlockSimpleCoating
    {

        public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel, ref string failureCode)
        {
            BlockEntityBarrel beba = api.World.BlockAccessor.GetBlockEntity(blockSel.Position.AddCopy(blockSel.Face.Opposite)) as BlockEntityBarrel;
            if (beba != null) return false;


            return base.TryPlaceBlock(world, byPlayer, itemstack, blockSel, ref failureCode);
        }

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling)
        {
            if (blockSel != null)
            {
                BlockEntityBarrel beba = api.World.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityBarrel;
                if (beba != null && !beba.inventory[1].Empty)
                {
                    if (beba.inventory[1].Itemstack.Item?.Code?.Path == "cottagecheeseportion")
                    {
                        if (beba.inventory[1].StackSize < 25)
                        {
                            (api as ICoreClientAPI)?.TriggerIngameError(this, "notenough", Lang.Get("Need at least 25 litres to create a roll of cheese"));
                            handHandling = EnumHandHandling.PreventDefault;
                            return;
                        }

                        if (api.World.Side == EnumAppSide.Server)
                        {
                            ItemStack ccStack = beba.inventory[1].TakeOut(25);

                            BlockCheeseCurdsBundle block = api.World.GetBlock(new AssetLocation("curdbundle")) as BlockCheeseCurdsBundle;
                            ItemStack bundleStack = new ItemStack(block);
                            block.SetContents(bundleStack, ccStack);

                            slot.TakeOut(1);
                            slot.MarkDirty();

                            beba.MarkDirty(true);

                            if (!byEntity.TryGiveItemStack(bundleStack))
                            {
                                api.World.SpawnItemEntity(bundleStack, byEntity.Pos.XYZ.AddCopy(0, 0.5, 0));
                            }
                        }

                        handHandling = EnumHandHandling.PreventDefault;
                        return;
                    }
                }
            }

            base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handHandling);
        }
    }
}
