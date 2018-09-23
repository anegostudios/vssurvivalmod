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
        public override void OnHeldInteractStart(IItemSlot slot, IEntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandHandling handling)
        {
            if (blockSel == null) return;
            IPlayer player = byEntity.World.PlayerByUid((byEntity as EntityPlayer)?.PlayerUID);


            if (!byEntity.World.TestPlayerAccessBlock(player, blockSel.Position, EnumBlockAccessFlags.BuildOrBreak))
            {
                slot.MarkDirty();
                return;
            }

            if (!(byEntity is EntityPlayer) || player.WorldData.CurrentGameMode != EnumGameMode.Creative)
            {
                slot.TakeOut(1);
                slot.MarkDirty();
            }
            
            EntityProperties type = byEntity.World.GetEntityType(new AssetLocation("strawdummy"));
            Entity entity = byEntity.World.ClassRegistry.CreateEntity(type);

            if (entity != null)
            {
                entity.ServerPos.X = blockSel.Position.X + (blockSel.DidOffset ? 0 : blockSel.Face.Normali.X) + 0.5f;
                entity.ServerPos.Y = blockSel.Position.Y + (blockSel.DidOffset ? 0 : blockSel.Face.Normali.Y);
                entity.ServerPos.Z = blockSel.Position.Z + (blockSel.DidOffset ? 0 : blockSel.Face.Normali.Z) + 0.5f;
                entity.ServerPos.Yaw = byEntity.LocalPos.Yaw + GameMath.PI;
                if (player?.PlayerUID != null)
                {
                    entity.WatchedAttributes.SetString("ownerUid", player.PlayerUID);
                }
                
                entity.Pos.SetFrom(entity.ServerPos);

                byEntity.World.PlaySoundAt(new AssetLocation("sounds/block/torch"), entity, player);

                byEntity.World.SpawnEntity(entity);
                handling = EnumHandHandling.PreventDefaultAction;
            }

        }
    }
}