using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

#nullable disable

namespace Vintagestory.GameContent
{
    public class ItemOpenedBeenade : Item
    {
        WorldInteraction[] interactions;

        public override void OnLoaded(ICoreAPI api)
        {
            if (api.Side != EnumAppSide.Client) return;
            ICoreClientAPI capi = api as ICoreClientAPI;

            interactions = ObjectCacheUtil.GetOrCreate(api, "openedBeenadeInteractions", () =>
            {
                List<ItemStack> stacks = new List<ItemStack>();

                foreach (Block block in api.World.Blocks)
                {
                    if (block.Code == null) continue;

                    if (block is BlockSkep && block.Variant["type"].Equals("populated"))
                    {
                        stacks.Add(new ItemStack(block));
                    }
                }

                return new WorldInteraction[]
                {
                    new WorldInteraction()
                    {
                        ActionLangCode = "heldhelp-fill",
                        MouseButton = EnumMouseButton.Right,
                        Itemstacks = stacks.ToArray()
                    }
                };
            });
        }


        public override string GetHeldTpUseAnimation(ItemSlot activeHotbarSlot, Entity byEntity)
        {
            return null;
        }

        public override void OnHeldInteractStart(ItemSlot itemslot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            if (blockSel == null) return;

            if (!byEntity.World.Claims.TryAccess((byEntity as EntityPlayer)?.Player, blockSel.Position, EnumBlockAccessFlags.BuildOrBreak))
            {
                return;
            }

            Block block = byEntity.World.BlockAccessor.GetBlock(blockSel.Position);
            if (block is BlockSkep && block.Variant["type"].Equals("populated"))
            {
                handling = EnumHandHandling.PreventDefaultAction;
            }
        }

        public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            if (blockSel == null) return false;

            if (byEntity.World is IClientWorldAccessor)
            {
                ModelTransform tf = new ModelTransform();
                tf.EnsureDefaultValues();

                float offset = GameMath.Clamp(secondsUsed * 3, 0, 2f);

                tf.Translation.Set(-offset, offset / 4f, 0);
            }

            SimpleParticleProperties bees = BlockEntityBeehive.Bees;
            BlockPos pos = blockSel.Position;
            Random rand = byEntity.World.Rand;

            Vec3d startPos = new Vec3d(pos.X + rand.NextDouble(), pos.Y + rand.NextDouble() * 0.25f, pos.Z + rand.NextDouble());
            Vec3d endPos = new Vec3d(byEntity.Pos.X, byEntity.Pos.Y + byEntity.LocalEyePos.Y - 0.2f, byEntity.Pos.Z);

            Vec3f minVelo = new Vec3f((float)(endPos.X - startPos.X), (float)(endPos.Y - startPos.Y), (float)(endPos.Z - startPos.Z));
            minVelo.Normalize();
            minVelo *= 2;

            bees.MinPos = startPos;
            bees.MinVelocity = minVelo;
            bees.WithTerrainCollision = true;

            IPlayer byPlayer = null;
            if (byEntity is EntityPlayer) byPlayer = byEntity.World.PlayerByUid(((EntityPlayer)byEntity).PlayerUID);

            byEntity.World.SpawnParticles(bees, byPlayer);

            return secondsUsed < 4;
        }


        public override bool OnHeldInteractCancel(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, EnumItemUseCancelReason cancelReason)
        {

            return true;
        }

        public override void OnHeldInteractStop(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            if (blockSel == null) return;

            if (!byEntity.World.Claims.TryAccess((byEntity as EntityPlayer)?.Player, blockSel.Position, EnumBlockAccessFlags.BuildOrBreak))
            {
                return;
            }

            Block block = byEntity.World.BlockAccessor.GetBlock(blockSel.Position);
            bool ok = block is BlockSkep && block.Variant["type"].Equals("populated");
            if (!ok) return;

            if (secondsUsed < 3.9f) return;


            slot.TakeOut(1);
            slot.MarkDirty();

            IPlayer byPlayer = null;
            if (byEntity is EntityPlayer) byPlayer = byEntity.World.PlayerByUid(((EntityPlayer)byEntity).PlayerUID);
            byPlayer?.InventoryManager.TryGiveItemstack(new ItemStack(byEntity.World.GetItem(CodeWithVariant("type", "closed"))));

            Block emptySkepBlock = byEntity.World.GetBlock(block.CodeWithVariant("type", "empty"));
            byEntity.World.BlockAccessor.SetBlock(emptySkepBlock.BlockId, blockSel.Position);
        }


        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);
            if (inSlot.Itemstack.Collectible.Attributes == null) return;
            dsc.AppendLine(Lang.Get("Fill it up with bees and throw it for a stingy surprise"));
        }

        public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot)
        {
            return interactions.Append(base.GetHeldInteractionHelp(inSlot));
        }


    }
}
