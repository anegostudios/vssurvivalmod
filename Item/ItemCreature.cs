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
        public override string GetHeldTpUseAnimation(ItemSlot activeHotbarSlot, Entity byEntity)
        {
            return null;
        }

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling)
        {
            if (blockSel == null) return;

            IPlayer player = byEntity.World.PlayerByUid((byEntity as EntityPlayer).PlayerUID);

            if (!byEntity.World.Claims.TryAccess(player, blockSel.Position, EnumBlockAccessFlags.BuildOrBreak))
            {
                return;
            }

            if (!(byEntity is EntityPlayer) || player.WorldData.CurrentGameMode != EnumGameMode.Creative)
            {
                slot.TakeOut(1);
                slot.MarkDirty();
            }


            EntityProperties type = byEntity.World.GetEntityType(new AssetLocation(CodeEndWithoutParts(1)));
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
                handHandling = EnumHandHandling.PreventDefaultAction;
            }
        }

        public override string GetHeldTpIdleAnimation(ItemSlot activeHotbarSlot, Entity byEntity, EnumHand hand)
        {
            EntityProperties type = byEntity.World.GetEntityType(new AssetLocation(CodeEndWithoutParts(1)));
            if (type == null) return base.GetHeldTpIdleAnimation(activeHotbarSlot, byEntity, hand);

            float size = Math.Max(type.HitBoxSize.X, type.HitBoxSize.Y);

            if (size > 1) return "holdunderarm";
            return "holdbothhands";
        }
    }
}