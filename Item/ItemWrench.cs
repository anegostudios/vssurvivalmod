using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using System;
#nullable enable

namespace Vintagestory.GameContent
{
    public interface IExtraWrenchModes
    {
        SkillItem[] GetExtraWrenchModes(IPlayer byPlayer, BlockSelection blockSelection);
        void OnWrenchInteract(IPlayer player, BlockSelection blockSel, int mode, int v);
    }

    public class ItemWrench : Item
    {
        SkillItem? rotateSk;

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

        SkillItem[]? GetExtraWrenchModes(IPlayer byPlayer, BlockSelection? blockSelection)
        {
            if (blockSelection == null) return null;

            return api.World.BlockAccessor
                .GetBlock(blockSelection.Position)
                .GetInterface<IExtraWrenchModes>(api.World, blockSelection.Position)?
                .GetExtraWrenchModes(byPlayer, blockSelection);
        }


        public override int GetToolMode(ItemSlot slot, IPlayer byPlayer, BlockSelection blockSelection)
        {
            if (GetExtraWrenchModes(byPlayer, blockSelection) != null)
            {
                return slot.Itemstack!.Attributes.GetInt("toolMode-" + api.World.BlockAccessor.GetBlock(blockSelection.Position).Id);
            }

            return base.GetToolMode(slot, byPlayer, blockSelection);
        }

        public override SkillItem[] GetToolModes(ItemSlot slot, IClientPlayer forPlayer, BlockSelection blockSel)
        {
            if (GetExtraWrenchModes(forPlayer, blockSel) is SkillItem[] skillItems && skillItems.Length > 0)
            {
                return [rotateSk!, .. skillItems];
            }

            return base.GetToolModes(slot, forPlayer, blockSel);
        }

        public override void SetToolMode(ItemSlot slot, IPlayer byPlayer, BlockSelection blockSelection, int toolMode)
        {
            if (GetExtraWrenchModes(byPlayer, blockSelection)?.Length > 0)
            {
#if DEBUG
                api.World.Logger.Debug("Set wrench tool mode " + toolMode);
#endif
                slot?.Itemstack?.Attributes.SetInt("toolMode-" + api.World.BlockAccessor.GetBlock(blockSelection.Position).Id, toolMode);
                return;
            }

            base.SetToolMode(slot, byPlayer, blockSelection, toolMode);
        }




        public override void OnHeldAttackStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandHandling handling)
        {
            if (blockSel == null) return;
            IPlayer? player = (byEntity as EntityPlayer)?.Player;

            if (!byEntity.World.Claims.TryAccess(player, blockSel.Position, EnumBlockAccessFlags.BuildOrBreak))
            {
                api.World.BlockAccessor.MarkBlockEntityDirty(blockSel.Position.AddCopy(blockSel.Face));
                api.World.BlockAccessor.MarkBlockDirty(blockSel.Position.AddCopy(blockSel.Face));
                return;
            }


            if (player != null && handleModedInteract(slot, blockSel, player, 1))
            {
                handling = EnumHandHandling.PreventDefault;
                return;
            }

            if (rotate(byEntity, blockSel, 1))
            {
                if (player?.WorldData.CurrentGameMode != EnumGameMode.Creative)
                {
                    DamageItem(api.World, byEntity, slot);
                }
            }

            handling = EnumHandHandling.PreventDefault;
        }

