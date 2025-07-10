using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

#nullable disable

namespace Vintagestory.GameContent
{
    public class ItemJournalEntry : Item
    {

        public override void OnHeldInteractStart(ItemSlot itemslot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            if (byEntity.World.Side != EnumAppSide.Server)
            {
                handling = EnumHandHandling.PreventDefault;
                return;
            }

            IPlayer byPlayer = (byEntity.World as EntityPlayer)?.Player;
            if (byPlayer == null) return;

            try
            {
                JournalEntry entry = this.Attributes["journalentry"].AsObject<JournalEntry>();
                this.api.ModLoader.GetModSystem<ModJournal>().AddOrUpdateJournalEntry(byPlayer as IServerPlayer, entry);

                itemslot.TakeOut(1);
                itemslot.MarkDirty();

                handling = EnumHandHandling.PreventDefault;

                byEntity.World.PlaySoundAt(new AssetLocation("sounds/effect/writing"), byEntity, byPlayer);

            } catch (Exception e)
            {
                byEntity.World.Logger.Error("Failed adding journal entry.");
                byEntity.World.Logger.Error(e);
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
    }
}
