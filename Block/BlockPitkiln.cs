using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class PitKilnModelConfig
    {
        public CompositeShape Shape;
        public float[] MinHitboxY2;
        public string[] BuildStages;
        public string[] BuildMatCodes;
    }

    public class BuildStage
    {
        public string ElementName;
        public ItemStack Material;
        public float MinHitboxY2;
    }

    public class BlockPitkiln : BlockGroundStorage
    {
        public Dictionary<string, BuildStage[]> BuildStagesByBlock = new Dictionary<string, BuildStage[]>();
        public Dictionary<string, Shape> ShapesByBlock = new Dictionary<string, Shape>();

        WorldInteraction[] ingiteInteraction;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);


            List<ItemStack> canIgniteStacks = new List<ItemStack>();
            foreach (CollectibleObject obj in api.World.Collectibles)
            {
                string firstCodePart = obj.FirstCodePart();

                if (obj is Block && (obj as Block).HasBehavior<BlockBehaviorCanIgnite>() || obj is ItemFirestarter)
                {
                    List<ItemStack> stacks = obj.GetHandBookStacks(api as ICoreClientAPI);
                    if (stacks != null) canIgniteStacks.AddRange(stacks);
                }
            }

            ingiteInteraction = new WorldInteraction[] { new WorldInteraction()
            {
                ActionLangCode = "blockhelp-firepit-ignite",
                MouseButton = EnumMouseButton.Right,
                HotKeyCode = "sneak",
                Itemstacks = canIgniteStacks.ToArray(),
                GetMatchingStacks = (wi, bs, es) =>
                {
                    BlockEntityPitKiln beg = api.World.BlockAccessor.GetBlockEntity(bs.Position) as BlockEntityPitKiln;
                    if (beg?.Lit != true)
                    {
                        return wi.Itemstacks;
                    }
                    return null;
                }
            }
            };



            var modelConfigs = Attributes["modelConfigs"].AsObject<Dictionary<string, PitKilnModelConfig>>();
            var buildMats = Attributes["buildMats"].AsObject<Dictionary<string, JsonItemStack>>();

            Dictionary<string, ItemStack> resolvedMats = new Dictionary<string, ItemStack>();

            foreach (var val in buildMats)
            {
                if (!val.Value.Resolve(api.World, "pit kiln build material", true)) continue;

                resolvedMats[val.Key] = val.Value.ResolvedItemstack;
            }

            foreach (var val in modelConfigs)
            {
                if (val.Value?.BuildStages == null || val.Value.BuildMatCodes == null || val.Value.Shape?.Base == null)
                {
                    api.World.Logger.Error("Pit kiln model configs: Build stage array, build mat array or composite shape is null. Will ignore this config.");
                    continue;
                }

                if (val.Value.BuildStages.Length != val.Value.BuildMatCodes.Length)
                {
                    api.World.Logger.Error("Pit kiln model configs: Build stage array and build mat array not the same length, please fix. Will ignore this config.");
                    continue;
                }

                var loc = val.Value.Shape.Base.Clone().WithPathAppendixOnce(".json").WithPathPrefixOnce("shapes/");
                Shape shape = api.Assets.TryGet(loc)?.ToObject<Shape>();
                if (shape == null)
                {
                    api.World.Logger.Error("Pit kiln model configs: Shape file {0} not found. Will ignore this config.", val.Value.Shape.Base);
                    continue;
                }

                string[] stages = val.Value.BuildStages;
                string[] matcodes = val.Value.BuildMatCodes;

                BuildStage[] resostages = new BuildStage[stages.Length];

                for (int i = 0; i < stages.Length; i++)
                {
                    ItemStack stack;
                    if (!resolvedMats.TryGetValue(matcodes[i], out stack))
                    {
                        api.World.Logger.Error("Pit kiln model configs: No such mat code " + matcodes[i] + ". Please fix. Will ignore all configs.");
                        return;
                    }

                    float miny2 = 0;
                    if (val.Value.MinHitboxY2 != null)
                    {
                        miny2 = val.Value.MinHitboxY2[GameMath.Clamp(i, 0, val.Value.MinHitboxY2.Length - 1)];
                    }

                    resostages[i] = new BuildStage() { ElementName = stages[i], Material = stack, MinHitboxY2 = miny2 };
                    
                }

                BuildStagesByBlock[val.Key] = resostages;
                ShapesByBlock[val.Key] = shape;
            }
        }



        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            BlockEntity be = world.BlockAccessor.GetBlockEntity(blockSel.Position);
            if (be is BlockEntityGroundStorage beg) 
            { 
                beg.OnPlayerInteract(byPlayer, blockSel);
                return true;
            }

            return true;
        }


        public override EnumBlockMaterial GetBlockMaterial(IBlockAccessor blockAccessor, BlockPos pos, ItemStack stack = null)
        {
            return base.GetBlockMaterial(blockAccessor, pos, stack);
        }

        public bool TryCreateKiln(IWorldAccessor world, IPlayer byPlayer, BlockPos pos)
        {
            ItemSlot hotbarSlot = byPlayer.InventoryManager.ActiveHotbarSlot;
            if (hotbarSlot.Empty) return false;
            
            BlockEntity be = world.BlockAccessor.GetBlockEntity(pos);
            if (be is BlockEntityGroundStorage beg)
            {
                ICoreClientAPI capi = api as ICoreClientAPI;
                bool ok = true;
                foreach (var face in BlockFacing.HORIZONTALS.Append(BlockFacing.DOWN))
                {
                    BlockPos npos = pos.AddCopy(face);
                    Block block = world.BlockAccessor.GetBlock(npos);
                    if (!block.CanAttachBlockAt(world.BlockAccessor, this, npos, face.Opposite))
                    {
                        capi?.TriggerIngameError(this, "notsolid", Lang.Get("Pitkilns need to be surrounded by solid, non-flammabe blocks."));
                        ok = false;
                        break;
                    }
                    if (block.CombustibleProps != null)
                    {
                        capi?.TriggerIngameError(this, "notsolid", Lang.Get("Pitkilns need to be surrounded by solid, non-flammabe blocks."));
                        ok = false;
                        break;
                    }
                }
                if (!ok) return false;

                Block upblock = world.BlockAccessor.GetBlock(pos.UpCopy());
                if (upblock.Replaceable < 6000)
                {
                    ok = false;
                    capi?.TriggerIngameError(this, "notairspace", Lang.Get("Pitkilns need air space one block above."));
                }
                if (!ok) return false;


                BuildStage[] buildStages = null;
                bool found = false;
                foreach (var val in BuildStagesByBlock)
                {
                    if (!beg.Inventory[0].Empty && WildcardUtil.Match(new AssetLocation(val.Key), beg.Inventory[0].Itemstack.Collectible.Code))
                    {
                        buildStages = val.Value;
                        found = true;
                        break;
                    }
                }
                if (!found)
                {
                    BuildStagesByBlock.TryGetValue("*", out buildStages);
                }

                if (buildStages == null) return false;
                if (!hotbarSlot.Itemstack.Equals(world, buildStages[0].Material, GlobalConstants.IgnoredStackAttributes) || hotbarSlot.StackSize < buildStages[0].Material.StackSize) return false;



                var prevInv = beg.Inventory;

                world.BlockAccessor.SetBlock(Id, pos);

                var begs = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityPitKiln;
                for (int i = 0; i < prevInv.Count; i++)
                {
                    begs.Inventory[i] = prevInv[i];
                }

                begs.OnCreated(byPlayer);
                begs.updateMeshes();
                begs.MarkDirty(true);
                return true;
            }


            return false;
        }


        public override EnumIgniteState OnTryIgniteBlock(EntityAgent byEntity, BlockPos pos, float secondsIgniting)
        {
            BlockEntityPitKiln beb = byEntity.World.BlockAccessor.GetBlockEntity(pos) as BlockEntityPitKiln;
            if (!beb.CanIgnite()) return EnumIgniteState.NotIgnitablePreventDefault;

            

            return secondsIgniting > 4 ? EnumIgniteState.IgniteNow : EnumIgniteState.Ignitable;
        }

        public override void OnTryIgniteBlockOver(EntityAgent byEntity, BlockPos pos, float secondsIgniting, ref EnumHandling handling)
        {
            handling = EnumHandling.PreventDefault;

            BlockEntityPitKiln beb = byEntity.World.BlockAccessor.GetBlockEntity(pos) as BlockEntityPitKiln;
            beb?.TryIgnite((byEntity as EntityPlayer).Player);
        }



        public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer)
        {
            var begs = world.BlockAccessor.GetBlockEntity(selection.Position) as BlockEntityPitKiln;
            if (begs != null)
            {
                if (!begs.IsComplete)
                {
                    ItemStack[] stacks = new ItemStack[] { begs.NextBuildStage.Material };

                    return new WorldInteraction[] { new WorldInteraction() {
                        ActionLangCode = "blockhelp-pitkiln-build",
                        MouseButton = EnumMouseButton.Right,
                        HotKeyCode = "sneak",
                        Itemstacks = stacks.ToArray(),
                        GetMatchingStacks = (wi, bs, es) => {
                            return stacks;
                        }
                    } };
                } else
                {
                    return ingiteInteraction;
                }
            }

            return base.GetPlacedBlockInteractionHelp(world, selection, forPlayer);
        }

    }
}
