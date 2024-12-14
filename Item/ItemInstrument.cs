using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

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
            if (mseo.OwnerShipsByPlayerUid.TryGetValue(plr.PlayerUID, out var ownerships) && ownerships.TryGetValue(GroupCode, out var ownership))
            {
                var entity = api.World.GetEntityById(ownership.EntityId);
                if (entity != null)
                {
                    var tm = entity.GetBehavior<EntityBehaviorTaskAI>().TaskManager;
                    var aitcto = tm.AllTasks.FirstOrDefault(t => t is AiTaskComeToOwner) as AiTaskComeToOwner;
                    if (entity.ServerPos.DistanceTo(byEntity.ServerPos) <= aitcto.TeleportMaxRange) // Do nothing outside max teleport range
                    {
                        var mount = entity?.GetInterface<IMountable>();
                        if (mount != null)
                        {
                            if (mount.IsMountedBy(plr.Entity)) return;
                            if (mount.AnyMounted())
                            {
                                entity.GetBehavior<EntityBehaviorRideable>()?.UnmnountPassengers(); // You are not my owner, get lost!
                            }
                        }


                        tm.StopTasks();
                        tm.ExecuteTask(aitcto, 0);
                    }
                }
            }
        }
    }
}
