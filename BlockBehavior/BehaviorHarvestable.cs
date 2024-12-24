using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class BlockBehaviorHarvestable : BlockBehavior
    {
        float harvestTime;
        bool exchangeBlock;
        public BlockDropItemStack[] harvestedStacks;
        public BlockDropItemStack harvestedStack { get { return harvestedStacks[0]; } set { harvestedStacks[0] = value; } }

        public AssetLocation harvestingSound;

        AssetLocation harvestedBlockCode;
        Block harvestedBlock;
        string interactionHelpCode;

        public BlockBehaviorHarvestable(Block block) : base(block)
        {
        }

        public override void Initialize(JsonObject properties)
        {
            base.Initialize(properties);

            interactionHelpCode = properties["harvestTime"].AsString("blockhelp-harvetable-harvest");
            harvestTime = properties["harvestTime"].AsFloat(0);
            harvestedStacks = properties["harvestedStacks"].AsObject<BlockDropItemStack[]>(null);
            BlockDropItemStack tempStack = properties["harvestedStack"].AsObject<BlockDropItemStack>(null);
            if (harvestedStacks == null && tempStack != null)
            {
                harvestedStacks = new BlockDropItemStack[1];
                harvestedStacks[0] = tempStack;
            }
            exchangeBlock = properties["exchangeBlock"].AsBool(false);

            string code = properties["harvestingSound"].AsString("game:sounds/block/leafy-picking");
            if (code != null) {
                harvestingSound = AssetLocation.Create(code, block.Code.Domain);
            }

            code = properties["harvestedBlockCode"].AsString();
            if (code != null)
            {
                harvestedBlockCode = AssetLocation.Create(code, block.Code.Domain);
            }
        }

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            harvestedStacks.Foreach(harvestedStack => harvestedStack?.Resolve(api.World, "harvestedStack of block ", block.Code));

            harvestedBlock = api.World.GetBlock(harvestedBlockCode);
            if (harvestedBlock == null)
            {
                api.World.Logger.Warning("Unable to resolve harvested block code '{0}' for block {1}. Will ignore.", harvestedBlockCode, block.Code);
            }
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ref EnumHandling handling)
        {
            if (!world.Claims.TryAccess(byPlayer, blockSel.Position, EnumBlockAccessFlags.Use))
            {
                return false;
            }

            handling = EnumHandling.PreventDefault;

            if (harvestedStacks != null)
            {
                world.PlaySoundAt(harvestingSound, blockSel.Position, 0, byPlayer);
                return true;
            }

            return false;
        }

        public override bool OnBlockInteractStep(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ref EnumHandling handled)
        {
            if (blockSel == null) return false;

            handled = EnumHandling.PreventDefault;

            (byPlayer as IClientPlayer)?.TriggerFpAnimation(EnumHandInteract.HeldItemAttack);

            if (world.Rand.NextDouble() < 0.05)
            {
                world.PlaySoundAt(harvestingSound, blockSel.Position, 0, byPlayer);
            }

            if (world.Side == EnumAppSide.Client && world.Rand.NextDouble() < 0.25)
            {
                world.SpawnCubeParticles(blockSel.Position.ToVec3d().Add(blockSel.HitPosition), harvestedStacks[0].ResolvedItemstack, 0.25f, 1, 0.5f, byPlayer, new Vec3f(0, 1, 0));
            }

            return world.Side == EnumAppSide.Client || secondsUsed < harvestTime;
        }

        public override void OnBlockInteractStop(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ref EnumHandling handled)
        {
            handled = EnumHandling.PreventDefault;


            if (secondsUsed > harvestTime - 0.05f && harvestedStacks != null && world.Side == EnumAppSide.Server)
            {
                float dropRate = 1;

                if (block.Attributes?.IsTrue("forageStatAffected") == true)
                {
                    dropRate *= byPlayer.Entity.Stats.GetBlended("forageDropRate");
                }

                harvestedStacks.Foreach(harvestedStack => 
                {
                    ItemStack stack = harvestedStack.GetNextItemStack(dropRate);
                    if (stack == null) return;
                    var origStack = stack.Clone();
                    var quantity = stack.StackSize;
                    if (!byPlayer.InventoryManager.TryGiveItemstack(stack))
                    {
                        world.SpawnItemEntity(stack, blockSel.Position);
                    }
                    world.Logger.Audit("{0} Took {1}x{2} from {3} at {4}.",
                        byPlayer.PlayerName,
                        quantity,
                        stack.Collectible.Code,
                        block.Code,
                        blockSel.Position
                    );

                    TreeAttribute tree = new TreeAttribute();
                    tree["itemstack"] = new ItemstackAttribute(origStack.Clone());
                    tree["byentityid"] = new LongAttribute(byPlayer.Entity.EntityId);
                    world.Api.Event.PushEvent("onitemcollected", tree);
                });

                if (harvestedBlock != null)
                {
                    if (!exchangeBlock) world.BlockAccessor.SetBlock(harvestedBlock.BlockId, blockSel.Position);
                    else world.BlockAccessor.ExchangeBlock(harvestedBlock.BlockId, blockSel.Position);
                }

                world.PlaySoundAt(harvestingSound, blockSel.Position, 0, byPlayer);
            }
        }


        public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer, ref EnumHandling handled)
        {
            if (harvestedStacks != null)
            {
                bool notProtected = true;

                if (world.Claims != null && world is IClientWorldAccessor clientWorld && clientWorld.Player?.WorldData.CurrentGameMode == EnumGameMode.Survival)
                {
                    EnumWorldAccessResponse resp = world.Claims.TestAccess(clientWorld.Player, selection.Position, EnumBlockAccessFlags.Use);
                    if (resp != EnumWorldAccessResponse.Granted) notProtected = false;
                }

                if (notProtected) return new WorldInteraction[]
                {
                    new WorldInteraction()
                    {
                        ActionLangCode = interactionHelpCode,
                        MouseButton = EnumMouseButton.Right
                    }
                };
            }

            return base.GetPlacedBlockInteractionHelp(world, selection, forPlayer, ref handled);
        }
    }
}