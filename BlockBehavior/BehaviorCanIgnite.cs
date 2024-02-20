using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class BlockBehaviorCanIgnite : BlockBehavior
    {
        static List<ItemStack> canIgniteStacks = new List<ItemStack>();
        static List<ItemStack> canIgniteStacksWithFirestarter = new List<ItemStack>();

        static public List<ItemStack> CanIgniteStacks(ICoreAPI api, bool withFirestarter)
        {
            if (canIgniteStacks.Count == 0)
            {
                foreach (CollectibleObject obj in api.World.Collectibles)
                {
                    if (obj is Block block)
                    {
                        if (block.HasBehavior<BlockBehaviorCanIgnite>())
                        {
                            List<ItemStack> stacks = obj.GetHandBookStacks(api as ICoreClientAPI);
                            if (stacks != null)
                            {
                                canIgniteStacks.AddRange(stacks);
                                canIgniteStacksWithFirestarter.AddRange(stacks);
                            }
                        }
                    }
                    else if (obj is ItemFirestarter)
                    {
                        List<ItemStack> stacks = obj.GetHandBookStacks(api as ICoreClientAPI);
                        canIgniteStacksWithFirestarter.AddRange(stacks);
                    }
                }
            }

            return withFirestarter ? canIgniteStacksWithFirestarter : canIgniteStacks;
        }

        public override void OnUnloaded(ICoreAPI api)
        {
            canIgniteStacks.Clear();
            canIgniteStacksWithFirestarter.Clear();
        }


        public BlockBehaviorCanIgnite(Block block) : base(block)
        {
        }



        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling, ref EnumHandling blockHandling)
        {
            if (blockSel == null) return;
            Block block = byEntity.World.BlockAccessor.GetBlock(blockSel.Position);

            IPlayer byPlayer = (byEntity as EntityPlayer)?.Player;
            if (!byEntity.World.Claims.TryAccess(byPlayer, blockSel.Position, EnumBlockAccessFlags.Use))
            {
                return;
            }


            EnumIgniteState state = EnumIgniteState.NotIgnitable;

            IIgnitable ign = block.GetInterface<IIgnitable>(byEntity.World, blockSel.Position);
            if (ign != null)
            {
                state = ign.OnTryIgniteBlock(byEntity, blockSel.Position, 0);
            }

            if (state == EnumIgniteState.NotIgnitablePreventDefault)
            {
                blockHandling = EnumHandling.PreventDefault;
                handHandling = EnumHandHandling.PreventDefault;
            }

            if (!byEntity.Controls.ShiftKey && state != EnumIgniteState.Ignitable)
            {
                return;
            }           

            blockHandling = EnumHandling.PreventDefault;
            handHandling = EnumHandHandling.PreventDefault;

            byEntity.World.PlaySoundAt(new AssetLocation("sounds/torch-ignite"), byEntity, byPlayer, false, 16);
        }

        public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandling handling)
        {
            if (blockSel == null) return false;

            IPlayer byPlayer = (byEntity as EntityPlayer)?.Player;
            if (!byEntity.World.Claims.TryAccess(byPlayer, blockSel.Position, EnumBlockAccessFlags.Use))
            {
                return false;
            }


            Block block = byEntity.World.BlockAccessor.GetBlock(blockSel.Position);

            EnumIgniteState igniteState = EnumIgniteState.NotIgnitable;

            IIgnitable ign = block.GetInterface<IIgnitable>(byEntity.World, blockSel.Position);
            if (ign != null) igniteState = ign.OnTryIgniteBlock(byEntity, blockSel.Position, secondsUsed);
            if (igniteState == EnumIgniteState.NotIgnitablePreventDefault) return false;

            handling = EnumHandling.PreventDefault;

            if (byEntity.World is IClientWorldAccessor)
            {
                ModelTransform tf = new ModelTransform();
                tf.EnsureDefaultValues();

                tf.Translation.Set(0, Math.Min(1.1f / 3, secondsUsed * 4 / 3f)/2, -Math.Min(1.1f, secondsUsed * 4));
                tf.Rotation.X = -Math.Min(30, secondsUsed * 90 * 2f);
                tf.Rotation.Z = -Math.Min(20, secondsUsed * 90 * 4f);
                byEntity.Controls.UsingHeldItemTransformBefore = tf;


                if (secondsUsed > 0.25f && (int)(30 * secondsUsed) % 2 == 1)
                {
                    Random rand = byEntity.World.Rand;
                    Vec3d pos = blockSel.Position.ToVec3d().Add(blockSel.HitPosition).Add(rand.NextDouble()*0.25 - 0.125, rand.NextDouble() * 0.25 - 0.125, rand.NextDouble() * 0.25 - 0.125);

                    Block blockFire = byEntity.World.GetBlock(new AssetLocation("fire"));

                    AdvancedParticleProperties props = blockFire.ParticleProperties[blockFire.ParticleProperties.Length - 1].Clone();
                    props.basePos = pos;
                    props.Quantity.avg = 0.5f;

                    byEntity.World.SpawnParticles(props, byPlayer);

                    props.Quantity.avg = 0;
                }
            }


            // Crappy fix to make igniting not buggy T_T
            if (byEntity.World.Side == EnumAppSide.Server) return true;

            return secondsUsed <= 3.2;
        }

        public override void OnHeldInteractStop(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandling handling)
        {
            if (blockSel == null || secondsUsed < 3) return;

            IPlayer byPlayer = (byEntity as EntityPlayer)?.Player;
            if (!byEntity.World.Claims.TryAccess(byPlayer, blockSel.Position, EnumBlockAccessFlags.Use))
            {
                return;
            }


            EnumHandling handled = EnumHandling.PassThrough;
            Block block = byEntity.World.BlockAccessor.GetBlock(blockSel.Position);
            IIgnitable ign = block.GetInterface<IIgnitable>(byEntity.World, blockSel.Position);
            ign?.OnTryIgniteBlockOver(byEntity, blockSel.Position, secondsUsed, ref handled);

            if (handled != EnumHandling.PassThrough)
            {
                return;
            }

            handling = EnumHandling.PreventDefault;

            if (!byEntity.World.Claims.TryAccess(byPlayer, blockSel.Position, EnumBlockAccessFlags.BuildOrBreak))
            {
                return;
            }

            if (blockSel != null && byEntity.World.Side == EnumAppSide.Server)
            {
                BlockPos bpos = blockSel.Position.AddCopy(blockSel.Face);
                block = byEntity.World.BlockAccessor.GetBlock(bpos);

                if (block.BlockId == 0)
                {
                    byEntity.World.BlockAccessor.SetBlock(byEntity.World.GetBlock(new AssetLocation("fire")).BlockId, bpos);
                    BlockEntity befire = byEntity.World.BlockAccessor.GetBlockEntity(bpos) as BlockEntity;
                    befire?.GetBehavior<BEBehaviorBurning>()?.OnFirePlaced(blockSel.Face, (byEntity as EntityPlayer).PlayerUID);
                }
            }
        }

        public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot, ref EnumHandling handling)
        {
            WorldInteraction[] interactions = new WorldInteraction[]
            {
                new WorldInteraction
                {
                    HotKeyCode = "shift",
                    ActionLangCode = "heldhelp-igniteblock",
                    MouseButton = EnumMouseButton.Right
                }
            };

            return base.GetHeldInteractionHelp(inSlot, ref handling).Append(interactions);
        }

    }
}
