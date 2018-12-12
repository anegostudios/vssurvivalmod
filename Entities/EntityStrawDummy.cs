using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class EntityStrawDummy : EntityHumanoid
    {
        public override void OnInteract(EntityAgent byEntity, IItemSlot slot, Vec3d hitPosition, EnumInteractMode mode)
        {
            if (!Alive || World.Side == EnumAppSide.Client || mode == 0)
            {
                base.OnInteract(byEntity, slot, hitPosition, mode);
                return;
            }

            string owneruid = WatchedAttributes.GetString("ownerUid", null);
            string agentUid = (byEntity as EntityPlayer)?.PlayerUID;

            if (agentUid != null && (owneruid == null || owneruid == "" || owneruid == agentUid) && byEntity.Controls.Sneak)
            {
                ItemStack stack = new ItemStack(byEntity.World.GetItem(new AssetLocation("strawdummy")));
                if (!byEntity.TryGiveItemStack(stack))
                {
                    byEntity.World.SpawnItemEntity(stack, ServerPos.XYZ);
                }
                Die();
                return;
            }

            base.OnInteract(byEntity, slot, hitPosition, mode);
        }

    }
}
