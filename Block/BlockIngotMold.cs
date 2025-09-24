using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using static OpenTK.Graphics.OpenGL.GL;

namespace Vintagestory.GameContent
{
    public class BlockIngotMold : Block
    {
        WorldInteraction[] interactionsLeft = null!;
        WorldInteraction[] interactionsRight = null!;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            if (api.Side != EnumAppSide.Client) return;

            if (LastCodePart() == "raw") return;

            interactionsLeft = ObjectCacheUtil.GetOrCreate(api, "ingotmoldBlockInteractionsLeft", () =>
            {
                List<ItemStack> smeltedContainerStacks = [];
                List<ItemStack> chiselStacks = [];

                foreach (CollectibleObject obj in api.World.Collectibles)
                {
                    if (obj is BlockSmeltedContainer)
                    {
                        smeltedContainerStacks.Add(new ItemStack(obj));
                    }

                    if (obj.Tool is EnumTool.Chisel)
                    {
                        chiselStacks.Add(new ItemStack(obj));
                    }
                }

                return new WorldInteraction[] {
                    new WorldInteraction()
                    {
                        ActionLangCode = "blockhelp-ingotmold-pour",
                        HotKeyCode = "shift",
                        MouseButton = EnumMouseButton.Right,
                        Itemstacks = [.. smeltedContainerStacks],
                        GetMatchingStacks = (wi, bs, es) =>
                        {
                            BlockEntityIngotMold? beim = api.World.BlockAccessor.GetBlockEntity(bs.Position) as BlockEntityIngotMold;
                            return (beim != null && !beim.IsFullLeft && !beim.ShatteredLeft) ? wi.Itemstacks : null;
                        }
                    },
                    new WorldInteraction()
                    {
                        ActionLangCode = "blockhelp-ingotmold-takeingot",
                        HotKeyCode = null,
                        MouseButton = EnumMouseButton.Right,
                        ShouldApply = (wi, bs, es) =>
                        {
                            BlockEntityIngotMold? beim = api.World.BlockAccessor.GetBlockEntity(bs.Position) as BlockEntityIngotMold;
                            return beim != null && beim.IsFullLeft && beim.IsHardenedLeft && !beim.ShatteredLeft;
                        }
                    },
                    new WorldInteraction()
                    {
                        ActionLangCode = "blockhelp-ingotmold-chiselmoldforbits",
                        HotKeyCode = null,
                        MouseButton = EnumMouseButton.Left,
                        Itemstacks = [.. chiselStacks],
                        GetMatchingStacks = (wi, bs, es) =>
                        {
                            BlockEntityIngotMold? beim = api.World.BlockAccessor.GetBlockEntity(bs.Position) as BlockEntityIngotMold;
                            return (beim != null && beim.FillLevelLeft > 0 && beim.IsHardenedLeft && ! beim.ShatteredLeft) ? wi.Itemstacks : null;
                        }
                    },
                    new WorldInteraction()
                    {
                        ActionLangCode = "blockhelp-ingotmold-pickup",
                        HotKeyCode = null,
                        RequireFreeHand = true,
                        MouseButton = EnumMouseButton.Right,
                        ShouldApply = (wi, bs, es) =>
                        {
                            BlockEntityIngotMold? beim = api.World.BlockAccessor.GetBlockEntity(bs.Position) as BlockEntityIngotMold;
                            return beim != null && beim.ContentsLeft == null && ! beim.ShatteredLeft;
                        }
                    },
                    new WorldInteraction()
                    {
                        ActionLangCode = "blockhelp-ingotmold-placemold",
                        HotKeyCode = "shift",
                        Itemstacks = [new(this)],
                        MouseButton = EnumMouseButton.Right,
                        GetMatchingStacks = (wi, bs, es) =>
                        {
                            BlockEntityIngotMold? beim = api.World.BlockAccessor.GetBlockEntity(bs.Position) as BlockEntityIngotMold;
                            return (beim != null && beim.QuantityMolds < 2) ? wi.Itemstacks : null;
                        }
                    }
                };
            });



