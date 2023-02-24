using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public interface IBlockItemPile
    {
        bool Construct(ItemSlot slot, IWorldAccessor world, BlockPos pos, IPlayer byPlayer);
    }

    public interface IBlockEntityItemPile
    {
        bool OnPlayerInteract(IPlayer byPlayer);
    }

    public class BlockCoalPile : Block, IBlockItemPile, IIgnitable
    {
        Cuboidf[][] CollisionBoxesByFillLevel;

        public BlockCoalPile()
        {
            CollisionBoxesByFillLevel = new Cuboidf[9][];

            for (int i = 0; i <= 8; i++)
            {
                CollisionBoxesByFillLevel[i] = new Cuboidf[] { new Cuboidf(0, 0, 0, 1, i * 0.125f, 1) };
            }
        }

        WorldInteraction[] interactions;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            if (api.Side != EnumAppSide.Client) return;
            ICoreClientAPI capi = api as ICoreClientAPI;

            interactions = ObjectCacheUtil.GetOrCreate(api, "coalBlockInteractions", () =>
            {
                List<ItemStack> canIgniteStacks = BlockBehaviorCanIgnite.CanIgniteStacks(api, false);

                return new WorldInteraction[] {
                    new WorldInteraction()
                    {
                        ActionLangCode = "blockhelp-coalpile-addcoal",
                        MouseButton = EnumMouseButton.Right,
                        HotKeyCode = "shift",
                        Itemstacks = new ItemStack[] { new ItemStack(api.World.GetItem(new AssetLocation("charcoal")), 2) }
                    },
                    new WorldInteraction()
                    {
                        ActionLangCode = "blockhelp-coalpile-removecoal",
                        MouseButton = EnumMouseButton.Right,
                        HotKeyCode = null
                    },
                    new WorldInteraction()
                    {
                        ActionLangCode = "blockhelp-forge-ignite",
                        HotKeyCode = "shift",
                        MouseButton = EnumMouseButton.Right,
                        Itemstacks = canIgniteStacks.ToArray(),
                        GetMatchingStacks = (wi, bs, es) => {
                            BlockEntityForge bef = api.World.BlockAccessor.GetBlockEntity(bs.Position) as BlockEntityForge;
                            if (bef!= null && bef.CanIgnite && !bef.IsBurning)
                            {
                                return wi.Itemstacks;
                            }
                            return null;
                        }
                    }
                };
            });
        }


        public override void GetDecal(IWorldAccessor world, BlockPos pos, ITexPositionSource decalTexSource, ref MeshData decalModelData, ref MeshData blockModelData)
        {
            BlockEntityCoalPile bea = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityCoalPile;
            if (bea == null)
            {
                base.GetDecal(world, pos, decalTexSource, ref decalModelData, ref blockModelData);
                return;
            }

            decalModelData.Clear();
            bea.GetDecalMesh(decalTexSource, out decalModelData);
        }

        public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos)
        {
            BlockEntityCoalPile bea = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityCoalPile;
            if (bea != null) return bea.inventory[0]?.Itemstack?.Clone();

            return base.OnPickBlock(world, pos);
        }

        public int GetLayercount(IWorldAccessor world, BlockPos pos)
        {
            BlockEntityCoalPile bea = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityCoalPile;
            if (bea != null) return bea.Layers;
            return 0;
        }

        public override Cuboidf[] GetCollisionBoxes(IBlockAccessor blockAccessor, BlockPos pos)
        {
            BlockEntityCoalPile bea = blockAccessor.GetBlockEntity(pos) as BlockEntityCoalPile;
            if (bea == null) return new Cuboidf[0];

            return CollisionBoxesByFillLevel[bea.Layers];
        }

        public override Cuboidf[] GetSelectionBoxes(IBlockAccessor blockAccessor, BlockPos pos)
        {
            BlockEntityCoalPile bea = blockAccessor.GetBlockEntity(pos) as BlockEntityCoalPile;
            if (bea == null) return new Cuboidf[0];

            return CollisionBoxesByFillLevel[bea.Layers];
        }


        public override BlockDropItemStack[] GetDropsForHandbook(ItemStack handbookStack, IPlayer forPlayer)
        {
            return new BlockDropItemStack[0];
        }

        public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1f)
        {
            // Handled by BlockEntityItemPile
            return new ItemStack[0];
        }


        public override bool OnFallOnto(IWorldAccessor world, BlockPos pos, Block block, TreeAttribute blockEntityAttributes)
        {
            if (block is BlockCoalPile)
            {
                BlockEntityCoalPile be = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityCoalPile;
                if (be != null)
                {
                    return be.MergeWith(blockEntityAttributes);
                }
            }

            return base.OnFallOnto(world, pos, block, blockEntityAttributes);
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            BlockEntity be = world.BlockAccessor.GetBlockEntity(blockSel.Position);
            if (be is BlockEntityCoalPile)
            {
                BlockEntityCoalPile pile = (BlockEntityCoalPile)be;
                return pile.OnPlayerInteract(byPlayer);
            }

            return false;
        }


        public override byte[] GetLightHsv(IBlockAccessor blockAccessor, BlockPos pos, ItemStack stack = null)
        {
            if (pos == null) return base.GetLightHsv(blockAccessor, pos, stack);

            BlockEntityCoalPile bea = blockAccessor.GetBlockEntity(pos) as BlockEntityCoalPile;
            if (bea?.IsBurning == true) return new byte[] { 0, 7, 8 };

            return base.GetLightHsv(blockAccessor, pos, stack);
        }


        public EnumIgniteState OnTryIgniteBlock(EntityAgent byEntity, BlockPos pos, float secondsIgniting)
        {
            BlockEntityCoalPile bea = byEntity.World.BlockAccessor.GetBlockEntity(pos) as BlockEntityCoalPile;

            if (bea == null || !bea.CanIgnite)
            {
                return EnumIgniteState.NotIgnitablePreventDefault;
            }

            if (secondsIgniting > 0.25f && (int)(30 * secondsIgniting) % 9 == 1)
            {
                Random rand = byEntity.World.Rand;
                Vec3d dpos = new Vec3d(pos.X + 2 / 8f + 4 / 8f * rand.NextDouble(), pos.Y + 7 / 8f, pos.Z + 2 / 8f + 4 / 8f * rand.NextDouble());

                Block blockFire = byEntity.World.GetBlock(new AssetLocation("fire"));

                AdvancedParticleProperties props = blockFire.ParticleProperties[blockFire.ParticleProperties.Length - 1];
                props.basePos = dpos;
                props.Quantity.avg = 1;

                IPlayer byPlayer = null;
                if (byEntity is EntityPlayer) byPlayer = byEntity.World.PlayerByUid(((EntityPlayer)byEntity).PlayerUID);

                byEntity.World.SpawnParticles(props, byPlayer);

                props.Quantity.avg = 0;
            }

            if (secondsIgniting >= 1.5f)
            {
                return EnumIgniteState.IgniteNow;
            }

            return EnumIgniteState.Ignitable;
        }

        public void OnTryIgniteBlockOver(EntityAgent byEntity, BlockPos pos, float secondsIgniting, ref EnumHandling handling)
        {
            if (secondsIgniting < 1.45f) return;

            handling = EnumHandling.PreventDefault;

            IPlayer byPlayer = null;
            if (byEntity is EntityPlayer) byPlayer = byEntity.World.PlayerByUid(((EntityPlayer)byEntity).PlayerUID);
            if (byPlayer == null) return;

            BlockEntityCoalPile bea = byEntity.World.BlockAccessor.GetBlockEntity(pos) as BlockEntityCoalPile;
            bea?.TryIgnite();
        }





        public bool Construct(ItemSlot slot, IWorldAccessor world, BlockPos pos, IPlayer player)
        {
            Block block = world.BlockAccessor.GetBlock(pos);
            if (!block.IsReplacableBy(this)) return false;
            Block belowBlock = world.BlockAccessor.GetBlock(pos.DownCopy());
            if (!belowBlock.CanAttachBlockAt(world.BlockAccessor, this, pos.DownCopy(), BlockFacing.UP) /*&& (belowBlock != this || FillLevel(world.BlockAccessor, pos.DownCopy()) != 4)*/) return false;

            world.BlockAccessor.SetBlock(BlockId, pos);

            BlockEntity be = world.BlockAccessor.GetBlockEntity(pos);
            if (be is BlockEntityCoalPile)
            {
                BlockEntityCoalPile pile = (BlockEntityCoalPile)be;
                if (player == null || player.WorldData.CurrentGameMode != EnumGameMode.Creative)
                {
                    pile.inventory[0].Itemstack = (ItemStack)slot.TakeOut(player?.Entity.Controls.CtrlKey == true ? pile.BulkTakeQuantity : pile.DefaultTakeQuantity);
                    slot.MarkDirty();
                }
                else
                {
                    pile.inventory[0].Itemstack = (ItemStack)slot.Itemstack.Clone();
                    pile.inventory[0].Itemstack.StackSize = Math.Min(pile.inventory[0].Itemstack.StackSize, pile.MaxStackSize);
                }

                pile.MarkDirty();
                world.BlockAccessor.MarkBlockDirty(pos);
                world.PlaySoundAt(pile.SoundLocation, pos.X, pos.Y, pos.Z, player, true);
            }

            return true;
        }

        public override void OnNeighbourBlockChange(IWorldAccessor world, BlockPos pos, BlockPos neibpos)
        {
            Block belowBlock = world.BlockAccessor.GetBlock(pos.DownCopy());
            if (!belowBlock.CanAttachBlockAt(world.BlockAccessor, this, pos.DownCopy(), BlockFacing.UP))
            {
                world.BlockAccessor.BreakBlock(pos, null);
                return;
            }
      
            Block neibBlock = world.BlockAccessor.GetBlock(neibpos);
            Block neibliqBlock = world.BlockAccessor.GetBlock(neibpos, BlockLayersAccess.Fluid);
            if (neibBlock.Attributes?.IsTrue("smothersFire") == true || neibliqBlock.Attributes?.IsTrue("smothersFire") == true)
            {
                var becp = api.World.BlockAccessor.GetBlockEntity(pos) as BlockEntityCoalPile;
                becp?.Extinguish();
            }

            base.OnNeighbourBlockChange(world, pos, neibpos);
        }


        public override bool CanAttachBlockAt(IBlockAccessor blockAccessor, Block block, BlockPos pos, BlockFacing blockFace, Cuboidi attachmentArea = null)
        {
            BlockEntityCoalPile be = blockAccessor.GetBlockEntity(pos) as BlockEntityCoalPile;
            if (be != null)
            {
                return be.OwnStackSize == be.MaxStackSize;
            }

            return base.CanAttachBlockAt(blockAccessor, block, pos, blockFace, attachmentArea);
        }

        public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer)
        {
            return interactions.Append(base.GetPlacedBlockInteractionHelp(world, selection, forPlayer));
        }

        public override void OnEntityCollide(IWorldAccessor world, Entity entity, BlockPos pos, BlockFacing facing, Vec3d collideSpeed, bool isImpact)
        {
            base.OnEntityCollide(world, entity, pos, facing, collideSpeed, isImpact);

            if (world.Side == EnumAppSide.Server)
            {
                BlockEntityCoalPile be = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityCoalPile;
                if (be.IsBurning)
                {
                    long lastBurnMs = entity.Attributes.GetLong("lastCoalBurnTick");

                    if (world.ElapsedMilliseconds - lastBurnMs > 1000)
                    {
                        entity.ReceiveDamage(new DamageSource() { DamageTier = 0, Source = EnumDamageSource.Block, SourceBlock = this, SourcePos = pos.ToVec3d(), Type = EnumDamageType.Fire }, 1f);
                        entity.Attributes.SetLong("lastCoalBurnTick", world.ElapsedMilliseconds);

                        if (world.Rand.NextDouble() < 0.125)
                        {
                            entity.Ignite();
                        }
                    }

                    if (lastBurnMs > world.ElapsedMilliseconds)
                    {
                        entity.Attributes.SetLong("lastCoalBurnTick", world.ElapsedMilliseconds);
                    }
                }

                
            }
        }

    }
}
