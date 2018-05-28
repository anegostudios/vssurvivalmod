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

        
        public EntityBehaviorBreathe(Entity entity) : base(entity)
        {
        }

        public void Check()
        {
            if (entity is EntityPlayer)
            {
                // Asphyxiation damage for players is broken (particularly on dedicated server play)
                return;
                /*EntityPlayer plr = (EntityPlayer)entity;
                EnumGameMode mode = entity.World.PlayerByUid(plr.PlayerUID).WorldData.CurrentGameMode;
                if (mode == EnumGameMode.Creative || mode == EnumGameMode.Spectator) return;*/
            }
            
            BlockPos pos = new BlockPos(
                (int)(entity.ServerPos.X),
                (int)(entity.ServerPos.Y + ((EntityAgent)entity).EyeHeight()),
                (int)(entity.ServerPos.Z)
            );

            Block block = entity.World.BlockAccessor.GetBlock(pos);
            Cuboidf[] collisionboxes = block.GetCollisionBoxes(entity.World.BlockAccessor, pos);
            

            if (collisionboxes == null) return;

            for (int i = 0; i < collisionboxes.Length; i++)
            {
                Cuboidf box = collisionboxes[i];
                tmp.Set(pos.X + box.X1, pos.Y + box.Y1, pos.Z + box.Z1, pos.X + box.X2, pos.Y + box.Y2, pos.Z + box.Z2);

                if (tmp.Contains(entity.ServerPos.X, entity.ServerPos.Y + ((EntityAgent)entity).EyeHeight(), entity.ServerPos.Z))
                {
                    DamageSource dmgsrc = new DamageSource() { source = EnumDamageSource.Block, sourceBlock = block, type = EnumDamageType.Asphyxiation };
                    entity.ReceiveDamage(dmgsrc, 1f);

                    break;
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
        }

        public override string PropertyName()
        {
            return "breathe";
        }
    }
}