            interactionsRight = ObjectCacheUtil.GetOrCreate(api, "ingotmoldBlockInteractionsRight", () =>
            {
                List<ItemStack> smeltedContainerStacks = [];
                List<ItemStack> chiselStacks = [];

                foreach (CollectibleObject obj in api.World.Collectibles)
                {
                    if (obj is BlockSmeltedContainer)
                    {
                        smeltedContainerStacks.Add(new ItemStack(obj));
                    }

                    if (obj.Tool is EnumTool.Chisel)
                    {
                        chiselStacks.Add(new ItemStack(obj));
                    }
                }

                return new WorldInteraction[] {
                    new WorldInteraction()
                    {
                        ActionLangCode = "blockhelp-ingotmold-pour",
                        HotKeyCode = "shift",
                        MouseButton = EnumMouseButton.Right,
                        Itemstacks = [.. smeltedContainerStacks],
                        GetMatchingStacks = (wi, bs, es) =>
                        {
                            BlockEntityIngotMold? beim = api.World.BlockAccessor.GetBlockEntity(bs.Position) as BlockEntityIngotMold;
                            return (beim != null && beim.QuantityMolds > 1 && !beim.IsFullRight && !beim.ShatteredRight) ? wi.Itemstacks : null;
                        }
                    },
                    new WorldInteraction()
                    {
                        ActionLangCode = "blockhelp-ingotmold-takeingot",
                        HotKeyCode = null,
                        MouseButton = EnumMouseButton.Right,
                        ShouldApply = (wi, bs, es) =>
                        {
                            BlockEntityIngotMold? beim = api.World.BlockAccessor.GetBlockEntity(bs.Position) as BlockEntityIngotMold;
                            return beim != null && beim.QuantityMolds > 1 && beim.IsFullRight && beim.IsHardenedRight && !beim.ShatteredRight;
                        }
                    },
                    new WorldInteraction()
                    {
                        ActionLangCode = "blockhelp-ingotmold-chiselmoldforbits",
                        HotKeyCode = null,
                        MouseButton = EnumMouseButton.Left,
                        Itemstacks = [.. chiselStacks],
                        GetMatchingStacks = (wi, bs, es) =>
                        {
                            BlockEntityIngotMold? beim = api.World.BlockAccessor.GetBlockEntity(bs.Position) as BlockEntityIngotMold;
                            return (beim != null && beim.QuantityMolds > 1 && beim.FillLevelRight > 0 && beim.IsHardenedRight && !beim.ShatteredRight) ? wi.Itemstacks : null;
                        }
                    },
                    new WorldInteraction()
                    {
                        ActionLangCode = "blockhelp-ingotmold-pickup",
                        HotKeyCode = null,
                        RequireFreeHand = true,
                        MouseButton = EnumMouseButton.Right,
                        ShouldApply = (wi, bs, es) =>
                        {
                            BlockEntityIngotMold? beim = api.World.BlockAccessor.GetBlockEntity(bs.Position) as BlockEntityIngotMold;
                            return beim != null && beim.QuantityMolds > 1 && beim.ContentsRight == null && !beim.ShatteredRight;
                        }
                    }
                };
            });


        }

        public override bool DoParticalSelection(IWorldAccessor world, BlockPos pos)
        {
            return true;
        }

        readonly Cuboidf[] oneMoldBoxes = [new(0, 0, 0, 1, 0.1875f, 1)];
        readonly Cuboidf[] twoMoldBoxesNS = [new(0, 0, 0, 0.5f, 0.1875f, 1), new(0.5f, 0, 0, 1, 0.1875f, 1)];
        readonly Cuboidf[] twoMoldBoxesEW = [new (0, 0, 0, 1, 0.1875f, 0.5f), new(0, 0, 0.5f, 1, 0.1875f, 1)];

