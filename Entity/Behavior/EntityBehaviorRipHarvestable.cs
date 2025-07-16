using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

#nullable disable

namespace Vintagestory.GameContent
{
    public class EntityBehaviorRipHarvestable : EntityBehavior
    {
        protected float ripHarvestChance = 0.25f;

        public EntityBehaviorRipHarvestable(Entity entity) : base(entity)
        {
        }

        public override string PropertyName() => "ripharvestable";


        public override void OnEntityReceiveDamage(DamageSource damageSource, ref float damage)
        {
            if (damage > 0 && entity.World.Side == EnumAppSide.Server)
            {
                var eagent = damageSource.SourceEntity as EntityAgent;
                var attackWeaponSlot = eagent?.RightHandItemSlot;
                if (attackWeaponSlot?.Itemstack?.ItemAttributes != null && attackWeaponSlot.Itemstack.ItemAttributes.IsTrue("ripHarvest") && entity.World.Rand.NextDouble() < ripHarvestChance)
                {
                    var ebh = entity.GetBehavior<EntityBehaviorHarvestable>();
                    if (ebh != null)
                    {
                        ebh.GenerateDrops((eagent as EntityPlayer).Player);
                    }

                    var lootSlot = ebh.Inventory.FirstNonEmptySlot;
                    if (lootSlot != null)
                    {
                        entity.World.SpawnItemEntity(lootSlot.TakeOutWhole(), entity.ServerPos.XYZ);
                    }
                }
            }
        }
    }
}
