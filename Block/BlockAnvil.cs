using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

#nullable disable

namespace Vintagestory.GameContent
{
    public class BlockAnvil : Block
    {
        public int MetalTier { get; private set; } = 0;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            var metalsByCode = api.ModLoader.GetModSystem<SurvivalCoreSystem>().metalsByCode;

            if (metalsByCode.TryGetValue(Variant["metal"] ?? LastCodePart(), out var metalVariant)) MetalTier = metalVariant.Tier;
        }


        public override void OnDecalTesselation(IWorldAccessor world, MeshData decalMesh, BlockPos pos)
        {
            base.OnDecalTesselation(world, decalMesh, pos);
            BlockEntityAnvil bect = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityAnvil;
            if (bect != null)
            {
                decalMesh.Rotate(0, bect.MeshAngle, 0);
            }
        }

        public override Cuboidf[] GetSelectionBoxes(IBlockAccessor blockAccessor, BlockPos pos)
        {
            BlockEntityAnvil bea = blockAccessor.GetBlockEntity(pos) as BlockEntityAnvil;
            if (bea != null)
            {
                Cuboidf[] selectionBoxes = bea.GetSelectionBoxes(blockAccessor, pos);
                float angledeg = Math.Abs(bea.MeshAngle * GameMath.RAD2DEG);
                selectionBoxes[0] = angledeg == 0  || angledeg == 180 ? SelectionBoxes[0] : SelectionBoxes[1];
                return selectionBoxes;
            }

            return base.GetSelectionBoxes(blockAccessor, pos);
        }

        public override Cuboidf[] GetCollisionBoxes(IBlockAccessor blockAccessor, BlockPos pos)
        {
            return GetSelectionBoxes(blockAccessor, pos);
        }

        public override Cuboidf[] GetParticleCollisionBoxes(IBlockAccessor blockAccessor, BlockPos pos)
        {
            return GetSelectionBoxes(blockAccessor, pos);
        }

        public override bool DoPartialSelection(IWorldAccessor world, BlockPos pos)
        {
            return true;
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            BlockEntityAnvil bea = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityAnvil;
            if (bea != null)
            {
                if (bea.OnPlayerInteract(world, byPlayer, blockSel))
                {
                    return true;
                }

                return false;
            }

            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }


        public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer)
        {
            return ObjectCacheUtil.GetOrCreate(api, "anvilBlockInteractions" + MetalTier, () =>
            {
                List<ItemStack> workableStacklist = new List<ItemStack>();
                List<ItemStack> hammerStacklist = new List<ItemStack>();

                foreach (Item item in api.World.Items)
                {
                    if (item.Code == null) continue;

                    var workable = item.GetCollectibleInterface<IAnvilWorkable>();
                    if (workable != null)
                    {
                        var stacks = item.GetHandBookStacks(api as ICoreClientAPI);
                        workableStacklist.AddRange(stacks?.Where(stack => workable.GetRequiredAnvilTier(stack) <= MetalTier) ?? []);
                    }

                    if (item is ItemHammer)
                    {
                        hammerStacklist.Add(new ItemStack(item));
                    }
                }

                return new WorldInteraction[] {
                    new WorldInteraction()
                    {
                        ActionLangCode = "blockhelp-anvil-takeworkable",
                        MouseButton = EnumMouseButton.Right,
                        ShouldApply = (wi, bs, es) => GetBlockEntity<BlockEntityAnvil>(bs.Position)?.WorkItemStack != null
                    },
                    new WorldInteraction()
                    {
                        ActionLangCode = "blockhelp-anvil-placeworkable",
                        HotKeyCode = "shift",
                        MouseButton = EnumMouseButton.Right,
                        Itemstacks = workableStacklist.ToArray(),
                        GetMatchingStacks = (wi, bs, es) => GetBlockEntity<BlockEntityAnvil>(bs.Position)?.WorkItemStack == null ? wi.Itemstacks : null
                    },
                    new WorldInteraction()
                    {
                        ActionLangCode = "blockhelp-anvil-smith",
                        MouseButton = EnumMouseButton.Left,
                        Itemstacks = hammerStacklist.ToArray(),
                        GetMatchingStacks = (wi, bs, es) => GetBlockEntity<BlockEntityAnvil>(bs.Position)?.WorkItemStack == null ? null : wi.Itemstacks
                    },
                    new WorldInteraction()
                    {
                        ActionLangCode = "blockhelp-anvil-rotateworkitem",
                        MouseButton = EnumMouseButton.Right,
                        Itemstacks = hammerStacklist.ToArray(),
                        GetMatchingStacks = (wi, bs, es) => GetBlockEntity<BlockEntityAnvil>(bs.Position)?.WorkItemStack == null ? null : wi.Itemstacks
                    },
                    new WorldInteraction()
                    {
                        ActionLangCode = "blockhelp-selecttoolmode",
                        HotKeyCode = "toolmodeselect",
                        MouseButton = EnumMouseButton.None,
                        Itemstacks = hammerStacklist.ToArray(),
                        GetMatchingStacks = (wi, bs, es) => GetBlockEntity<BlockEntityAnvil>(bs.Position)?.WorkItemStack == null ? null : wi.Itemstacks
                    },
                    new WorldInteraction()
                    {
                        ActionLangCode = "blockhelp-anvil-addvoxels",
                        HotKeyCode = "shift",
                        MouseButton = EnumMouseButton.Right,
                        Itemstacks = workableStacklist.ToArray(),
                        GetMatchingStacks = (wi, bs, es) => {
                            BlockEntityAnvil bea = api.World.BlockAccessor.GetBlockEntity(bs.Position) as BlockEntityAnvil;
                            return bea?.WorkItemStack == null ? null : [bea.WorkItemStack.Collectible.GetCollectibleInterface<IAnvilWorkable>().GetBaseMaterial(bea.WorkItemStack)];
                        }
                    }
                };
            }).Append(base.GetPlacedBlockInteractionHelp(world, selection, forPlayer));
        }

        public override bool DoPlaceBlock(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ItemStack byItemStack)
        {
            bool val = base.DoPlaceBlock(world, byPlayer, blockSel, byItemStack);

            if (val)
            {
                BlockEntityAnvil bect = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityAnvil;
                if (bect != null)
                {
                    BlockPos targetPos = blockSel.DidOffset ? blockSel.Position.AddCopy(blockSel.Face.Opposite) : blockSel.Position;
                    double dx = byPlayer.Entity.Pos.X - (targetPos.X + blockSel.HitPosition.X);
                    double dz = byPlayer.Entity.Pos.Z - (targetPos.Z + blockSel.HitPosition.Z);
                    float angleHor = (float)Math.Atan2(dx, dz);

                    float deg22dot5rad = GameMath.PIHALF / 4;
                    float roundRad = ((int)Math.Round(angleHor / deg22dot5rad)) * deg22dot5rad;
                    bect.MeshAngle = roundRad;
                }
            }

            return val;
        }
    }
}
