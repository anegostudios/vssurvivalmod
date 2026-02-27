using Vintagestory.API;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    /// <summary>
    /// Allows a block to be harvested with the right mouse button for set items.
    /// Uses the code "harvestable".
    /// </summary>
    /// <example><code lang="json">
    ///"behaviorsByType": {
	///	"*-ripe": [
	///		{
	///			"name": "Harvestable",
	///			"properties": {
	///				"harvestTime": 0.6,
	///				"harvestedStack": {
	///					"type": "item",
	///					"code": "fruit-{type}",
	///					"quantity": { "avg": 4.4 }
	///				},
	///				"harvestedBlockCode": "bigberrybush-{type}-empty",
	///				"exchangeBlock": true
	///			}
	///		}
	///	]
	///}
    ///...
    ///"attributes": {
	///	"forageStatAffected": true
	///}
    /// </code></example>
    [DocumentAsJson]
    [AddDocumentationProperty("ForageStatAffected", "Should the harvested stack amount be multiplied by the player's 'forageDropRate' stat?", "System.Boolean", "Optional", "False", true)]
    public class BlockBehaviorHarvestable : BlockBehavior
    {
        /// <summary>
        /// The amount of time, in seconds, it takes to harvest this block.
        /// </summary>
        [DocumentAsJson("Recommended", "0")]
        float harvestTime;

        /// <summary>
        /// Should this block be exchanged (true) or replaced (false)? If true, then any block entity at the same position will not be deleted.
        /// </summary>
        [DocumentAsJson("Optional", "False")]
        bool exchangeBlock;

        /// <summary>
        /// An array of drops for when the block is harvested. If only using a single drop you can use <see cref="harvestedStack"/>, otherwise this property is required.
        /// </summary>
        [DocumentAsJson("Required")]
        public BlockDropItemStack[]? harvestedStacks;

        /// <summary>
        /// A drop for when the block is harvested. If using more than a single drop, use <see cref="harvestedStacks"/>, otherwise this property is required.
        /// </summary>
        [DocumentAsJson("Obsolete")]
        public BlockDropItemStack? harvestedStack { get { return harvestedStacks?[0]; } set { if (harvestedStacks != null && value != null) harvestedStacks[0] = value; } }

        /// <summary>
        /// The sound to play whilst the object is being harvested.
        /// </summary>
        [DocumentAsJson("Optional", "sounds/block/leafy-picking")]
        public AssetLocation? harvestingSound;

        /// <summary>
        /// The block to replace this one after it is harvested.
        /// </summary>
        [DocumentAsJson("Optional", "None")]
        AssetLocation? harvestedBlockCode;

        /// <summary>
        /// The block required to harvest the block.
        /// </summary>
        [DocumentAsJson("Optional", "None")]
        public EnumTool? Tool;

        Block? harvestedBlock;

        /// <summary>
        /// The code to use for the interaction help of this block.
        /// </summary>
        [DocumentAsJson("Optional", "blockhelp-harvetable-harvest")]
        string interactionHelpCode = "blockhelp-harvetable-harvest";

        public BlockBehaviorHarvestable(Block block) : base(block)
        {
        }

        public override void Initialize(JsonObject properties)
        {
            base.Initialize(properties);

            interactionHelpCode = properties["interactionHelpCode"].AsString("blockhelp-harvetable-harvest");
            harvestTime = properties["harvestTime"].AsFloat(0);
            Tool = properties["tool"].AsObject<EnumTool?>(null);
            harvestedStacks = properties["harvestedStacks"].AsObject<BlockDropItemStack[]>(null);
            BlockDropItemStack? tempStack = properties["harvestedStack"].AsObject<BlockDropItemStack>(null);
            if (harvestedStacks == null && tempStack != null)
            {
                harvestedStacks = new BlockDropItemStack[1];
                harvestedStacks[0] = tempStack;
            }
            exchangeBlock = properties["exchangeBlock"].AsBool(false);

            string? code = properties["harvestingSound"].AsString("game:sounds/block/leafy-picking");
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
            if (Tool != null && byPlayer.InventoryManager.ActiveTool != Tool) return false;

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
            if (Tool != null && byPlayer.InventoryManager.ActiveTool != Tool) return false;

            if (blockSel == null) return false;

            handled = EnumHandling.PreventDefault;

            (byPlayer as IClientPlayer)?.TriggerFpAnimation(EnumHandInteract.HeldItemAttack);

            if (world.Rand.NextDouble() < 0.05)
            {
                world.PlaySoundAt(harvestingSound, blockSel.Position, 0, byPlayer);
            }

            if (world.Side == EnumAppSide.Client && world.Rand.NextDouble() < 0.25 && harvestedStacks?[0]?.ResolvedItemstack != null)
            {
                world.SpawnCubeParticles(blockSel.Position.ToVec3d().Add(blockSel.HitPosition), harvestedStacks[0].ResolvedItemstack, 0.25f, 1, 0.5f, byPlayer, new Vec3f(0, 1, 0));
            }

            return world.Side == EnumAppSide.Client || secondsUsed < harvestTime;
        }

        public override void OnBlockInteractStop(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ref EnumHandling handled)
        {
            if (Tool != null && byPlayer.InventoryManager.ActiveTool != Tool) return;

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
                    ItemStack? stack = harvestedStack.GetNextItemStack(dropRate);
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

                if (Tool != null)
                {
                    var toolSlot = byPlayer.InventoryManager.ActiveHotbarSlot;
                    toolSlot.Itemstack?.Collectible.DamageItem(world, byPlayer.Entity, toolSlot);
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
                        MouseButton = EnumMouseButton.Right,
                        Itemstacks = Tool == null ? null : ObjectCacheUtil.GetToolStacks(world.Api, (EnumTool)Tool)
                    }
                };
            }

            return base.GetPlacedBlockInteractionHelp(world, selection, forPlayer, ref handled);
        }
    }
}
