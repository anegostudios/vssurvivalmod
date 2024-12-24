using Cairo;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class FruitTreeShape
    {
        public CompositeShape CShape;
        public Shape Shape;
    }

    public class FruitTreeWorldGenConds
    {
        public string Type;
        public float MinTemp;
        public float MaxTemp;
        public float MinRain;
        public float MaxRain;
        public float Chance = 1;
    }

    public class BlockFruitTreeBranch : BlockFruitTreePart, ITexPositionSource, ICustomTreeFellingBehavior, ICustomHandbookPageContent
    {
        Block branchBlock;
        BlockFruitTreeFoliage foliageBlock;
        ICoreClientAPI capi;

        public FruitTreeWorldGenConds[] WorldGenConds;

        public Dictionary<string, FruitTreeShape> Shapes = new Dictionary<string, FruitTreeShape>();

        public Dictionary<string, FruitTreeTypeProperties> TypeProps;

        public Size2i AtlasSize => capi.BlockTextureAtlas.Size;

        string curTreeType;
        Shape curTessShape;
        public TextureAtlasPosition this[string textureCode]
        {
            get
            {
                // Prio 1: Config
                foliageBlock.foliageProps.TryGetValue(curTreeType, out var props);
                if (props != null)
                {
                    TextureAtlasPosition texPos = props.GetOrLoadTexture(capi, textureCode);
                    if (texPos != null) return texPos;
                }

                // Prio 2: Get from currently tesselating shape
                AssetLocation texturePath=null;
                if (curTessShape?.Textures.TryGetValue(textureCode, out texturePath) == true)
                {
                    return capi.BlockTextureAtlas[texturePath];
                }

                return null;
            }
        }

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            capi = api as ICoreClientAPI;

            branchBlock = api.World.GetBlock(CodeWithVariant("type", "branch"));
            foliageBlock = api.World.GetBlock(AssetLocation.Create(Attributes["foliageBlock"].AsString(), Code.Domain)) as BlockFruitTreeFoliage;

            TypeProps = Attributes["fruittreeProperties"].AsObject<Dictionary<string, FruitTreeTypeProperties>>();
            var shapeFiles = Attributes["shapes"].AsObject<Dictionary<string, CompositeShape>>();

            WorldGenConds = Attributes["worldgen"].AsObject<FruitTreeWorldGenConds[]>();

            foreach (var val in shapeFiles)
            {
                Shape shape = API.Common.Shape.TryGet(api, val.Value.Base.WithPathAppendixOnce(".json").WithPathPrefixOnce("shapes/"));
                Shapes[val.Key] = new FruitTreeShape() { Shape = shape, CShape = val.Value };
            }

            var rnd = new LCGRandom(api.World.Seed);
            foreach (var prop in TypeProps)
            {
                foreach (var bdstack in prop.Value.FruitStacks)
                {
                    bdstack.Resolve(api.World, "fruit tree FruitStacks ", Code);
                }

                (api as ICoreServerAPI)?.RegisterTreeGenerator(new AssetLocation("fruittree-" + prop.Key), (blockAccessor, pos, treegenParams) => GrowTree(blockAccessor, pos, prop.Key, treegenParams.size, rnd));
            }
        }




        public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel, ref string failureCode)
        {
            if (!CanPlaceBlock(world, byPlayer, blockSel, ref failureCode)) return false;

            bool planted = world.BlockAccessor.GetBlock(blockSel.Position.DownCopy()).Fertility > 0;
            var aimedBe = world.BlockAccessor.GetBlockEntity(blockSel.DidOffset ? blockSel.Position.AddCopy(blockSel.Face.Opposite) : blockSel.Position) as BlockEntityFruitTreeBranch;
            bool grafted = blockSel.Face != BlockFacing.DOWN && aimedBe != null && ((aimedBe.SideGrowth & (1 << blockSel.Face.Index)) > 0);

            if (!planted && !grafted)
            {
                failureCode = "fruittreecutting";
                return false;
            }

            if (grafted && TypeProps.TryGetValue(aimedBe.TreeType, out var rootProps) && TypeProps.TryGetValue(itemstack.Attributes.GetString("type"), out var selfProprs))
            {
                if (rootProps.CycleType != selfProprs.CycleType)
                {
                    failureCode = "fruittreecutting-ctypemix";
                    return false;
                }
            }

            return DoPlaceBlock(world, byPlayer, blockSel, itemstack);
        }



        public override void OnBeforeRender(ICoreClientAPI capi, ItemStack itemstack, EnumItemRenderTarget target, ref ItemRenderInfo renderinfo)
        {
            if (itemstack.Collectible.Variant["type"] == "cutting")
            {
                curTreeType = itemstack.Attributes.GetString("type");
                if (curTreeType == null) return;

                var dict = ObjectCacheUtil.GetOrCreate(capi, "cuttingMeshRefs", () => new Dictionary<string, MultiTextureMeshRef>());

                if (!dict.TryGetValue(curTreeType, out var meshref))
                {
                    curTessShape = capi.TesselatorManager.GetCachedShape(this.Shape.Base);
                    capi.Tesselator.TesselateShape("fruittreecutting", curTessShape, out var meshdata, this);
                    dict[curTreeType] = renderinfo.ModelRef = capi.Render.UploadMultiTextureMesh(meshdata);
                } else
                {
                    renderinfo.ModelRef = meshref;
                }
            }

            base.OnBeforeRender(capi, itemstack, target, ref renderinfo);
        }

        public override void OnUnloaded(ICoreAPI api)
        {
            base.OnUnloaded(api);

            if (capi != null)
            {
                var dict = ObjectCacheUtil.GetOrCreate(capi, "cuttingMeshRefs", () => new Dictionary<string, MultiTextureMeshRef>());
                if (dict != null)
                {
                    foreach (var val in dict)
                    {
                        val.Value.Dispose();
                    }
                }
            }
        }

        public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos)
        {
            var stack = base.OnPickBlock(world, pos);
            var be = api.World.BlockAccessor.GetBlockEntity(pos) as BlockEntityFruitTreeBranch;

            stack.Attributes.SetString("type", be?.TreeType ?? "pinkapple");

            return stack;
        }

        public override BlockDropItemStack[] GetDropsForHandbook(ItemStack handbookStack, IPlayer forPlayer)
        {
            var drops = base.GetDropsForHandbook(handbookStack, forPlayer);

            foreach (var drop in drops)
            {
                if (drop.ResolvedItemstack.Collectible is BlockFruitTreeBranch)
                {
                    drop.ResolvedItemstack.Attributes.SetString("type", handbookStack.Attributes.GetString("type") ?? "pinkapple");
                }
            }

            return drops;
        }

        public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1)
        {
            ItemStack[] stacks = base.GetDrops(world, pos, byPlayer, dropQuantityMultiplier);

            var be = api.World.BlockAccessor.GetBlockEntity(pos) as BlockEntityFruitTreeBranch;

            bool alive = be != null && be.FoliageState != EnumFoliageState.Dead;

            for (int i = 0; i < stacks.Length; i++)
            {
                var stack = stacks[i];

                if (stack.Collectible is BlockFruitTreeBranch)
                {
                    stack.Attributes.SetString("type", be?.TreeType);
                }
                if (stack.Collectible.Variant["type"] == "cutting" && !alive)
                {
                    stacks[i] = new ItemStack(world.GetItem(new AssetLocation("firewood")), 2);
                }
            }

            return stacks;
        }


        public override bool ShouldMergeFace(int facingIndex, Block neighbourBlock, int intraChunkIndex3d)
        {
            return this == branchBlock && (facingIndex == 1 || facingIndex == 2 || facingIndex == 4) & (neighbourBlock == this || neighbourBlock == branchBlock);
        }



        public EnumTreeFellingBehavior GetTreeFellingBehavior(BlockPos pos, Vec3i fromDir, int spreadIndex)
        {
            var bebranch = api.World.BlockAccessor.GetBlockEntity(pos) as BlockEntityFruitTreeBranch;
            if (bebranch == null || bebranch.PartType != EnumTreePartType.Branch) return EnumTreeFellingBehavior.Chop;
            else
            {
                if (bebranch.GrowthDir.IsVertical) return EnumTreeFellingBehavior.Chop;
                else return EnumTreeFellingBehavior.NoChop;
            }
        }

        public override Cuboidf[] GetCollisionBoxes(IBlockAccessor blockAccessor, BlockPos pos)
        {
            var bebranch = blockAccessor.GetBlockEntity(pos) as BlockEntityFruitTreeBranch;
            return bebranch?.GetColSelBox() ?? base.GetCollisionBoxes(blockAccessor, pos);
        }

        public override Cuboidf[] GetSelectionBoxes(IBlockAccessor blockAccessor, BlockPos pos)
        {
            var bebranch = blockAccessor.GetBlockEntity(pos) as BlockEntityFruitTreeBranch;
            return bebranch?.GetColSelBox() ?? base.GetCollisionBoxes(blockAccessor, pos);
        }

        public override string GetPlacedBlockName(IWorldAccessor world, BlockPos pos)
        {
            var bebranch = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityFruitTreeBranch;

            if (bebranch != null) {
                var rootbh = bebranch.GetBehavior<FruitTreeRootBH>();
                if (rootbh != null && rootbh.IsYoung && bebranch.PartType != EnumTreePartType.Cutting)
                {
                    return Lang.Get("fruittree-young-" + bebranch.TreeType);
                }

                string code = "fruittree-branch-";

                if (bebranch.PartType == EnumTreePartType.Cutting) code = "fruittree-cutting-";
                else if (bebranch.PartType == EnumTreePartType.Stem || rootbh != null) code = "fruittree-stem-";


                return Lang.Get(code + bebranch.TreeType);

            }
            return base.GetPlacedBlockName(world, pos);
        }

        public override string GetHeldItemName(ItemStack itemStack)
        {
            string type = itemStack.Attributes?.GetString("type", "unknown") ?? "unknown";
            return Lang.Get("fruittree-cutting-" + type);
        }

        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);
        }

        public override bool TryPlaceBlockForWorldGen(IBlockAccessor blockAccessor, BlockPos pos, BlockFacing onBlockFace, IRandom worldgenRandom, BlockPatchAttributes attributes = null)
        {
            var dBlock = blockAccessor.GetBlockBelow(pos);
            if (dBlock.Fertility <= 20) return false;

            var climate = blockAccessor.GetClimateAt(pos, EnumGetClimateMode.WorldGenValues);
            int rnd = worldgenRandom.NextInt(WorldGenConds.Length);

            int len = WorldGenConds.Length;
            for (int i = 0; i < len; i++)
            {
                var conds = WorldGenConds[(i + rnd) % len];
                if (conds.MinTemp <= climate.Temperature && conds.MaxTemp >= climate.Temperature && conds.MinRain <= climate.Rainfall && conds.MaxRain >= climate.Rainfall && worldgenRandom.NextFloat() <= conds.Chance)
                {
                    blockAccessor.SetBlock(BlockId, pos);
                    blockAccessor.SpawnBlockEntity(EntityClass, pos);
                    var be = blockAccessor.GetBlockEntity(pos) as BlockEntityFruitTreeBranch;

                    be.TreeType = conds.Type;
                    be.FastForwardGrowth = worldgenRandom.NextFloat();

                    return true;
                }
            }

            return false;
        }

        public void GrowTree(IBlockAccessor blockAccessor, BlockPos pos, string type, float growthRel, IRandom random)
        {
            pos = pos.UpCopy();
            blockAccessor.SetBlock(BlockId, pos);

            BlockEntityFruitTreeBranch be = api.ClassRegistry.CreateBlockEntity(EntityClass) as BlockEntityFruitTreeBranch;
            be.Pos = pos.Copy();
            be.TreeType = type;
            be.FastForwardGrowth = growthRel;

            blockAccessor.SpawnBlockEntity(be);
        }

        public override void OnBlockPlaced(IWorldAccessor world, BlockPos blockPos, ItemStack byItemStack = null)
        {
            BlockEntityFruitTreeBranch be = world.BlockAccessor.GetBlockEntity(blockPos) as BlockEntityFruitTreeBranch;
            if (be != null && be.FastForwardGrowth != null)
            {
                be.CreateBehaviors(this, api.World);
                be.Initialize(api);
                be.MarkDirty(true);

                return;
            }

            base.OnBlockPlaced(world, blockPos, byItemStack);
        }


        public void OnHandbookPageComposed(List<RichTextComponentBase> components, ItemSlot inSlot, ICoreClientAPI capi, ItemStack[] allStacks, ActionConsumable<string> openDetailPageFor)
        {
            string type = inSlot.Itemstack.Attributes?.GetString("type", "unknown") ?? "unknown";
            if (TypeProps.TryGetValue(type, out var props))
            {
                StringBuilder sb = new StringBuilder();
                if (props.CycleType == EnumTreeCycleType.Deciduous)
                {
                    sb.AppendLine(Lang.Get("Must experience {0} game hours below {1}°C in the cold season to bear fruit in the following year.", props.VernalizationHours.avg, props.VernalizationTemp.avg));
                    sb.AppendLine(Lang.Get("Will die if exposed to {0}°C or colder", props.DieBelowTemp.avg));
                } else
                {
                    sb.AppendLine(Lang.Get("Evergreen tree. Will die if exposed to {0} °C or colder", props.DieBelowTemp.avg));
                }

                sb.AppendLine();
                sb.AppendLine(Lang.Get("handbook-fruittree-note-averages"));

                float marginTop = 7;

                components.Add(new ClearFloatTextComponent(capi, marginTop));
                components.Add(new RichTextComponent(capi, Lang.Get("Growing properties") + "\n", CairoFont.WhiteSmallText().WithWeight(FontWeight.Bold)));
                components.Add(new RichTextComponent(capi, sb.ToString(), CairoFont.WhiteSmallText()));

                components.Add(new ClearFloatTextComponent(capi, marginTop));
                components.Add(new RichTextComponent(capi, Lang.Get("fruittree-produces") + "\n", CairoFont.WhiteSmallText().WithWeight(FontWeight.Bold)));

                foreach (var stack in props.FruitStacks)
                {
                    components.Add(new ItemstackTextComponent(capi, stack.ResolvedItemstack, 40, 0, EnumFloat.Inline));
                }


            }
        }
    }
}
