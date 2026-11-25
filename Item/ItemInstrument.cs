using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

#nullable disable

namespace Vintagestory.GameContent
{
    public class ItemFlute : Item
    {
        protected string GroupCode = "mountableanimal";

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            var ela = api.World.ElapsedMilliseconds;
            var prevela = slot.Itemstack.Attributes.GetLong("lastPlayerMs", -99999);

            // User must have restarted his game world, allow call
            if (prevela > ela)
            {
                prevela = ela-4001;
            }
            if (ela - prevela <= 4000) return;


            slot.Itemstack.Attributes.SetLong("lastPlayerMs", ela);
            api.World.PlaySoundAt(new AssetLocation("sounds/instrument/elkcall"), byEntity, (byEntity as EntityPlayer)?.Player, 0.75f, 32, 0.5f);

            if (api.Side == EnumAppSide.Server)
            {
                callElk(byEntity);
            }

            handling = EnumHandHandling.PreventDefault;
        }

        private void callElk(EntityAgent byEntity)
        {
            var plr = (byEntity as EntityPlayer).Player;
            var mseo = api.ModLoader.GetModSystem<ModSystemEntityOwnership>();
            if (!mseo.OwnerShipsByPlayerUid.TryGetValue(plr.PlayerUID, out var ownerships) || ownerships == null || !ownerships.TryGetValue(GroupCode, out var ownership))
            {
                return;
            }

            var entity = api.World.GetEntityById(ownership.EntityId);
            if (entity == null)
            {
                return;
            }

            var mw = entity.GetBehavior<EntityBehaviorMortallyWoundable>();
            if (mw?.HealthState == EnumEntityHealthState.MortallyWounded || mw?.HealthState == EnumEntityHealthState.Dead)
            {
                return;
            }

            var tm = entity.GetBehavior<EntityBehaviorTaskAI>().TaskManager;
            var aitcto = tm.AllTasks.FirstOrDefault(t => t is AiTaskComeToOwner) as AiTaskComeToOwner;
            if (entity.Pos.DistanceTo(byEntity.Pos) > aitcto.TeleportMaxRange) // Do nothing outside max teleport range
            {
                return;
            }

            var mount = entity?.GetInterface<IMountable>();
            if (mount != null)
            {
                if (mount.IsMountedBy(plr.Entity)) return;
                if (mount.AnyMounted())
                {
                    entity.GetBehavior<EntityBehaviorRideable>()?.UnmountPassengers(); // You are not my owner, get lost!
                }
            }

            entity.AlwaysActive = true;
            entity.State = EnumEntityState.Active;
            aitcto.allowTeleportCount=1;
            tm.StopTasks();
            tm.ExecuteTask(aitcto, 0);
        }
    }
}
