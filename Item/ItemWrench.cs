using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Cairo;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public interface IWrenchOrientable
    {
        void Rotate(EntityAgent byEntity, BlockSelection blockSel, int dir);
    }

    public class ItemWrench : Item
    {
        

        public override void OnHeldAttackStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandHandling handling)
        {
            if (blockSel == null) return;

            if (rotate(byEntity, blockSel, 1))
            {
                DamageItem(api.World, byEntity, slot);
            }

            handling = EnumHandHandling.PreventDefault;
        }

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
            if (handling == EnumHandHandling.PreventDefault) return;

            if (blockSel == null) return;

            if (rotate(byEntity, blockSel, -1))
            {
                DamageItem(api.World, byEntity, slot);
            }

            handling = EnumHandHandling.PreventDefault;
        }

        private bool rotate(EntityAgent byEntity, BlockSelection blockSel, int dir)
        {
            IPlayer byPlayer = (byEntity as EntityPlayer)?.Player;
            if (byPlayer == null) return false;

            if (!byEntity.World.Claims.TryAccess(byPlayer, blockSel.Position, EnumBlockAccessFlags.BuildOrBreak))
            {
                api.World.BlockAccessor.MarkBlockEntityDirty(blockSel.Position.AddCopy(blockSel.Face));
                api.World.BlockAccessor.MarkBlockDirty(blockSel.Position.AddCopy(blockSel.Face));
                return false;
            }

            var block = api.World.BlockAccessor.GetBlock(blockSel.Position);
            if (block is IWrenchOrientable iwre)
            {
                api.World.PlaySoundAt(block.Sounds.Place, blockSel.Position.X + 0.5f, blockSel.Position.Y + 0.5f, blockSel.Position.Z + 0.5f, byPlayer);
                (api.World as IClientWorldAccessor)?.Player.TriggerFpAnimation(EnumHandInteract.HeldItemInteract);

                iwre.Rotate(byEntity, blockSel, dir);
                return true;
            }

            BlockBehaviorWrenchOrientable bhWOrientable = block.GetBehavior<BlockBehaviorWrenchOrientable>();
            if (bhWOrientable == null) return false;

            var types = BlockBehaviorWrenchOrientable.VariantsByType[bhWOrientable.BaseCode];

            int index = types.IndexOf(bhWOrientable.block.Code);
            if (index == -1) return false;

            var newcode = types[GameMath.Mod(index + dir, types.Count)];
            var newblock = api.World.GetBlock(newcode);

            api.World.BlockAccessor.SetBlock(0, blockSel.Position);
            api.World.BlockAccessor.SetBlock(newblock.Id, blockSel.Position);

            api.World.PlaySoundAt(newblock.Sounds.Place, blockSel.Position.X + 0.5f, blockSel.Position.Y + 0.5f, blockSel.Position.Z + 0.5f, byPlayer);
            (api.World as IClientWorldAccessor)?.Player.TriggerFpAnimation(EnumHandInteract.HeldItemInteract);

            return true;
        }
    }
}
