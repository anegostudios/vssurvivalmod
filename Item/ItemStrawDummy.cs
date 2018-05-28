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
    public class ItemStrawDummy : Item
    {
        public override bool OnHeldInteractStart(IItemSlot slot, IEntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            if (blockSel == null) return false;

            if (!(byEntity is EntityPlayer) || byEntity.World.PlayerByUid((byEntity as EntityPlayer).PlayerUID).WorldData.CurrentGameMode != EnumGameMode.Creative)
            {
                slot.TakeOut(1);
                slot.MarkDirty();
            }

            EntityType type = byEntity.World.GetEntityType(new AssetLocation("strawdummy"));
            Entity entity = byEntity.World.ClassRegistry.CreateEntity(type);

            if (entity != null)
            {
                entity.ServerPos.X = blockSel.Position.X + (blockSel.DidOffset ? 0 : blockSel.Face.Normali.X) + 0.5f;
                entity.ServerPos.Y = blockSel.Position.Y + (blockSel.DidOffset ? 0 : blockSel.Face.Normali.Y);
                entity.ServerPos.Z = blockSel.Position.Z + (blockSel.DidOffset ? 0 : blockSel.Face.Normali.Z) + 0.5f;
                entity.ServerPos.Yaw = byEntity.LocalPos.Yaw + GameMath.PI;

                entity.Pos.SetFrom(entity.ServerPos);

                IPlayer player = (byEntity is EntityPlayer) ? byEntity.World.PlayerByUid((byEntity as EntityPlayer).PlayerUID) : null;

                byEntity.World.PlaySoundAt(new AssetLocation("sounds/block/torch"), entity, player);

                byEntity.World.SpawnEntity(entity);
                return true;
            }

            return false;
        }
    }
}