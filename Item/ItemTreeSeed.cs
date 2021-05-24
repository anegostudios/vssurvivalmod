using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class ItemTreeSeed : Item
    {
        WorldInteraction[] interactions;

        bool isMapleSeed;


        public override void OnLoaded(ICoreAPI api)
        {
            isMapleSeed = Variant["type"] == "maple" || Variant["type"] == "crimsonkingmaple";

            if (api.Side != EnumAppSide.Client) return;
            ICoreClientAPI capi = api as ICoreClientAPI;

            interactions = ObjectCacheUtil.GetOrCreate(api, "treeSeedInteractions", () =>
            {
                List<ItemStack> stacks = new List<ItemStack>();

                foreach (Block block in api.World.Blocks)
                {
                    if (block.Code == null || block.EntityClass == null) continue;
                    if (block.Fertility > 0)
                    {
                        stacks.Add(new ItemStack(block));
                    }
                }

                return new WorldInteraction[]
                {
                    new WorldInteraction()
                    {
                        ActionLangCode = "heldhelp-plant",
                        MouseButton = EnumMouseButton.Right,
                        HotKeyCode = "sneak",
                        Itemstacks = stacks.ToArray()
                    }
                };
            });
        }

        public override void OnBeforeRender(ICoreClientAPI capi, ItemStack itemstack, EnumItemRenderTarget target, ref ItemRenderInfo renderinfo)
        {
            base.OnBeforeRender(capi, itemstack, target, ref renderinfo);

            if (isMapleSeed && target == EnumItemRenderTarget.Ground)
            {
                EntityItem ei = (renderinfo.InSlot as EntityItemSlot).Ei;
                if (!ei.Collided && !ei.Swimming)
                {
                    renderinfo.Transform = renderinfo.Transform.Clone(); // dont override the original transform
                    renderinfo.Transform.Rotation.X = -90;
                    renderinfo.Transform.Rotation.Y = (float)(capi.World.ElapsedMilliseconds % 360.0) * 2f;
                    renderinfo.Transform.Rotation.Z = 0;
                }
            }
        }

        public override void OnHeldInteractStart(ItemSlot itemslot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling)
        {
            if (blockSel == null || !byEntity.Controls.Sneak)
            {
                base.OnHeldInteractStart(itemslot, byEntity, blockSel, entitySel, firstEvent, ref handHandling);
                return;
            }

            string treetype = Variant["type"];

            Block saplBlock = byEntity.World.GetBlock(AssetLocation.Create("sapling-" + treetype + "-free", Code.Domain));

            if (saplBlock != null)
            {
                IPlayer byPlayer = null;
                if (byEntity is EntityPlayer) byPlayer = byEntity.World.PlayerByUid(((EntityPlayer)byEntity).PlayerUID);

                blockSel = blockSel.Clone();
                blockSel.Position.Up();

                string failureCode = "";
                if (!saplBlock.TryPlaceBlock(api.World, byPlayer, itemslot.Itemstack, blockSel, ref failureCode))
                {
                    if (api is ICoreClientAPI capi && failureCode != null && failureCode != "__ignore__")
                    {
                        capi.TriggerIngameError(this, failureCode, Lang.Get("placefailure-" + failureCode));
                    }
                } else
                {
                    byEntity.World.PlaySoundAt(new AssetLocation("sounds/block/dirt1"), blockSel.Position.X + 0.5f, blockSel.Position.Y, blockSel.Position.Z + 0.5f, byPlayer);

                    ((byEntity as EntityPlayer)?.Player as IClientPlayer)?.TriggerFpAnimation(EnumHandInteract.HeldItemInteract);

                    if (byPlayer?.WorldData?.CurrentGameMode != EnumGameMode.Creative)
                    {
                        itemslot.TakeOut(1);
                        itemslot.MarkDirty();
                    }
                }

                handHandling = EnumHandHandling.PreventDefault;
            }
        }


        public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot)
        {
            return interactions.Append(base.GetHeldInteractionHelp(inSlot));
        }
    }
}
