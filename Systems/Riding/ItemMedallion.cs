using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

#nullable disable

namespace Vintagestory.GameContent;

public class ItemMedallion : Item, IAttachedListener
{
    public void OnAttached(ItemSlot itemslot, int slotIndex, Entity toEntity, EntityAgent byEntity)
    {
        if (api.Side == EnumAppSide.Client) return;
        if (byEntity == null || !toEntity.Alive) return;

        api.ModLoader.GetModSystem<ModSystemEntityOwnership>().ClaimOwnership(toEntity, byEntity);
    }

    public void OnDetached(ItemSlot itemslot, int slotIndex, Entity fromEntity, EntityAgent byEntity)
    {
        if (api.Side == EnumAppSide.Client) return;
        
        api.ModLoader.GetModSystem<ModSystemEntityOwnership>().RemoveOwnership(fromEntity);
    }
}
