using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

namespace Vintagestory.GameContent
{
    public class ItemMedallion : Item, IAttachedListener
    {
        public void OnAttached(ItemSlot itemslot, int slotIndex, Entity toEntity, EntityAgent byEntity)
        {
            if (api.Side == EnumAppSide.Client) return;
            if (byEntity != null)
            {
                api.ModLoader.GetModSystem<ModSystemEntityOwnership>().ClaimOwnership(toEntity, byEntity);
            }
        }

        public void OnDetached(ItemSlot itemslot, int slotIndex, Entity fromEntity)
        {
            if (api.Side == EnumAppSide.Client) return;
            api.ModLoader.GetModSystem<ModSystemEntityOwnership>().RemoveOwnership(fromEntity);
        }
    }
}
