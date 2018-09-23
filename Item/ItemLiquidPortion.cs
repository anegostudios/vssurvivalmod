using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Cairo;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    class ItemLiquidPortion : Item
    {
        public override void OnGroundIdle(EntityItem entityItem)
        {
            entityItem.Die(EnumDespawnReason.Removed);

            if (entityItem.World.Side == EnumAppSide.Server)
            {
                entityItem.World.SpawnCubeParticles(entityItem.LocalPos.XYZ, entityItem.Itemstack, 0.5f, 15 * entityItem.Itemstack.StackSize, 0.35f);
                entityItem.World.PlaySoundAt(new AssetLocation("sounds/environment/smallsplash"), (float)entityItem.LocalPos.X, (float)entityItem.LocalPos.Y, (float)entityItem.LocalPos.Z, null);
            }
            

            base.OnGroundIdle(entityItem);
            
        }
    }
}
