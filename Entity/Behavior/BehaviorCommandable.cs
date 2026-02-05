using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.GameContent
{
    public class EntityBehaviorCommandable : EntityBehavior
    {
        public bool Sit
        {
            get
            {
                return entity.WatchedAttributes.GetBool("commandSit");
            }
            set
            {
                entity.WatchedAttributes.SetBool("commandSit", value);
            }
        }

        public string GuardedNameClient
        {
            get
            {
                return entity.WatchedAttributes.GetString("guardedName");
            }
            set
            {
                entity.WatchedAttributes.SetString("guardedName", value);
            }
        }

        public EntityBehaviorCommandable(Entity entity) : base(entity)
        {

        }

        public override void AfterInitialized(bool onFirstSpawn)
        {
            if (entity.World.Side == EnumAppSide.Client)
            {
                SetGuardedName();
            }
        }

        private void SetGuardedName()
        {
            Entity guarded = GetGuardedEntity();
            if (guarded != null)
            {
                string name = guarded.GetName();
                GuardedNameClient = name ?? "";
            }
            else GuardedNameClient = "";
        }

        public override void OnEntitySpawn()
        {
            setupTaskBlocker();
        }

        public override void OnEntityLoaded()
        {
            setupTaskBlocker();
        }


        void setupTaskBlocker()
        {
            if (entity.Api.Side != EnumAppSide.Server) return;
            var bhtaskAi = entity.GetBehavior<EntityBehaviorTaskAI>();
            if (bhtaskAi != null)
            {
                bhtaskAi.TaskManager.OnShouldExecuteTask += (task) => !Sit || task is AiTaskIdle || task is AiTaskLookAround || task is AiTaskComeToOwner;
            }
        }

        public override void OnInteract(EntityAgent byEntity, ItemSlot itemslot, Vec3d hitPosition, EnumInteractMode mode, ref EnumHandling handled)
        {
            if (entity.Alive && !byEntity.Controls.ShiftKey)
            {
                Sit = !Sit;
            }
        }

        public override void GetInfoText(StringBuilder infotext)
        {
            base.GetInfoText(infotext);

            if (Sit) infotext.AppendLine(Lang.Get("Waits"));
            else infotext.AppendLine(Lang.Get("Follows {0}", GuardedNameClient));

            var healthTree = entity.WatchedAttributes.GetTreeAttribute("health") as ITreeAttribute;
            if (healthTree != null) infotext.AppendLine(Lang.Get("commandable-entity-healthpoints", healthTree.GetFloat("currenthealth"), healthTree.GetFloat("maxhealth")));
        }

        public override string PropertyName()
        {
            return "commandable";
        }


        public Entity GetGuardedEntity()
        {
            var uid = entity.WatchedAttributes.GetString("guardedPlayerUid");
            if (uid != null)
            {
                return entity.World.PlayerByUid(uid)?.Entity;
            }
            else
            {
                var id = entity.WatchedAttributes.GetLong("guardedEntityId");
                return entity.World.GetEntityById(id);
            }
        }

        public override WorldInteraction[] GetInteractionHelp(IClientWorldAccessor world, EntitySelection es, IClientPlayer player, ref EnumHandling handled)
        {
            if (!entity.Alive) return null;

            return [
                new WorldInteraction() {
                    ActionLangCode = "entityinteraction-command",
                    HotKeyCode = null,
                    MouseButton = EnumMouseButton.Right
                }
            ];
        }

    }
}
