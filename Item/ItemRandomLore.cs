using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

#nullable disable

namespace Vintagestory.GameContent
{
    public class ItemRandomLore : ItemBook, IGroundStoredParticleEmitter
    {

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            lorehintParticleProps = GuiStyle.LoreHintParticles.Clone(api.World);
            lorehintParticleProps.AddPos.Y = 0.2f;
        }

        public override void OnHeldInteractStart(ItemSlot itemslot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            var cat = itemslot.Itemstack.Attributes.GetString("category");

            if (byEntity.World.Side == EnumAppSide.Server && cat != null)
            {
                IPlayer byPlayer = null;
                if (byEntity is EntityPlayer) byPlayer = byEntity.World.PlayerByUid(((EntityPlayer)byEntity).PlayerUID);

                if (!(byPlayer is IServerPlayer)) return;

                TreeAttribute tree = new TreeAttribute();
                tree.SetString("playeruid", byPlayer?.PlayerUID);
                tree.SetString("category", cat);
                tree.SetItemstack("itemstack", itemslot.Itemstack.Clone());

                api.Event.PushEvent("loreDiscovery", tree);
            }

            base.OnHeldInteractStart(itemslot, byEntity, blockSel, entitySel, firstEvent, ref handling);
            handling = EnumHandHandling.PreventDefault;
        }


        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);

            var cat = inSlot.Itemstack.Attributes.GetString("category");
            if (cat != null)
            {
                dsc.Append(Lang.Get("loretype-" + cat));
            }
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


        private SimpleParticleProperties lorehintParticleProps;
        public bool ShouldSpawnGSParticles(IWorldAccessor world, ItemStack stack) => true;

        public void DoSpawnGSParticles(IAsyncParticleManager manager, BlockPos pos, Vec3f offset)
        {
            if (api.World.Rand.NextDouble() < /*0.05*/ 0.1) // for some reason this lad spawns less than e.g. the library resonator?
            {
                lorehintParticleProps.MinPos = pos.ToVec3d();
                manager.Spawn(lorehintParticleProps);
            }
        }
    }




}
