using System;
using System.Collections.Generic;
using System.Text;
using Cairo;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class ItemDough : Item
    {
        ItemStack[] tableStacks;

        public override void OnLoaded(ICoreAPI api)
        {
            List<ItemStack> tableStacks = new List<ItemStack>();
            foreach (CollectibleObject obj in api.World.Collectibles)
            {
                if (obj is Block && (obj as Block).Attributes?.IsTrue("pieFormingSurface") == true)
                {
                    tableStacks.Add(new ItemStack(obj));
                }
            }

            this.tableStacks = tableStacks.ToArray();
        }


        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            if (blockSel != null)
            {
                var block = api.World.BlockAccessor.GetBlock(blockSel.Position);
                if (block.Attributes?.IsTrue("pieFormingSurface") == true)
                {
                    if (slot.StackSize >= 2)
                    {
                        BlockPie blockform = api.World.GetBlock(new AssetLocation("pie-raw")) as BlockPie;
                        blockform.TryPlacePie(byEntity, blockSel);
                    } else
                    {
                        ICoreClientAPI capi = api as ICoreClientAPI;
                        if (capi != null) capi.TriggerIngameError(this, "notpieable", Lang.Get("Need at least 2 dough"));
                    }

                    handling = EnumHandHandling.PreventDefault;
                    return;
                }
            }

            base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
        }

        public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot)
        {
            return new WorldInteraction[] {
                new WorldInteraction()
                {
                    ActionLangCode = "heldhelp-makepie",
                    Itemstacks = tableStacks,
                    HotKeyCode = "sneak",
                    MouseButton = EnumMouseButton.Right,
                }
            }.Append(base.GetHeldInteractionHelp(inSlot));
        }
    }
}
