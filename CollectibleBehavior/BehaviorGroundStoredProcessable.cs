using Vintagestory.API;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    /// <summary>
    /// Allows an item to be processed with the right mouse button when in ground storage.
    /// Uses the code "GroundStoredProcessable".
    /// </summary>
    /// <example><code lang="json">
    ///"behaviorsByType": {
	///	"*-ripe": [
	///		{
	///			"name": "GroundStoredProcessable",
	///			"properties": {
	///				"processTime": 0.6,
	///				"processedStack": {
	///					"type": "item",
	///					"code": "fruit-{type}",
	///					"quantity": { "avg": 4.4 }
	///				},
	///				"remainingItem": {
    ///				    "type": "item",
    ///				    "code": "bowstave-recurve-raw"
    ///				},
    ///				"tool": "knife"
	///			}
	///		}
	///	]
	///}
    /// </code></example>
    public class CollectibleBehaviorGroundStoredProcessable : CollectibleBehavior, IContainedInteractable
    {
        /// <summary>
        /// The amount of time, in seconds, it takes to process this item.
        /// </summary>
        [DocumentAsJson("Recommended", "0")]
        public float ProcessTime;

        /// <summary>
        /// An array of drops for when the block is processed.
        /// </summary>
        [DocumentAsJson("Required")]
        public BlockDropItemStack[]? ProcessedStacks = [];

        /// <summary>
        /// The sound to play whilst the object is being processed.
        /// </summary>
        [DocumentAsJson("Optional", "None")]
        public AssetLocation? ProcessingSound;

        /// <summary>
        /// The animation to play whilst the object is being processed. Needs to match an animation code on the Seraph.
        /// </summary>
        [DocumentAsJson("Optional", "None")]
        public string? ProcessingAnimationCode;

        /// <summary>
        /// The item to replace this one in ground storage after it is processed.
        /// </summary>
        [DocumentAsJson("Optional", "None")]
        public JsonItemStack? RemainingItem;

        /// <summary>
        /// The code to use for the interaction help of this item.
        /// </summary>
        [DocumentAsJson("Optional", "blockhelp-processable-process")]
        string? interactionHelpCode;

        /// <summary>
        /// The code to use for the interaction help of this item.
        /// </summary>
        [DocumentAsJson("Optional", "groundstoredprocessesdesc-title")]
        public string? HandbookProcessIntoTitle;

        /// <summary>
        /// The code to use for the interaction help of this item.
        /// </summary>
        [DocumentAsJson("Optional", "handbook-createdby-groundstoredprocessing")]
        public string? HandbookCreatedByTitle;

        /// <summary>
        /// If set, then the given tool is required to process.
        /// </summary>
        [DocumentAsJson("Optional", "None")]
        public EnumTool? Tool;

        /// <summary>
        /// If set, changes how much damage is done to the tool in the process.
        /// </summary>
        [DocumentAsJson("Optional", "1")]
         int toolDamage = 1;

        public CollectibleBehaviorGroundStoredProcessable(CollectibleObject collObj) : base(collObj)
        {
        }

        public override void Initialize(JsonObject properties)
        {
            base.Initialize(properties);

            interactionHelpCode = properties["interactionHelpCode"].AsString("blockhelp-processable-process");
            HandbookProcessIntoTitle = properties["handbookProcessIntoTitle"].AsString("groundstoredprocessesdesc-title");
            HandbookCreatedByTitle = properties["handbookCreatedByTitle"].AsString("handbook-createdby-groundstoredprocessing");
            ProcessTime = properties["processTime"].AsFloat();
            ProcessedStacks = properties["processedStacks"].AsObject<BlockDropItemStack[]?>();
            ProcessingAnimationCode = properties["AnimationCode"].AsString();
            string? code = properties["processingSound"].AsString();
            if (code != null) {
                ProcessingSound = AssetLocation.Create(code, collObj.Code.Domain);
            }

            RemainingItem = properties["remainingItem"].AsObject<JsonItemStack?>();

            Tool = properties["tool"].AsObject<EnumTool?>();
            toolDamage = properties["toolDurabilityCost"].AsInt(1);
        }

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            if (ProcessedStacks == null && RemainingItem == null)
            {
                api.Logger.Warning($"{collObj.Code} has no processedStacks or remainingItem specified for GroundStoredProcessable behavior");
            }

            ProcessedStacks?.Foreach(processedStack => processedStack?.Resolve(api.World, "processedStack of item ", collObj.Code));

            RemainingItem?.Resolve(api.World, "remainingItem of item ", collObj.Code);
        }

        public bool OnContainedInteractStart(BlockEntityContainer be, ItemSlot slot, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (Tool != null && byPlayer.InventoryManager.ActiveTool != Tool) return false;

            if (!byPlayer.Entity.Controls.ShiftKey) return false;

            if (!be.Api.World.Claims.TryAccess(byPlayer, blockSel.Position, EnumBlockAccessFlags.Use))
            {
                return false;
            }

            if (ProcessedStacks != null || RemainingItem != null)
            {
                be.Api.World.PlaySoundAt(ProcessingSound, blockSel.Position, 0, byPlayer);
                return true;
            }

            return false;
        }

        public bool OnContainedInteractStep(float secondsUsed, BlockEntityContainer be, ItemSlot slot, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (Tool != null && byPlayer.InventoryManager.ActiveTool != Tool) return false;

            if (!byPlayer.Entity.Controls.ShiftKey) return false;

            if (blockSel == null) return false;

            if (ProcessingAnimationCode != null && byPlayer.Entity.World is IClientWorldAccessor) byPlayer.Entity.StartAnimation(ProcessingAnimationCode);

            if (be.Api.World.Rand.NextDouble() < 0.05)
            {
                be.Api.World.PlaySoundAt(ProcessingSound, blockSel.Position, 0, byPlayer);
            }

            if (be.Api.World.Side == EnumAppSide.Client && be.Api.World.Rand.NextDouble() < 0.25 && (ProcessedStacks?[0]?.ResolvedItemstack ?? RemainingItem?.ResolvedItemstack) != null)
            {
                be.Api.World.SpawnCubeParticles(blockSel.Position.ToVec3d().Add(blockSel.HitPosition), ProcessedStacks?[0]?.ResolvedItemstack ?? RemainingItem!.ResolvedItemstack, 0.25f, 1, 0.5f, byPlayer, new Vec3f(0, 1, 0));
            }

            return be.Api.World.Side == EnumAppSide.Client || secondsUsed < ProcessTime;
        }

        public void OnContainedInteractStop(float secondsUsed, BlockEntityContainer be, ItemSlot slot, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (ProcessingAnimationCode != null) byPlayer.Entity.StopAnimation(ProcessingAnimationCode);
            if (Tool != null && byPlayer.InventoryManager.ActiveTool != Tool) return;

            if (secondsUsed > ProcessTime - 0.05f && (ProcessedStacks != null || RemainingItem != null) && be.Api.World.Side == EnumAppSide.Server)
            {
                ProcessedStacks?.Foreach(processedStack =>
                {
                    ItemStack? stack = processedStack.GetNextItemStack(slot.Itemstack.StackSize);
                    if (stack == null) return;
                    var origStack = stack.Clone();
                    var quantity = stack.StackSize;
                    if (!byPlayer.InventoryManager.TryGiveItemstack(stack))
                    {
                        be.Api.World.SpawnItemEntity(stack, blockSel.Position);
                    }
                    be.Api.World.Logger.Audit("{0} Took {1}x{2} from {3} at {4}.",
                        byPlayer.PlayerName,
                        quantity,
                        stack.Collectible.Code,
                        collObj.Code,
                        blockSel.Position
                    );

                    TreeAttribute tree = new TreeAttribute();
                    tree["itemstack"] = new ItemstackAttribute(origStack.Clone());
                    tree["byentityid"] = new LongAttribute(byPlayer.Entity.EntityId);
                    be.Api.World.Api.Event.PushEvent("onitemcollected", tree);
                });

                int stacksize = slot.StackSize;
                slot.Itemstack = RemainingItem?.ResolvedItemstack?.Clone();
                slot.Itemstack?.StackSize *= stacksize;
                be.MarkDirty(true);
                if (be.Inventory.Empty) be.Api.World.BlockAccessor.SetBlock(0, blockSel.Position);

                if (Tool != null)
                {
                    var toolSlot = byPlayer.InventoryManager.ActiveHotbarSlot;
                    toolSlot.Itemstack?.Collectible.DamageItem(be.Api.World, byPlayer.Entity, toolSlot, toolDamage);
                }

                be.Api.World.PlaySoundAt(ProcessingSound, blockSel.Position, 0, byPlayer);
            }
        }


        public WorldInteraction[] GetContainedInteractionHelp(BlockEntityContainer be, ItemSlot slot, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (ProcessedStacks != null || RemainingItem != null)
            {
                bool notProtected = true;

                if (be.Api.World.Claims != null && be.Api.World is IClientWorldAccessor clientWorld && clientWorld.Player?.WorldData.CurrentGameMode == EnumGameMode.Survival)
                {
                    EnumWorldAccessResponse resp = clientWorld.Claims.TestAccess(clientWorld.Player, blockSel.Position, EnumBlockAccessFlags.Use);
                    if (resp != EnumWorldAccessResponse.Granted) notProtected = false;
                }

                if (notProtected) return
                [
                    new()
                    {
                        ActionLangCode = interactionHelpCode,
                        MouseButton = EnumMouseButton.Right,
                        HotKeyCode = "shift",
                        Itemstacks = Tool == null ? null : ObjectCacheUtil.GetToolStacks(be.Api, (EnumTool)Tool)
                    }
                ];
            }

            return [];
        }
    }
}
