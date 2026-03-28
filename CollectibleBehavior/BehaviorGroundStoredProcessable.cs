using System.Linq;
using Vintagestory.API;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
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
    ///				"tool": "knife",
    ///				"requiredSurfaceMaterials": ["Stone", "Metal", "Brick"],
    ///				"transferFreshness": true
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

        /// <summary>
        /// If set, changes how much damage is done to the tool in the process.
        /// </summary>
        [DocumentAsJson("Optional", "false")]
        bool transferFreshness = false;

        /// <summary>
        /// If set, the block below the ground storage must be one of these materials for processing to work.
        /// Valid values: Stone, Metal, Mantle, Brick, Ore, Ceramic, Wood, etc.
        /// If null (default), processing works on any surface.
        /// </summary>
        [DocumentAsJson("Optional", "None")]
        public EnumBlockMaterial[]? RequiredSurfaceMaterials;

        /// <summary>
        /// The lang key for the error message shown when the surface material requirement is not met.
        /// </summary>
        [DocumentAsJson("Optional", "itemore-needssolid-error")]
        string? surfaceErrorLangCode;

        /// <summary>
        /// A sound to play when processing completes. If not set, ProcessingSound is used.
        /// </summary>
        [DocumentAsJson("Optional", "None")]
        public AssetLocation? CompletionSound;

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
            ProcessingAnimationCode = properties["processingAnimationCode"].AsString();
            string? code = properties["processingSound"].AsString();
            if (code != null) {
                ProcessingSound = AssetLocation.Create(code, collObj.Code.Domain);
            }
            code = properties["completionSound"].AsString();
            if (code != null) {
                CompletionSound = AssetLocation.Create(code, collObj.Code.Domain);
            }

            RemainingItem = properties["remainingItem"].AsObject<JsonItemStack?>();

            Tool = properties["tool"].AsObject<EnumTool?>();
            toolDamage = properties["toolDurabilityCost"].AsInt(1);
            transferFreshness = properties["transferFreshness"].AsBool();
            RequiredSurfaceMaterials = properties["requiredSurfaceMaterials"].AsObject<EnumBlockMaterial[]?>();
            surfaceErrorLangCode = properties["surfaceErrorLangCode"].AsString("itemore-needssolid-error");
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

        bool canProcessOnSurfaceMaterial(BlockEntityContainer be, IPlayer byPlayer)
        {
            if (RequiredSurfaceMaterials == null) return true;

            var belowMaterial = be.Api.World.BlockAccessor.GetBlock(be.Pos.DownCopy()).BlockMaterial;
            return RequiredSurfaceMaterials.Contains(belowMaterial);
        }

        public bool OnContainedInteractStart(BlockEntityContainer be, ItemSlot slot, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (Tool != null && byPlayer.InventoryManager.ActiveTool != Tool) return false;

            if (!byPlayer.Entity.Controls.ShiftKey) return false;

            if (!be.Api.World.Claims.TryAccess(byPlayer, blockSel.Position, EnumBlockAccessFlags.Use))
            {
                return false;
            }

            if (!canProcessOnSurfaceMaterial(be, byPlayer))
            {
                (be.Api as ICoreClientAPI)?.TriggerIngameError(this, "needssolidsurface", Lang.Get(surfaceErrorLangCode));
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

            if (!canProcessOnSurfaceMaterial(be, byPlayer)) return false;

            if (ProcessingAnimationCode != null && byPlayer.Entity.World is IClientWorldAccessor) byPlayer.Entity.StartAnimation(ProcessingAnimationCode);

            if (be.Api.World.Rand.NextDouble() < 0.05)
            {
                be.Api.World.PlaySoundAt(ProcessingSound, blockSel.Position, 0, byPlayer);
            }

            if (be.Api.World.Side == EnumAppSide.Client && be.Api.World.Rand.NextDouble() < 0.25 && (ProcessedStacks?[0]?.ResolvedItemstack ?? RemainingItem?.ResolvedItemstack) != null)
            {
                be.Api.World.SpawnCubeParticles(blockSel.Position.ToVec3d().Add(blockSel.HitPosition), ProcessedStacks?[0]?.ResolvedItemstack ?? RemainingItem!.ResolvedItemstack, 0.25f, 1, 0.5f, byPlayer, new Vec3f(0, 1, 0));
            }

            return secondsUsed < ProcessTime;
        }

        public void OnContainedInteractStop(float secondsUsed, BlockEntityContainer be, ItemSlot slot, IPlayer byPlayer, BlockSelection blockSel)
        {
            byPlayer.Entity.StopAnimation(ProcessingAnimationCode);
            if (Tool != null && byPlayer.InventoryManager.ActiveTool != Tool) return;

            if (!canProcessOnSurfaceMaterial(be, byPlayer)) return;

            if (secondsUsed > ProcessTime - 0.05f && (ProcessedStacks != null || RemainingItem != null) && be.Api.World.Side == EnumAppSide.Server)
            {
                ProcessedStacks?.Foreach(processedStack =>
                {
                    ItemStack? stack = processedStack.GetNextItemStack();
                    if (stack == null) return;
                    var origStack = stack.Clone();
                    var quantity = stack.StackSize;
                    if (transferFreshness)
                    {
                        TransitionableProperties[]? tprops = stack.Collectible.GetTransitionableProperties(be.Api.World, stack, null);
                        TransitionableProperties? perishProps = tprops?.FirstOrDefault(p => p.Type == EnumTransitionType.Perish);

                        // Carry over freshness
                        if (perishProps != null)
                        {
                            CollectibleObject.CarryOverFreshness(be.Api, slot, stack, perishProps);
                        }
                    }

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

                ItemStack? remainingStack = RemainingItem?.ResolvedItemstack?.Clone();
                if (transferFreshness)
                {
                    TransitionableProperties[]? tprops = remainingStack?.Collectible.GetTransitionableProperties(be.Api.World, remainingStack, null);
                    TransitionableProperties? perishProps = tprops?.FirstOrDefault(p => p.Type == EnumTransitionType.Perish);

                    // Carry over freshness
                    if (perishProps != null)
                    {
                        CollectibleObject.CarryOverFreshness(be.Api, slot, remainingStack, perishProps);
                    }
                }

                if (slot.Itemstack.StackSize > 1)
                {
                    if (remainingStack != null)
                    {
                        var origStack = remainingStack.Clone();
                        var quantity = remainingStack.StackSize;
                        if (!byPlayer.InventoryManager.TryGiveItemstack(remainingStack))
                        {
                            be.Api.World.SpawnItemEntity(remainingStack, blockSel.Position);
                        }

                        be.Api.World.Logger.Audit("{0} Took {1}x{2} from {3} at {4}.",
                            byPlayer.PlayerName,
                            quantity,
                            remainingStack.Collectible.Code,
                            collObj.Code,
                            blockSel.Position
                        );

                        TreeAttribute tree = new TreeAttribute();
                        tree["itemstack"] = new ItemstackAttribute(origStack.Clone());
                        tree["byentityid"] = new LongAttribute(byPlayer.Entity.EntityId);
                        be.Api.World.Api.Event.PushEvent("onitemcollected", tree);
                    }

                    slot.TakeOut(1);
                }
                else slot.Itemstack = remainingStack;
                be.MarkDirty(true);
                if (be.Inventory.Empty) be.Api.World.BlockAccessor.SetBlock(0, blockSel.Position);

                if (Tool != null)
                {
                    var toolSlot = byPlayer.InventoryManager.ActiveHotbarSlot;
                    toolSlot.Itemstack?.Collectible.DamageItem(be.Api.World, byPlayer.Entity, toolSlot, toolDamage);
                }

                be.Api.World.PlaySoundAt(CompletionSound ?? ProcessingSound, blockSel.Position, 0, byPlayer);
            }
        }

        public bool OnContainedInteractCancel(float secondsUsed, BlockEntityContainer be, ItemSlot slot, IPlayer byPlayer, BlockSelection blockSel, EnumItemUseCancelReason cancelReason)
        {
            byPlayer.Entity.StopAnimation(ProcessingAnimationCode);
            return true;
        }


        public WorldInteraction[] GetContainedInteractionHelp(BlockEntityContainer be, ItemSlot slot, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (ProcessedStacks != null || RemainingItem != null)
            {
                if (!canProcessOnSurfaceMaterial(be, byPlayer)) return [];

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
