using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

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

        public EntityBehaviorCommandable(Entity entity) : base(entity)
        {

        }

        public override void Initialize(EntityProperties properties, JsonObject attributes)
        {
            base.Initialize(properties, attributes);
        }


        public override void OnInteract(EntityAgent byEntity, ItemSlot itemslot, Vec3d hitPosition, EnumInteractMode mode, ref EnumHandling handled)
        {
            Sit = !Sit;
        }

        public override void GetInfoText(StringBuilder infotext)
        {
            base.GetInfoText(infotext);

            if (Sit) infotext.AppendLine(Lang.Get("Waits"));
            else infotext.AppendLine(Lang.Get("Follows"));

            var healthTree = entity.WatchedAttributes.GetTreeAttribute("health") as ITreeAttribute;
            if (healthTree != null) infotext.AppendLine(Lang.Get("Health: {0:0.##}/{1:0.##}", healthTree.GetFloat("currenthealth"), healthTree.GetFloat("maxhealth")));
        }

        public override string PropertyName()
        {
            return "commandable";
        }

    }
}
