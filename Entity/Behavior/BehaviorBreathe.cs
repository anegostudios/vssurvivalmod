using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class EntityBehaviorBreathe : EntityBehavior
    {
        Cuboidd tmp = new Cuboidd();
        float breatheInterval = 0;
        //the padding that the collisionbox is adjusted by for suffocation damage.  Can be adjusted as necessary - don't set to exactly 0.
        float padding = 0.1f; 

        
        public EntityBehaviorBreathe(Entity entity) : base(entity)
        {
        }

        public void Check()
        {
            if (entity is EntityPlayer)
            {
                EntityPlayer plr = (EntityPlayer)entity;
                EnumGameMode mode = entity.World.PlayerByUid(plr.PlayerUID).WorldData.CurrentGameMode;
                if (mode == EnumGameMode.Creative || mode == EnumGameMode.Spectator) return;
            }
            
            BlockPos pos = new BlockPos(
                (int)(entity.ServerPos.X),
                (int)(entity.ServerPos.Y + ((EntityAgent)entity).EyeHeight()),
                (int)(entity.ServerPos.Z)
            );

            Block block = entity.World.BlockAccessor.GetBlock(pos);
            Cuboidf[] collisionboxes = block.GetCollisionBoxes(entity.World.BlockAccessor, pos);

            Cuboidf box = new Cuboidf();

            if (collisionboxes == null) return;

            for (int i = 0; i < collisionboxes.Length; i++)
            {
                box.Set(collisionboxes[i]);
                box.OmniGrowBy(-padding);
                tmp.Set(pos.X + box.X1, pos.Y + box.Y1, pos.Z + box.Z1, pos.X + box.X2, pos.Y + box.Y2, pos.Z + box.Z2);
                box.OmniGrowBy(padding);

                if (tmp.Contains(entity.ServerPos.X, entity.ServerPos.Y + ((EntityAgent)entity).EyeHeight(), entity.ServerPos.Z))
                {
                    Cuboidd EntitySuffocationBox = entity.CollisionBox.ToDouble();

                    if (tmp.Intersects(EntitySuffocationBox))
                    {
                        DamageSource dmgsrc = new DamageSource() { source = EnumDamageSource.Block, sourceBlock = block, type = EnumDamageType.Suffocation };
                        entity.ReceiveDamage(dmgsrc, 1f);
                        break;
                    }


                }

            }

        }


        public override void OnGameTick(float deltaTime)
        {
            base.OnGameTick(deltaTime);

            breatheInterval += deltaTime;

            if (breatheInterval > 1)
            {
                breatheInterval--;
                Check();
            }

            entity.World.FrameProfiler.Mark("entity-breathe");
        }

        public override string PropertyName()
        {
            return "breathe";
        }
    }
}
