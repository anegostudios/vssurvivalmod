using System.Linq;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Util;

#nullable disable

namespace Vintagestory.GameContent
{
    public class EntityBehaviorPlayerRevivable : EntityBehavior
    {
        EntityPlayer entityPlayer;

        public EntityBehaviorPlayerRevivable(Entity entity) : base(entity)
        {
            entityPlayer = entity as EntityPlayer;
        }


        public override void GetInfoText(StringBuilder infotext)
        {
            if (!entity.Alive)
            {
                double hoursleft = entityPlayer.RevivableIngameHoursLeft();

                if (hoursleft < 1) {
                    if (hoursleft*60 >= 0) infotext.AppendLine(Lang.Get("Mortally wounded, alive for {0} ingame minutes.", (int)(hoursleft*60)));
                } else
                {
                    infotext.AppendLine(Lang.Get("Mortally wounded, alive for {0} more hours", (int)hoursleft));
                }                
            }
        }

        public void AttemptRevive()
        {
            if (entityPlayer.RevivableIngameHoursLeft() <= 0)
            {
                return;
            }

            entityPlayer.Attributes.SetBool("noSatietyRestoreOnRevive", true);
            entityPlayer.Revive();
            entityPlayer.LastReviveTotalHours = entityPlayer.World.Calendar.TotalHours - 2; // No respawn glow
        }


        public override WorldInteraction[] GetInteractionHelp(IClientWorldAccessor world, EntitySelection es, IClientPlayer player, ref EnumHandling handled)
        {
            var wis = base.GetInteractionHelp(world, es, player, ref handled);
            if (!entity.Alive && entityPlayer.RevivableIngameHoursLeft() > 0)
            {
                wis ??= [];
                wis = wis.Append(
                    new WorldInteraction()
                    {
                        ActionLangCode = "reviveplayer",
                        MouseButton = EnumMouseButton.Right,
                        HotKeyCode = "ctrl",
                        Itemstacks = EntityBehaviorHealth.GetAllHealingItems(world.Api),
                        RequireFreeHand = true
                    }
                );
            }

            return wis;
        }

        public override string PropertyName()
        {
            return "playerrevivable";
        }

        
    }
}
