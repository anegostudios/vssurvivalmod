using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace Vintagestory.GameContent
{
    public class ItemNpcGuideStick : Item
    {
        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            handling = EnumHandHandling.PreventDefault;

            if (blockSel == null) return;

            var pos = blockSel.FullPosition;
            (api as ICoreServerAPI)?.ChatCommands.ExecuteUnparsed
                (string.Format("/npc exec nav ={0} ={1} ={2} run 0.006 1.8", pos.X, pos.Y, pos.Z),
                new TextCommandCallingArgs() { Caller = new Caller() { Player = (byEntity as EntityPlayer).Player } }
            );
        }
    }
}
