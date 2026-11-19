using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

#nullable disable

namespace Vintagestory.GameContent
{
    public class EntityStrawDummy : EntityHumanoid
    {
        public override void OnInteract(EntityAgent byEntity, ItemSlot slot, Vec3d hitPosition, EnumInteractMode mode)
        {
            if (!Api.World.Claims.TryAccess(((EntityPlayer)byEntity).Player, Pos.AsBlockPos, EnumBlockAccessFlags.Use) || !Alive || World.Side == EnumAppSide.Client || mode == 0)
            {
                base.OnInteract(byEntity, slot, hitPosition, mode);
                return;
            }

            string owneruid = WatchedAttributes.GetString("ownerUid", null);
            string agentUid = (byEntity as EntityPlayer)?.PlayerUID;

            if (agentUid != null &&
                (owneruid == null || owneruid == "" || owneruid == agentUid) &&
                byEntity.Controls.ShiftKey)
            {
                ItemStack stack = new ItemStack(byEntity.World.GetItem(new AssetLocation("strawdummy")));
                if (!byEntity.TryGiveItemStack(stack))
                {
                    byEntity.World.SpawnItemEntity(stack, Pos.XYZ);
                }
                byEntity.World.Logger.Audit("{0} Took 1x{1} at {2}.",
                    byEntity.GetName(),
                    stack.Collectible.Code,
                    Pos.AsBlockPos
                );
                Die();
                return;
            }

            base.OnInteract(byEntity, slot, hitPosition, mode);
        }

        public override WorldInteraction[] GetInteractionHelp(IClientWorldAccessor world, EntitySelection es, IClientPlayer player)
        {
            return [
                new WorldInteraction()
                {
                    ActionLangCode = "entityhelp-strawdummy-pickup",
                    MouseButton = EnumMouseButton.Right,
                    HotKeyCode = "shift"
                },
                ..base.GetInteractionHelp(world, es, player)
            ];
        }

    }
}
