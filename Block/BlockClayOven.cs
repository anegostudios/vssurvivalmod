using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

#nullable disable

namespace Vintagestory.GameContent
{
    public interface IBakeableCallback
    {
        void OnBaked(ItemStack oldStack, ItemStack newStack);
    }

    public class BlockClayOven : Block, IIgnitable
    {
        /*
         * TODO: Potential visual enhancements
          
        1. render logs burning stages to ash
        2. render logs burning with red glow
        3. custom renderer to draw the item meshes instead of updating the whole chunk each time an item changes
        4. shader for gradual / intermediate levels of browning

         * Potential environment enhancements
          
        5. oven responds to room warming when getting environment / base temperature?
        6. rain / snow interactions with hot oven?

         * Handbook & Firepit
         
        7. once kiln or pit kiln are ready for clay assets, remove "bake" as a firepit verb, replace it with "toast" - so "bake" is reserved for the oven

         * New bakeable items
         
        8. code for large items (e.g. pies) which take up the full oven render space, all 4 slots
        9. could maybe also have long (2-slot) items in future?

        10. maybe remove temperature display from blockInfo and replace with general words like "very hot" "hot" "warm"
         */



        WorldInteraction[] interactions;
        AdvancedParticleProperties[] particles;
        Vec3f[] basePos;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            if (api.Side != EnumAppSide.Client) return;

            ICoreClientAPI capi = api as ICoreClientAPI;

            if (capi != null) interactions = ObjectCacheUtil.GetOrCreate(api, "ovenInteractions", () =>
            {
                List<ItemStack> bakeableStacklist = [];
                List<ItemStack> fuelStacklist = [];
                List<ItemStack> canIgniteStacks = BlockBehaviorCanIgnite.CanIgniteStacks(api, true);

                foreach (CollectibleObject obj in api.World.Collectibles)
                {
                    // we test firewood first because LazyWarlock's mod adds a wood baking recipe, which we don't want to be treated as a bakeable item here
                    if (obj.Attributes?.IsTrue("isClayOvenFuel") == true)
                    {
                        List<ItemStack> stacks = obj.GetHandBookStacks(capi);
                        if (stacks != null) fuelStacklist.AddRange(stacks);
                    }
                    else if (obj.Attributes?["bakingProperties"]?.AsObject<BakingProperties>() != null || obj.CombustibleProps?.SmeltingType == EnumSmeltType.Bake && obj.CombustibleProps.SmeltedStack != null && obj.CombustibleProps.MeltingPoint < BlockEntityOven.maxBakingTemperatureAccepted)
                    {
                        List<ItemStack> stacks = obj.GetHandBookStacks(capi);
                        if (stacks != null) bakeableStacklist.AddRange(stacks);
                    }
                }

                foreach (var stack in bakeableStacklist)
                {
                    if (stack.Collectible is not BlockPie pieBlock) continue;

                    stack.Attributes.SetInt("pieSize", 4);
                    stack.Attributes.SetString("topCrustType", "square");
                    stack.Attributes.SetInt("bakeLevel", 0);

                    ItemStack doughStack = new(api.World.GetItem("dough-spelt"), 2);
                    ItemStack fillingStack = new(api.World.GetItem("fruit-redapple"), 2);
                    pieBlock.SetContents(stack, [doughStack, fillingStack, fillingStack, fillingStack, fillingStack, doughStack]);
                    stack.Attributes.SetFloat("quantityServings", 1);
                }

                return new WorldInteraction[] {
                    new WorldInteraction()
                    {
                        ActionLangCode = "blockhelp-oven-bakeable",
                        HotKeyCode = null,
                        MouseButton = EnumMouseButton.Right,
                        Itemstacks = bakeableStacklist.ToArray(),
                        GetMatchingStacks = (wi, bs, es) => {
                            if (wi.Itemstacks.Length == 0) return null;
                            BlockEntityOven beo = api.World.BlockAccessor.GetBlockEntity(bs.Position) as BlockEntityOven;
                            return beo != null ? beo.CanAdd(wi.Itemstacks) : null;
                        }
                    },
                    new WorldInteraction()
                    {
                        ActionLangCode = "blockhelp-oven-fuel",
                        HotKeyCode = null,
                        MouseButton = EnumMouseButton.Right,
                        Itemstacks = fuelStacklist.ToArray(),
                        GetMatchingStacks = (wi, bs, es) => {
                            //if (wi.Itemstacks.Length == 0) return null;
                            BlockEntityOven beo = api.World.BlockAccessor.GetBlockEntity(bs.Position) as BlockEntityOven;
                            return beo != null ? beo.CanAddAsFuel(fuelStacklist.ToArray()) : null;
                        }
                    },
                    new WorldInteraction()
                    {
                        ActionLangCode = "blockhelp-oven-ignite",
                        MouseButton = EnumMouseButton.Right,
                        HotKeyCode = "shift",
                        Itemstacks = canIgniteStacks.ToArray(),
                        GetMatchingStacks = (wi, bs, es) => {
                            if (wi.Itemstacks.Length == 0) return null;
                            BlockEntityOven beo = api.World.BlockAccessor.GetBlockEntity(bs.Position) as BlockEntityOven;
                            return beo != null && beo.CanIgnite() ? wi.Itemstacks : null;
                        }
                    }
                };
            });

            InitializeParticles();
        }

        public override bool DoParticalSelection(IWorldAccessor world, BlockPos pos)
        {
            return true;
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection bs)
        {
            if (world.BlockAccessor.GetBlockEntity(bs.Position) is BlockEntityOven beo) return beo.OnInteract(byPlayer, bs);

            return base.OnBlockInteractStart(world, byPlayer, bs);
        }

