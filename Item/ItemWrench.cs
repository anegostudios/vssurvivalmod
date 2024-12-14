using System.Linq;
using System.Numerics;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public interface IExtraWrenchModes
    {
        SkillItem[] GetExtraWrenchModes(IPlayer byPlayer, BlockSelection blockSelection);
        void OnWrenchInteract(IPlayer player, BlockSelection blockSel, int mode, int v);
    }

    public class ItemWrench : Item
    {
        SkillItem rotateSk;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            rotateSk = new SkillItem() { Code = new AssetLocation("rotate"), Name = "Rotate (Default)" };

            if (api is ICoreClientAPI capi)
            {
                rotateSk.WithIcon(capi, capi.Gui.LoadSvgWithPadding(new AssetLocation("textures/icons/rotate.svg"), 48, 48, 5, ColorUtil.WhiteArgb));
            }
        }

        public override void OnUnloaded(ICoreAPI api)
        {
            rotateSk?.Dispose();
        }

        SkillItem[] GetExtraWrenchModes(IPlayer byPlayer, BlockSelection blockSelection)
        {
            if (blockSelection != null)
            {
                var block = api.World.BlockAccessor.GetBlock(blockSelection.Position);
                var iewm = block.GetInterface<IExtraWrenchModes>(api.World, blockSelection.Position);

                return iewm?.GetExtraWrenchModes(byPlayer, blockSelection);
            }

            return null;
        }


        public override int GetToolMode(ItemSlot slot, IPlayer byPlayer, BlockSelection blockSelection)
        {
            var skillItems = GetExtraWrenchModes(byPlayer, blockSelection);
            if (skillItems != null) {
                var block = api.World.BlockAccessor.GetBlock(blockSelection.Position);
                return slot.Itemstack.Attributes.GetInt("toolMode-" + block.Id);
            }

            return base.GetToolMode(slot, byPlayer, blockSelection);
        }

        public override SkillItem[] GetToolModes(ItemSlot slot, IClientPlayer forPlayer, BlockSelection blockSel)
        {
            var skillItems = GetExtraWrenchModes(forPlayer, blockSel);
            if (skillItems != null && skillItems.Length > 0)
            {
                return new SkillItem[] { rotateSk }.Append(skillItems);
            }

            return base.GetToolModes(slot, forPlayer, blockSel);
        }

        public override void SetToolMode(ItemSlot slot, IPlayer byPlayer, BlockSelection blockSelection, int toolMode)
        {
            var skillItems = GetExtraWrenchModes(byPlayer, blockSelection);
            if (skillItems != null && skillItems.Length > 0)
            {
                var block = api.World.BlockAccessor.GetBlock(blockSelection.Position);
                slot.Itemstack.Attributes.SetInt("toolMode-" + block.Id, toolMode);
                return;
            }

            base.SetToolMode(slot, byPlayer, blockSelection, toolMode);
        }




        public override void OnHeldAttackStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandHandling handling)
        {
            if (blockSel == null) return;
            var player = (byEntity as EntityPlayer)?.Player;

            if (handleModedInteract(slot, blockSel, player, 1))
            {
                handling = EnumHandHandling.PreventDefault;
                return;
            }

            if (rotate(byEntity, blockSel, 1))
            {
                if (player.WorldData.CurrentGameMode != EnumGameMode.Creative)
                {
                    DamageItem(api.World, byEntity, slot);
                }
            }

            handling = EnumHandHandling.PreventDefault;
        }

        private bool handleModedInteract(ItemSlot slot, BlockSelection blockSel, IPlayer player, int interactmode)
        {
            var skillItems = GetExtraWrenchModes(player, blockSel);
            if (skillItems != null)
            {
                int mode = GetToolMode(slot, player, blockSel);
                if (mode > 0)
                {
                    var block = api.World.BlockAccessor.GetBlock(blockSel.Position);
                    var iewm = block.GetInterface<IExtraWrenchModes>(api.World, blockSel.Position);
                    if (iewm != null)
                    {
                        iewm.OnWrenchInteract(player, blockSel, mode - 1, interactmode);
                        return true;
                    }
                }
            }
            return false;
        }


        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
            if (handling == EnumHandHandling.PreventDefault) return;
            if (blockSel == null) return;

            var player = (byEntity as EntityPlayer)?.Player;

            if (handleModedInteract(slot, blockSel, player, 0))
            {
                handling = EnumHandHandling.PreventDefault;
                return;
            }

            if (rotate(byEntity, blockSel, -1))
            {
                if (player.WorldData.CurrentGameMode != EnumGameMode.Creative)
                {
                    DamageItem(api.World, byEntity, slot);
                }
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
            var iwre = block.GetInterface<IWrenchOrientable>(api.World, blockSel.Position);
            if (iwre != null)
            {
                Rotate(blockSel, dir, byPlayer, block, iwre);
                return true;
            }

            BlockBehaviorWrenchOrientable bhWOrientable = block.GetBehavior<BlockBehaviorWrenchOrientable>();
            if (bhWOrientable == null) return false;

            using var types = BlockBehaviorWrenchOrientable.VariantsByType[bhWOrientable.BaseCode].GetEnumerator();
            
            while (types.MoveNext())
            {
                if (types.Current != null && types.Current.Equals(bhWOrientable.block.Code))
                {
                    break;
                }
            }
            // advance to the next element, if at end take first
            var newcode = types.MoveNext()
                ? types.Current
                : BlockBehaviorWrenchOrientable.VariantsByType[bhWOrientable.BaseCode].First();
            var newblock = api.World.GetBlock(newcode);

            api.World.BlockAccessor.ExchangeBlock(newblock.Id, blockSel.Position);

            api.World.PlaySoundAt(newblock.Sounds.Place, blockSel.Position, 0, byPlayer);
            (api.World as IClientWorldAccessor)?.Player.TriggerFpAnimation(EnumHandInteract.HeldItemInteract);

            return true;
        }

        private void Rotate(BlockSelection blockSel, int dir, IPlayer byPlayer, Block block, IWrenchOrientable iwre)
        {
            api.World.PlaySoundAt(block.Sounds.Place, blockSel.Position, 0, byPlayer);
            (api.World as IClientWorldAccessor)?.Player.TriggerFpAnimation(EnumHandInteract.HeldItemInteract);

            iwre.Rotate(byPlayer.Entity, blockSel, dir);
        }
    }
}
