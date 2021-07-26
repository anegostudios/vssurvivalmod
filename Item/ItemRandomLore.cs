using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vintagestory.API;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.ServerMods;

namespace Vintagestory.GameContent
{
    public class ItemRandomLore : Item
    {

        public override void OnHeldInteractStart(ItemSlot itemslot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            if (byEntity.World.Side != EnumAppSide.Server)
            {
                handling = EnumHandHandling.PreventDefault;
                return;
            }

            IPlayer byPlayer = null;
            if (byEntity is EntityPlayer) byPlayer = byEntity.World.PlayerByUid(((EntityPlayer)byEntity).PlayerUID);

            if (!(byPlayer is IServerPlayer)) return;

            TreeAttribute tree = new TreeAttribute();
            tree.SetString("playeruid", byPlayer?.PlayerUID);
            tree.SetString("category", itemslot.Itemstack.Attributes.GetString("category"));
            tree.SetItemstack("itemstack", itemslot.Itemstack.Clone());

            api.Event.PushEvent("loreDiscovery", tree);
            
            handling = EnumHandHandling.PreventDefault;
        }

        public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            /* if (byEntity.World is IClientWorldAccessor)
             {
                 ModelTransform tf = new ModelTransform();
                 tf.EnsureDefaultValues();

                 float offset = GameMath.Clamp(secondsUsed * 3, 0, 2f);

                 tf.Translation.Set(0, offset, offset / 8);
                 tf.Origin.Set(0.9f, -0.2f, 0.5f);
                 tf.Rotation.Set(0, 0, offset * 20);

                 byEntity.Controls.UsingHeldItemTransform = tf;
             }

             return secondsUsed < 2;*/
            return false;
        }


        public override bool OnHeldInteractCancel(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, EnumItemUseCancelReason cancelReason)
        {
            return true;
        }


        

        public override void OnHeldInteractStop(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            if (secondsUsed < 1.9) return;
        }


        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);

            dsc.Append(Lang.Get("loretype-" + inSlot.Itemstack.Attributes.GetString("category")));
        }

        public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot)
        {
            return new WorldInteraction[]
            {
                new WorldInteraction
                {
                    ActionLangCode = "heldhelp-addtojournal",
                    MouseButton = EnumMouseButton.Right
                }
            }.Append(base.GetHeldInteractionHelp(inSlot));
        }

    }




}
