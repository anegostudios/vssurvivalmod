using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class ItemHoe : Item
    {
        WorldInteraction[] interactions;

        public override void OnLoaded(ICoreAPI api)
        {
            if (api.Side != EnumAppSide.Client) return;
            ICoreClientAPI capi = api as ICoreClientAPI;

            interactions = ObjectCacheUtil.GetOrCreate(api, "hoeInteractions", () =>
            {
                List<ItemStack> stacks = new List<ItemStack>();

                foreach (Block block in api.World.Blocks)
                {
                    if (block.Code == null) continue;

                    if (block.Code.PathStartsWith("soil"))
                    {
                        stacks.Add(new ItemStack(block));
                    }
                }

                return new WorldInteraction[]
                {
                    new WorldInteraction()
                    {
                        ActionLangCode = "heldhelp-till",
                        MouseButton = EnumMouseButton.Right,
                        Itemstacks = stacks.ToArray()
                    }
                };
            });
        }



        public override void OnHeldInteractStart(ItemSlot itemslot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling)
        {
            if (blockSel == null) return;

            if (byEntity.Controls.ShiftKey && byEntity.Controls.CtrlKey)
            {
                base.OnHeldInteractStart(itemslot, byEntity, blockSel, entitySel, firstEvent, ref handHandling);
                return;
            }

            BlockPos pos = blockSel.Position;
            Block block = byEntity.World.BlockAccessor.GetBlock(pos);

            byEntity.Attributes.SetInt("didtill", 0);

            if (block.Code.PathStartsWith("soil"))
            {
                handHandling = EnumHandHandling.PreventDefault;
            }
        }


        public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            if (blockSel == null) return false;
            if (byEntity.Controls.ShiftKey && byEntity.Controls.CtrlKey) return false;

            IPlayer byPlayer = (byEntity as EntityPlayer).Player;

            if (byEntity.World is IClientWorldAccessor)
            {
                ModelTransform tf = new ModelTransform();
                tf.EnsureDefaultValues();

                float rotateToTill = GameMath.Clamp(secondsUsed * 18, 0, 2f);
                float scrape = GameMath.SmoothStep(1 / 0.4f * GameMath.Clamp(secondsUsed - 0.35f, 0, 1));
                float scrapeShake = secondsUsed > 0.35f && secondsUsed < 0.75f ? (float)(GameMath.Sin(secondsUsed * 50) / 60f) : 0;

                float rotateWithReset = Math.Max(0, rotateToTill - GameMath.Clamp(24 * (secondsUsed - 0.75f), 0, 2));
                float scrapeWithReset = Math.Max(0, scrape - Math.Max(0, 20*(secondsUsed - 0.75f)));

                tf.Origin.Set(0f, 0, 0.5f);
                tf.Rotation.Set(0, rotateWithReset * 45, 0);
                tf.Translation.Set(scrapeShake , 0, scrapeWithReset / 2);

                byEntity.Controls.UsingHeldItemTransformBefore = tf;
            }

            if (secondsUsed > 0.35f && secondsUsed < 0.87f)
            {
                Vec3d dir = new Vec3d().AheadCopy(1, 0, byEntity.SidedPos.Yaw - GameMath.PI);
                Vec3d pos = blockSel.Position.ToVec3d().Add(0.5 + dir.X, 1.03, 0.5 + dir.Z);

                pos.X -= dir.X * secondsUsed * 1 / 0.75f * 1.2f;
                pos.Z -= dir.Z * secondsUsed * 1 / 0.75f * 1.2f;

                byEntity.World.SpawnCubeParticles(blockSel.Position, pos, 0.25f, 3, 0.5f, byPlayer);
            }

            if (secondsUsed > 0.6f && byEntity.Attributes.GetInt("didtill") == 0 && byEntity.World.Side == EnumAppSide.Server)
            {
                byEntity.Attributes.SetInt("didtill", 1);
                DoTill(secondsUsed, slot, byEntity, blockSel, entitySel);
            }

            return secondsUsed < 1;
        }


        public override bool OnHeldInteractCancel(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, EnumItemUseCancelReason cancelReason)
        {
            return false;
        }

        public virtual void DoTill(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            if (blockSel == null) return;
            BlockPos pos = blockSel.Position;
            Block block = byEntity.World.BlockAccessor.GetBlock(pos);

            if (!block.Code.PathStartsWith("soil")) return;


            string fertility = block.LastCodePart(1);
            Block farmland = byEntity.World.GetBlock(new AssetLocation("farmland-dry-" + fertility));

            IPlayer byPlayer = (byEntity as EntityPlayer).Player;
            if (farmland == null || byPlayer == null) return;




            if (block.Sounds != null) byEntity.World.PlaySoundAt(block.Sounds.Place, pos.X, pos.Y, pos.Z, null);

            byEntity.World.BlockAccessor.SetBlock(farmland.BlockId, pos);
            slot.Itemstack.Collectible.DamageItem(byEntity.World, byEntity, byPlayer.InventoryManager.ActiveHotbarSlot);

            if (slot.Empty)
            {
                byEntity.World.PlaySoundAt(new AssetLocation("sounds/effect/toolbreak"), byEntity.Pos.X, byEntity.Pos.Y, byEntity.Pos.Z);
            }

            BlockEntity be = byEntity.World.BlockAccessor.GetBlockEntity(pos);
            if (be is BlockEntityFarmland)
            {
                ((BlockEntityFarmland)be).OnCreatedFromSoil(block);
            }

            byEntity.World.BlockAccessor.MarkBlockDirty(pos);
            //byEntity.GetBehavior<EntityBehaviorHunger>()?.ConsumeSaturation(5f);
        }


        public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot)
        {
            return interactions.Append(base.GetHeldInteractionHelp(inSlot));
        }

    }
}
