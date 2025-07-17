using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.GameContent
{
    public class BlockStaticTranslocator : Block
    {
        public SimpleParticleProperties idleParticles;
        public SimpleParticleProperties insideParticles;
        public SimpleParticleProperties teleportParticles;

        public bool Repaired => Variant["state"] != "broken";


        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            idleParticles = new SimpleParticleProperties(
                0.5f, 1,
                ColorUtil.ToRgba(150, 34, 47, 44),
                new Vec3d(),
                new Vec3d(),
                new Vec3f(-0.1f, -0.1f, -0.1f),
                new Vec3f(0.1f, 0.1f, 0.1f),
                1.5f,
                0,
                0.5f,
                0.75f,
                EnumParticleModel.Quad
            );

            idleParticles.SizeEvolve = EvolvingNatFloat.create(EnumTransformFunction.QUADRATIC, -0.6f);
            idleParticles.AddPos.Set(1, 2, 1);
            idleParticles.addLifeLength = 0.5f;
            idleParticles.RedEvolve = new EvolvingNatFloat(EnumTransformFunction.LINEAR, 80);


            insideParticles = new SimpleParticleProperties(
                0.5f, 1,
                ColorUtil.ToRgba(150, 92, 111, 107),
                new Vec3d(),
                new Vec3d(),
                new Vec3f(-0.2f, -0.2f, -0.2f),
                new Vec3f(0.2f, 0.2f, 0.2f),
                1.5f,
                0,
                0.5f,
                0.75f,
                EnumParticleModel.Quad
            );

            insideParticles.SizeEvolve = EvolvingNatFloat.create(EnumTransformFunction.QUADRATIC, -0.6f);
            insideParticles.AddPos.Set(1, 2, 1);
            insideParticles.addLifeLength = 0.5f;



            teleportParticles = new SimpleParticleProperties(
                0.5f, 1,
                ColorUtil.ToRgba(150, 92, 111, 107),
                new Vec3d(),
                new Vec3d(),
                new Vec3f(-0.2f, -0.2f, -0.2f),
                new Vec3f(0.2f, 0.2f, 0.2f),
                4.5f,
                0,
                0.5f,
                0.75f,
                EnumParticleModel.Quad
            );

            teleportParticles.OpacityEvolve = EvolvingNatFloat.create(EnumTransformFunction.QUADRATIC, -1f);
            teleportParticles.AddPos.Set(1, 2, 1);
            teleportParticles.addLifeLength = 0.5f;
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            ItemSlot slot = byPlayer.InventoryManager.ActiveHotbarSlot;
            if (slot.Empty) return base.OnBlockInteractStart(world, byPlayer, blockSel);

            if (!Repaired)
            {
                if (slot.Itemstack.Collectible.Code.Path == "metal-parts" && slot.StackSize >= 2)
                {
                    slot.TakeOut(2);
                    world.PlaySoundAt(new AssetLocation("sounds/effect/latch"), blockSel.Position, -0.25, byPlayer, true, 16);

                    Block block = world.GetBlock(CodeWithVariant("state", "normal"));
                    world.BlockAccessor.SetBlock(block.Id, blockSel.Position);

                    BlockEntityStaticTranslocator be = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityStaticTranslocator;
                    if (be != null) be.DoRepair(byPlayer);

                    return true;
                }

                
            } else
            {
                BlockEntityStaticTranslocator be = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityStaticTranslocator;
                if (be == null) return false;

                if (!be.FullyRepaired && slot.Itemstack.Collectible is ItemTemporalGear)
                {
                    be.DoRepair(byPlayer);
                    slot.TakeOut(1);
                    world.PlaySoundAt(new AssetLocation("sounds/effect/latch"), blockSel.Position, -0.25, byPlayer, true, 16);

                    return true;
                }

                /*if (!be.Activated && slot.Itemstack.Collectible.Code.Path == "gear-rusty")
                {
                    be.DoActivate();
                    slot.TakeOut(1);
                    world.PlaySoundAt(new AssetLocation("sounds/effect/latch"), blockSel.Position.X + 0.5, blockSel.Position.Y, blockSel.Position.Z + 0.5, byPlayer, true, 16);
                    return true;
                }*/
            }

            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }

        public override void OnEntityCollide(IWorldAccessor world, Entity entity, BlockPos pos, BlockFacing facing, Vec3d collideSpeed, bool isImpact)
        {
            base.OnEntityCollide(world, entity, pos, facing, collideSpeed, isImpact);

            BlockEntityStaticTranslocator be = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityStaticTranslocator;
            if (be == null) return;
            be.OnEntityCollide(entity);
        }

        public override string GetPlacedBlockInfo(IWorldAccessor world, BlockPos pos, IPlayer forPlayer)
        {
            if (!Repaired)
            {
                return Lang.Get("Seems to be missing a couple of gears. I think I've seen such gears before.");
            }

            return base.GetPlacedBlockInfo(world, pos, forPlayer);
        }

        public override string GetPlacedBlockName(IWorldAccessor world, BlockPos pos)
        {
            if (Repaired)
            {
                BlockEntityStaticTranslocator be = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityStaticTranslocator;
                if (be == null) return base.GetPlacedBlockName(world, pos);

                if (!be.FullyRepaired)
                {
                    return world.GetBlock(CodeWithVariant("state", "broken")).GetPlacedBlockName(world, pos);
                }
            }

            return base.GetPlacedBlockName(world, pos);
        }


        public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer)
        {
            if (!Repaired)
            {
                return new WorldInteraction[] {
                    new WorldInteraction()
                    {
                        ActionLangCode = "blockhelp-translocator-repair-1",
                        Itemstacks = new ItemStack[] { new ItemStack(world.GetBlock(new AssetLocation("metal-parts")), 2) },
                        MouseButton = EnumMouseButton.Right
                    }
                };
            } else
            {
                BlockEntityStaticTranslocator be = world.BlockAccessor.GetBlockEntity(selection.Position) as BlockEntityStaticTranslocator;
                if (be == null) return base.GetPlacedBlockInteractionHelp(world, selection, forPlayer);

                if (!be.FullyRepaired)
                {
                    return new WorldInteraction[]
                    {
                        new WorldInteraction()
                        {
                            ActionLangCode = "blockhelp-translocator-repair-2",
                            Itemstacks = new ItemStack[] { new ItemStack(world.GetItem(new AssetLocation("gear-temporal")), 3) },
                            MouseButton = EnumMouseButton.Right
                        }
                    };
                }

                if (!be.Activated)
                {
                    return new WorldInteraction[]
                    {
                        new WorldInteraction()
                        {
                            ActionLangCode = "blockhelp-translocator-activate",
                            Itemstacks = new ItemStack[] { new ItemStack(world.GetItem(new AssetLocation("gear-rusty"))) },
                            MouseButton = EnumMouseButton.Right
                        }
                    };
                }
            }

            return base.GetPlacedBlockInteractionHelp(world, selection, forPlayer);
        }

        public override void OnBlockBroken(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1)
        {
            if (byPlayer.WorldData.CurrentGameMode != EnumGameMode.Creative) return;

            base.OnBlockBroken(world, pos, byPlayer, dropQuantityMultiplier);
        }


        public override AssetLocation GetRotatedBlockCode(int angle)
        {
            BlockFacing beforeFacing = BlockFacing.FromCode(LastCodePart());
            int rotatedIndex = GameMath.Mod(beforeFacing.HorizontalAngleIndex - angle / 90, 4);
            BlockFacing nowFacing = BlockFacing.HORIZONTALS_ANGLEORDER[rotatedIndex];

            return CodeWithParts(nowFacing.Code);
        }

        public override AssetLocation GetHorizontallyFlippedBlockCode(EnumAxis axis)
        {
            BlockFacing facing = BlockFacing.FromCode(LastCodePart());
            if (facing.Axis == axis)
            {
                return CodeWithParts(facing.Opposite.Code);
            }

            return Code;
        }
    }
}