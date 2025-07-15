using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.GameContent
{

    public class ItemMechHelper : Item
    {

    }

    public class EntityMechHelper : EntityAgent
    {
        public override void OnInteract(EntityAgent byEntity, ItemSlot slot, Vec3d hitPosition, EnumInteractMode mode)
        {
            base.OnInteract(byEntity, slot, hitPosition, mode);
        }

    }
}
