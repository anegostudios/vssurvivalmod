using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class ItemCheese : Item
    {
        public string Type => Variant["type"];
        public string Part => Variant["part"];

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            if (byEntity.Controls.ShiftKey && blockSel != null)
            {
                IPlayer byPlayer = null;
                if (byEntity is EntityPlayer) byPlayer = byEntity.World.PlayerByUid(((EntityPlayer)byEntity).PlayerUID);
                if (byPlayer == null) return;

                if (!byEntity.World.Claims.TryAccess(byPlayer, blockSel.Position, EnumBlockAccessFlags.BuildOrBreak))
                {
                    return;
                }


                Block placeblock = api.World.GetBlock(new AssetLocation("cheese"));
                BlockPos targetPos = blockSel.Position.AddCopy(blockSel.Face);
                string failureCode="";

                BlockSelection placeSel = blockSel.Clone();
                placeSel.Position.Add(blockSel.Face);

                if (placeblock.TryPlaceBlock(api.World, byPlayer, slot.Itemstack, placeSel, ref failureCode))
                {
                    BECheese bec = api.World.BlockAccessor.GetBlockEntity(targetPos) as BECheese;
                    if (bec != null)
                    {
                        slot.TakeOut(1);
                        slot.MarkDirty();
                    }

                    api.World.PlaySoundAt(placeblock.Sounds.Place, targetPos.X + 0.5, targetPos.InternalY, targetPos.Z + 0.5, byPlayer);

                    handling = EnumHandHandling.PreventDefault;
                } else
                {
                    (api as ICoreClientAPI)?.TriggerIngameError(this, failureCode, Lang.Get("placefailure-" + failureCode));
                }

                return;
            }

            base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
        }


        public override ItemStack OnTransitionNow(ItemSlot slot, TransitionableProperties props)
        {
            if (props.Type == EnumTransitionType.Ripen)
            {
                BlockPos pos = slot.Inventory.Pos;
                if (pos != null)
                {
                    Room room = api.ModLoader.GetModSystem<RoomRegistry>().GetRoomForPosition(pos);
                    int lightlevel = api.World.BlockAccessor.GetLightLevel(pos, EnumLightLevelType.OnlySunLight);

                    if (room.ExitCount > 0 && lightlevel < 2)
                    {
                        return new ItemStack(api.World.GetItem(new AssetLocation("cheese-blue-4slice")));
                    }
                }
            }

            return base.OnTransitionNow(slot, props);
        }
    }
}