        public override Cuboidf[] GetSelectionBoxes(IBlockAccessor world, BlockPos pos)
        {
            BlockEntityIngotMold? beim = api.World.BlockAccessor.GetBlockEntity(pos) as BlockEntityIngotMold;

            if (beim == null || beim.QuantityMolds == 1)
            {
                return oneMoldBoxes;
            }

            var faceing = BlockFacing.HorizontalFromAngle(beim.MeshAngle);
            switch (faceing.Index)
            {
                case 0:
                case 2:
                {
                    return twoMoldBoxesEW;
                }
                default:
                    return twoMoldBoxesNS;
            }
        }

        public override Cuboidf[] GetCollisionBoxes(IBlockAccessor blockAccessor, BlockPos pos)
        {
            return GetSelectionBoxes(blockAccessor, pos);
        }

        public override void GetDecal(IWorldAccessor world, BlockPos pos, ITexPositionSource decalTexSource, ref MeshData decalModelData, ref MeshData blockModelData)
        {
            var be = GetBlockEntity<BlockEntityIngotMold>(pos);
            if (be != null)
            {
                blockModelData = be.GetCurrentDecalMesh(be);
                decalModelData = be.GetCurrentDecalMesh(decalTexSource);

                if (be.QuantityMolds == 2)
                {
                    var side = be.IsRightSideSelected ? BlockEntityIngotMold.right : BlockEntityIngotMold.left;
                    blockModelData.Translate(side);
                    decalModelData.Translate(side);
                }

                blockModelData.Rotate(Vec3f.Half, 0, be.MeshAngle, 0);
                decalModelData.Rotate(Vec3f.Half, 0, be.MeshAngle, 0);

                return;
            }

            base.GetDecal(world, pos, decalTexSource, ref decalModelData, ref blockModelData);
        }

        public override void OnHeldInteractStart(ItemSlot itemslot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            if (blockSel == null)
            {
                base.OnHeldInteractStart(itemslot, byEntity, blockSel, entitySel, firstEvent, ref handling);
                return;
            }

            if (byEntity.World.BlockAccessor.GetBlockEntity(blockSel.Position.AddCopy(blockSel.Face.Opposite)) is BlockEntityIngotMold beim)
            {
                if (byEntity.World.PlayerByUid((byEntity as EntityPlayer)?.PlayerUID) is IPlayer byPlayer)
                {
                    if (beim.OnPlayerInteract(byPlayer, blockSel.Face, blockSel.HitPosition))
                    {
                        handling = EnumHandHandling.PreventDefault;
                    }
                }
            }
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (world.BlockAccessor.GetBlockEntity(blockSel?.Position) is BlockEntityIngotMold beim)
            {
                return beim.OnPlayerInteract(byPlayer, blockSel!.Face, blockSel.HitPosition);
            }

            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }

        public override void OnEntityCollide(IWorldAccessor world, Entity entity, BlockPos pos, BlockFacing facing, Vec3d collideSpeed, bool isImpact)
        {
            if (world.Rand.NextDouble() > 0.05)
            {
                base.OnEntityCollide(world, entity, pos, facing, collideSpeed, isImpact);
                return;
            }

            var beim = GetBlockEntity<BlockEntityIngotMold>(pos);
            if (beim?.TemperatureLeft > 300 || beim?.TemperatureRight > 300)
            {
                entity.ReceiveDamage(new DamageSource() { Source = EnumDamageSource.Block, SourceBlock = this, Type = EnumDamageType.Fire, SourcePos = pos.ToVec3d() }, 0.5f);
            }

            base.OnEntityCollide(world, entity, pos, facing, collideSpeed, isImpact);
        }

        public override float GetTraversalCost(BlockPos pos, EnumAICreatureType creatureType)
        {
            if (creatureType == EnumAICreatureType.LandCreature || creatureType == EnumAICreatureType.Humanoid)
            {
                var beim = GetBlockEntity<BlockEntityIngotMold>(pos);
                if (beim?.TemperatureLeft > 300 || beim?.TemperatureRight > 300) return 10000f;
            }

            return 0;
        }