        private bool handleModedInteract(ItemSlot slot, BlockSelection blockSel, IPlayer player, int interactmode)
        {
            if (GetExtraWrenchModes(player, blockSel) != null)
            {
                int mode = GetToolMode(slot, player, blockSel);
                if (mode > 0)
                {
                    Block block = api.World.BlockAccessor.GetBlock(blockSel.Position);
                    if (block.GetInterface<IExtraWrenchModes>(api.World, blockSel.Position) is IExtraWrenchModes iewm)
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

            if (byEntity.World.BlockAccessor.GetBlockEntity<BlockEntityForge>(blockSel.Position) != null) return;

            IPlayer? player = (byEntity as EntityPlayer)?.Player;

            if (!byEntity.World.Claims.TryAccess(player, blockSel.Position, EnumBlockAccessFlags.BuildOrBreak))
            {
                api.World.BlockAccessor.MarkBlockEntityDirty(blockSel.Position.AddCopy(blockSel.Face));
                api.World.BlockAccessor.MarkBlockDirty(blockSel.Position.AddCopy(blockSel.Face));
                return;
            }


            if (api.World.BlockAccessor.GetSubDecors(blockSel.Position) is Dictionary<int, Block> decors)
            {
                int targetSubPos = blockSel.ToDecorIndex() / 6;
                foreach (var decorAndPos in decors)
                {
                    DecorBits decorPosition = new DecorBits(decorAndPos.Key);
                    if (decorPosition.Face == blockSel.Face.Index)   // Found a decor on the face we are looking at
                    {
                        int subPos = decorPosition.SubPosition;
                        if (subPos != 0 && subPos != targetSubPos) continue;
                        int newRotation = (decorPosition.Rotation + 1) % 8;

                        // Remove the decor from the old faceAndSubposition, and add it back with the new rotation
                        api.World.BlockAccessor.SetDecor(api.World.BlockAccessor.GetBlock(0), blockSel.Position, decorPosition);
                        decorPosition.Rotation = newRotation;
                        api.World.BlockAccessor.SetDecor(decorAndPos.Value, blockSel.Position, decorPosition);

                        handling = EnumHandHandling.PreventDefault;
                        return;
                    }
                }
            }


            if (player != null && handleModedInteract(slot, blockSel, player, 0))
            {
                handling = EnumHandHandling.PreventDefault;
                return;
            }

            if (rotate(byEntity, blockSel, -1))
            {
                if (player?.WorldData.CurrentGameMode != EnumGameMode.Creative)
                {
                    DamageItem(api.World, byEntity, slot);
                }
            }

            handling = EnumHandHandling.PreventDefault;
        }

        private bool rotate(EntityAgent byEntity, BlockSelection blockSel, int dir)
        {
            if ((byEntity as EntityPlayer)?.Player is not IPlayer byPlayer) return false;

            Block block = api.World.BlockAccessor.GetBlock(blockSel.Position);
            if (block.GetInterface<IWrenchOrientable>(api.World, blockSel.Position) is IWrenchOrientable iwo)
            {
                Rotate(blockSel, dir, byPlayer, block, iwo);
                return true;
            }

            if (block.GetBehavior<BlockBehaviorWrenchOrientable>() is not BlockBehaviorWrenchOrientable bwo) return false;

            using var types = BlockBehaviorWrenchOrientable.VariantsByType[bwo.BaseCode].GetEnumerator();

            while (types.MoveNext())
            {
                if (types.Current?.Equals(bwo.block.Code) ?? false)
                {
                    break;
                }
            }

            // advance to the next element, if at end take first
            var newCode = types.MoveNext() ? types.Current : BlockBehaviorWrenchOrientable.VariantsByType[bwo.BaseCode].First();

            if (api.World.GetBlock(newCode) is Block newBlock)
            {

                api.World.BlockAccessor.ExchangeBlock(newBlock.Id, blockSel.Position);
                if (api.Side == EnumAppSide.Client) api.World.BlockAccessor.RedrawNeighbouringChunk(blockSel.Position);

                api.World.PlaySoundAt(newBlock.Sounds.Place, blockSel.Position, 0, byPlayer);
                (api.World as IClientWorldAccessor)?.Player.TriggerFpAnimation(EnumHandInteract.HeldItemInteract);
            }
            else
            {
                api.World.Logger.Warning($"Couldn't find BlockBehaviorWrenchOrientable variant {newCode} for {block.Code}");
            }

            return true;
        }

        private void Rotate(BlockSelection blockSel, int dir, IPlayer byPlayer, Block block, IWrenchOrientable iwre)
        {
            api.World.PlaySoundAt(block.Sounds.Place, blockSel.Position, 0, byPlayer);
            (api.World as IClientWorldAccessor)?.Player.TriggerFpAnimation(EnumHandInteract.HeldItemInteract);
            iwre.Rotate(byPlayer.Entity, blockSel, dir);
            (api.World as IClientWorldAccessor)?.BlockAccessor.RedrawNeighbouringChunk(blockSel.Position);
        }
    }
}
