using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class ItemCreature : Item
    {
        public override string GetHeldTpUseAnimation(IItemSlot activeHotbarSlot, IEntity byEntity)
        {
            return null;
        }

        public override bool OnHeldInteractStart(IItemSlot slot, IEntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            if (blockSel == null) return false;

            if (!(byEntity is EntityPlayer) || byEntity.World.PlayerByUid((byEntity as EntityPlayer).PlayerUID).WorldData.CurrentGameMode != EnumGameMode.Creative)
            {
                slot.TakeOut(1);
                slot.MarkDirty();
            }
            

            EntityType type = byEntity.World.GetEntityType(new AssetLocation(CodeEndWithoutParts(1)));
            Entity entity = byEntity.World.ClassRegistry.CreateEntity(type);

            if (entity != null)
            {
                entity.ServerPos.X = blockSel.Position.X + (blockSel.DidOffset ? 0 : blockSel.Face.Normali.X) + 0.5f;
                entity.ServerPos.Y = blockSel.Position.Y + (blockSel.DidOffset ? 0 : blockSel.Face.Normali.Y);
                entity.ServerPos.Z = blockSel.Position.Z + (blockSel.DidOffset ? 0 : blockSel.Face.Normali.Z) + 0.5f;
                entity.ServerPos.Yaw = (float)byEntity.World.Rand.NextDouble() * 2 * GameMath.PI;

                entity.Pos.SetFrom(entity.ServerPos);

                entity.Attributes.SetString("origin", "playerplaced");
                byEntity.World.SpawnEntity(entity);
                return true;
            }

            return false;
        }

        public override string GetHeldTpIdleAnimation(IItemSlot activeHotbarSlot, IEntity byEntity)
        {
            EntityType type = byEntity.World.GetEntityType(new AssetLocation(CodeEndWithoutParts(1)));
            float size = Math.Max(type.HitBoxSize.X, type.HitBoxSize.Y);

            if (size > 1) return "holdunderarm";
            return "holdbothhands";
        }
    }
}