        public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel, ref string failureCode)
        {
            if (!byPlayer.Entity.Controls.ShiftKey)
            {
                failureCode = "onlywhensneaking";
                return false;
            }

            if (!world.BlockAccessor.GetBlock(blockSel.Position.DownCopy()).CanAttachBlockAt(world.BlockAccessor, this, blockSel.Position.DownCopy(), BlockFacing.UP))
            {
                failureCode = "requiresolidground";
                return false;
            }

            return base.TryPlaceBlock(world, byPlayer, itemstack, blockSel, ref failureCode);
        }

        public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos)
        {
            return GetBlockEntity<BlockEntityIngotMold>(pos)?.SelectedMold ?? base.OnPickBlock(world, pos);
        }

        public override BlockSounds GetSounds(IBlockAccessor blockAccessor, BlockSelection blockSel, ItemStack? stack = null)
        {
            return GetBlockEntity<BlockEntityIngotMold>(blockSel.Position)?.SelectedMold?.Block?.Sounds ?? base.GetSounds(blockAccessor, blockSel, stack);
        }

        public override float OnGettingBroken(IPlayer player, BlockSelection blockSel, ItemSlot itemslot, float remainingResistance, float dt, int counter)
        {
            var be = GetBlockEntity<BlockEntityIngotMold>(blockSel.Position);

            be.BeingChiseled = player?.InventoryManager is IPlayerInventoryManager invMan && invMan.OffhandTool is EnumTool.Hammer && invMan.ActiveTool is EnumTool.Chisel;

            return base.OnGettingBroken(player, blockSel, itemslot, remainingResistance, dt, counter);
        }

        public override void OnBlockBroken(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1f)
        {
            if (world.BlockAccessor.GetBlockEntity(pos) is BlockEntityIngotMold beim)
            {
                if (byPlayer?.WorldData.CurrentGameMode != EnumGameMode.Creative && byPlayer?.CurrentBlockSelection is BlockSelection blockSel)
                {
                    beim.SetSelectedSide(blockSel.HitPosition);
                    if (byPlayer?.InventoryManager is IPlayerInventoryManager invMan && invMan.OffhandTool is EnumTool.Hammer && invMan.ActiveTool is EnumTool.Chisel)
                    {
                        ItemStack? drop = beim.GetChiseledStack(beim.SelectedContents, beim.SelectedFillLevel, beim.SelectedShattered, beim.SelectedIsHardened);

                        if (drop != null)
                        {
                            if (SplitDropStacks)
                            {
                                for (int k = 0; k < drop.StackSize; k++)
                                {
                                    ItemStack stack = drop.Clone();
                                    stack.StackSize = 1;
                                    world.SpawnItemEntity(stack, pos, null);
                                }
                            }
                            else
                            {
                                world.SpawnItemEntity(drop, pos, null);
                            }

                            world.PlaySoundAt(new AssetLocation("sounds/block/ingot"), pos, 0, byPlayer);

                            beim.SelectedContents = null;
                            beim.SelectedFillLevel = 0;

                            DamageItem(world, byPlayer.Entity, invMan.ActiveHotbarSlot);
                            DamageItem(world, byPlayer.Entity, byPlayer.Entity?.LeftHandItemSlot);
                            return;
                        }
                    }
                    else
                    {
                        ItemStack[] drops = beim.GetStateAwareMoldSided(beim.SelectedMold, beim.SelectedShattered);

                        if (drops.Length > 0)
                        {
                            foreach (var drop in drops)
                            {
                                if (SplitDropStacks)
                                {
                                    for (int k = 0; k < drop.StackSize; k++)
                                    {
                                        ItemStack stack = drop.Clone();
                                        stack.StackSize = 1;
                                        world.SpawnItemEntity(stack, pos, null);
                                    }
                                }
                                else
                                {
                                    world.SpawnItemEntity(drop, pos, null);
                                }
                            }

                            if (!beim.IsRightSideSelected && beim.QuantityMolds > 1)
                            {
                                if (beim.MoldRight == null)
                                {
                                    beim.QuantityMolds--;

                                    beim.ContentsRight = null;
                                    beim.FillLevelRight = 0;
                                    beim.ShatteredRight = false;
                                    return;
                                }

                                beim.MoldLeft = beim.MoldRight;
                                beim.MoldMeshLeft = beim.MoldMeshRight;
                                beim.ContentsLeft = beim.ContentsRight;
                                beim.FillLevelLeft = beim.FillLevelRight;
                                beim.ShatteredLeft = beim.ShatteredRight;
                                world.BlockAccessor.ExchangeBlock(beim.MoldLeft.Block.BlockId, pos);
                                beim.MoldRight = null;
                                beim.ContentsRight = null;
                                beim.FillLevelRight = 0;
                                beim.ShatteredRight = false;
                            }

                            beim.QuantityMolds--;

                            if (beim.QuantityMolds <= 0)
                            {
                                world.BlockAccessor.SetBlock(0, pos);
                            }
                            else
                            {
                                beim.MoldRight = null;
                                beim.MarkDirty(true);
                            }

                            world.PlaySoundAt(beim.SelectedMold?.Block.Sounds?.GetBreakSound(byPlayer), pos, 0, byPlayer);
                            return;
                        }
                    }
                }
            }

            base.OnBlockBroken(world, pos, byPlayer, dropQuantityMultiplier);
        }

        public override BlockDropItemStack[] GetDropsForHandbook(ItemStack handbookStack, IPlayer forPlayer)
        {
            return Drops;
        }

        public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1)
        {
            if (world.BlockAccessor.GetBlockEntity(pos) is BlockEntityIngotMold beim)
            {
                return [ .. beim.GetStateAwareMolds(), .. beim.GetStateAwareMoldedStacks(), ];
            }

            return [ new ItemStack(this) ];
        }

        public override string GetPlacedBlockName(IWorldAccessor world, BlockPos pos)
        {
            if (world.BlockAccessor.GetBlockEntity(pos) is BlockEntityIngotMold beim)
            {
                if (beim.SelectedMold == null) return base.GetPlacedBlockName(world, pos);

                if (!beim.SelectedShattered) return beim.SelectedMold.GetName();
                else return Lang.Get("ceramicblock-blockname-shattered", beim.SelectedMold.GetName());
            }

            return base.GetPlacedBlockName(world, pos);
        }

        public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer)
        {
            if (world.BlockAccessor.GetBlockEntity(selection.Position) is BlockEntityIngotMold beim)
            {
                beim.SetSelectedSide(selection.HitPosition);

                return (beim.IsRightSideSelected ? interactionsRight : interactionsLeft).Append(base.GetPlacedBlockInteractionHelp(world, selection, forPlayer));
            }

            return (selection.SelectionBoxIndex == 0 ? interactionsLeft : interactionsRight).Append(base.GetPlacedBlockInteractionHelp(world, selection, forPlayer));
        }

        public override bool DoPlaceBlock(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ItemStack byItemStack)
        {
            bool val = base.DoPlaceBlock(world, byPlayer, blockSel, byItemStack);

            if (val && world.BlockAccessor.GetBlockEntity(blockSel.Position) is BlockEntityIngotMold beim)
            {
                beim.MoldLeft = byItemStack?.Clone() ?? byPlayer.InventoryManager.ActiveHotbarSlot.Itemstack?.Clone() ?? new ItemStack(this);
                if (beim.MoldLeft != null) beim.MoldLeft.StackSize = 1;

                var targetPos = blockSel.DidOffset ? blockSel.Position.AddCopy(blockSel.Face.Opposite) : blockSel.Position;
                var dx = byPlayer.Entity.Pos.X - (targetPos.X + blockSel.HitPosition.X);
                var dz = byPlayer.Entity.Pos.Z - (targetPos.Z + blockSel.HitPosition.Z);
                var angleHor = (float)Math.Atan2(dx, dz);

                var roundRad = ((int)Math.Round(angleHor / GameMath.PIHALF)) * GameMath.PIHALF;
                beim.MeshAngle = roundRad;
                beim.MarkDirty();
            }

            return val;
        }
    }
}