        EnumIgniteState IIgnitable.OnTryIgniteStack(EntityAgent byEntity, BlockPos pos, ItemSlot slot, float secondsIgniting)
        {
            BlockEntityOven beo = byEntity.World.BlockAccessor.GetBlockEntity(pos) as BlockEntityOven;
            if (!beo.IsBurning) return secondsIgniting > 2 ? EnumIgniteState.IgniteNow : EnumIgniteState.Ignitable;
            return EnumIgniteState.NotIgnitablePreventDefault;
        }

        public EnumIgniteState OnTryIgniteBlock(EntityAgent byEntity, BlockPos pos, float secondsIgniting)
        {
            BlockEntityOven beo = byEntity.World.BlockAccessor.GetBlockEntity(pos) as BlockEntityOven;
            if (beo == null || !beo.CanIgnite()) return EnumIgniteState.NotIgnitablePreventDefault;

            return secondsIgniting > 4 ? EnumIgniteState.IgniteNow : EnumIgniteState.Ignitable;
        }

        public void OnTryIgniteBlockOver(EntityAgent byEntity, BlockPos pos, float secondsIgniting, ref EnumHandling handling)
        {
            handling = EnumHandling.PreventDefault;

            BlockEntityOven beo = byEntity.World.BlockAccessor.GetBlockEntity(pos) as BlockEntityOven;
            beo?.TryIgnite();
        }

        public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer)
        {
            return interactions.Append(base.GetPlacedBlockInteractionHelp(world, selection, forPlayer));
        }

        //public override bool ShouldReceiveClientParticleTicks(IWorldAccessor world, IPlayer player, BlockPos pos, out bool isWindAffected)
        //{
        //    bool val = base.ShouldReceiveClientParticleTicks(world, player, pos, out _);
        //    isWindAffected = false;

        //    return val;
        //}

        public override void OnAsyncClientParticleTick(IAsyncParticleManager manager, BlockPos pos, float windAffectednessAtPos, float secondsTicking)
        {
            BlockEntityOven beo = manager.BlockAccess.GetBlockEntity(pos) as BlockEntityOven;
            if (beo != null && beo.IsBurning) beo.RenderParticleTick(manager, pos, windAffectednessAtPos, secondsTicking, particles);

            base.OnAsyncClientParticleTick(manager, pos, windAffectednessAtPos, secondsTicking);
        }

        private void InitializeParticles()
        {
            particles = new AdvancedParticleProperties[16];
            basePos = new Vec3f[particles.Length];

            Cuboidf[] spawnBoxes = new Cuboidf[]
            {
                    new Cuboidf(x1: 0.125f, y1: 0, z1: 0.125f, x2: 0.3125f, y2: 0.5f, z2: 0.875f),
                    new Cuboidf(x1: 0.7125f, y1: 0, z1: 0.125f, x2: 0.875f, y2: 0.5f, z2: 0.875f),
                    new Cuboidf(x1: 0.125f, y1: 0, z1: 0.125f, x2: 0.875f, y2: 0.5f, z2: 0.3125f),
                    new Cuboidf(x1: 0.125f, y1: 0, z1: 0.7125f, x2: 0.875f, y2: 0.5f, z2: 0.875f)
            };

            // This is smoke particles - similar to the Firepit
            for (int j = 0; j < 4; j++)
            {
                AdvancedParticleProperties props = ParticleProperties[0].Clone();

                Cuboidf box = spawnBoxes[j];
                basePos[j] = new Vec3f(0, 0, 0);

                props.PosOffset[0].avg = box.MidX;
                props.PosOffset[0].var = box.Width / 2;

                props.PosOffset[1].avg = 0.3f;
                props.PosOffset[1].var = 0.05f;

                props.PosOffset[2].avg = box.MidZ;
                props.PosOffset[2].var = box.Length / 2;

                props.Quantity.avg = 0.5f;
                props.Quantity.var = 0.2f;
                props.LifeLength.avg = 0.8f;

                particles[j] = props;
            }

            // The rest are flame particles: the spawn pos will be precisely controlled by spawning code in BEClayOven
            // This is the dark orange at the base of a flame
            for (int j = 4; j < 8; j++)
            {
                AdvancedParticleProperties props = ParticleProperties[1].Clone();
                props.PosOffset[1].avg = 0.06f;
                props.PosOffset[1].var = 0.02f;
                props.Quantity.avg = 0.5f;
                props.Quantity.var = 0.2f;
                props.LifeLength.avg = 0.3f;
                props.VertexFlags = 128;

                particles[j] = props;
            }

            // This is the bright orange in the middle of a flame
            for (int j = 8; j < 12; j++)
            {
                AdvancedParticleProperties props = ParticleProperties[2].Clone();
                props.PosOffset[1].avg = 0.09f;
                props.PosOffset[1].var = 0.02f;
                props.Quantity.avg = 0.5f;
                props.Quantity.var = 0.2f;
                props.LifeLength.avg = 0.18f;
                props.VertexFlags = 192;

                particles[j] = props;
            }

            // This is the bright yellow at the top of a flame
            for (int j = 12; j < 16; j++)
            {
                AdvancedParticleProperties props = ParticleProperties[3].Clone();
                props.PosOffset[1].avg = 0.12f;
                props.PosOffset[1].var = 0.03f;
                props.Quantity.avg = 0.2f;
                props.Quantity.var = 0.1f;
                props.LifeLength.avg = 0.12f;
                props.VertexFlags = 255;

                particles[j] = props;
            }
        }
    }
}
