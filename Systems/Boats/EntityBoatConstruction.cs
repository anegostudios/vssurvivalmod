using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

#nullable disable

namespace Vintagestory.GameContent
{
    public class ConstructionIgredient : CraftingRecipeIngredient
    {
        public string StoreWildCard;

        public new ConstructionIgredient Clone()
        {
            var c = CloneTo<ConstructionIgredient>();
            c.StoreWildCard = StoreWildCard;
            return c;
        }
    }

    public class ConstructionStage
    {
        public string[] AddElements;
        public string[] RemoveElements;
        public ConstructionIgredient[] RequireStacks;
        public string ActionLangCode = "rollers-construct";
    }

    public class EntityBoatConstruction : Entity
    {
        public override double FrustumSphereRadius => base.FrustumSphereRadius * 2;

        ConstructionStage[] stages;
        string material = "oak";

        Vec3f launchStartPos = new Vec3f();
        Dictionary<string, string> storedWildCards = new();

        WorldInteraction[] nextConstructWis;

        int CurrentStage
        {
            get { return WatchedAttributes.GetInt("currentStage", 0); }
            set { WatchedAttributes.SetInt("currentStage", value); }
        }

        public override void Initialize(EntityProperties properties, ICoreAPI api, long InChunkIndex3d)
        {
            requirePosesOnServer = true;
            WatchedAttributes.RegisterModifiedListener("currentStage", stagedChanged);
            WatchedAttributes.RegisterModifiedListener("wildcards", loadWildcards);
            base.Initialize(properties, api, InChunkIndex3d);
            stages = properties.Attributes["stages"].AsArray<ConstructionStage>();

            genNextInteractionStage();
        }

        private void stagedChanged()
        {
            MarkShapeModified();
            genNextInteractionStage();
        }

        public override void OnTesselation(ref Shape entityShape, string shapePathForLogging)
        {
            HashSet<string> addElements = new HashSet<string>();

            int cstage = CurrentStage;
            for (int i = 0; i <= cstage; i++)
            {
                var stage = stages[i];
                if (stage.AddElements != null)
                {
                    foreach (var addele in stage.AddElements)
                    {
                        addElements.Add(addele + "/*");
                    }
                }

                if (stage.RemoveElements != null)
                {
                    foreach (var remele in stage.RemoveElements)
                    {
                        addElements.Remove(remele + "/*");
                    }
                }
            }

            var esr = Properties.Client.Renderer as EntityShapeRenderer;
            if (esr != null)
            {
                esr.OverrideSelectiveElements = addElements.ToArray();
            }

            ICoreClientAPI capi = Api as ICoreClientAPI;
            if (capi != null) {
                setTexture("debarked", new AssetLocation(string.Format("block/wood/debarked/{0}", material)));
                setTexture("planks", new AssetLocation(string.Format("block/wood/planks/{0}1", material)));
            }

            base.OnTesselation(ref entityShape, shapePathForLogging);
        }

        private void setTexture(string code, AssetLocation assetLocation)
        {
            ICoreClientAPI capi = Api as ICoreClientAPI;
            var ctex = Properties.Client.Textures[code] = new CompositeTexture(assetLocation);
            capi.EntityTextureAtlas.GetOrInsertTexture(ctex, out int tui, out _);
            ctex.Baked.TextureSubId = tui;
        }

        EntityAgent launchingEntity;
        public override void OnInteract(EntityAgent byEntity, ItemSlot handslot, Vec3d hitPosition, EnumInteractMode mode)
        {
            base.OnInteract(byEntity, handslot, hitPosition, mode);

            if (Api.Side == EnumAppSide.Client) return;
            if (CurrentStage >= stages.Length - 1) return;

            if (CurrentStage == 0 && handslot.Empty && byEntity.Controls.ShiftKey)
            {
                byEntity.TryGiveItemStack(new ItemStack(Api.World.GetItem(new AssetLocation("roller")), 5));
                Die();
                return;
            }

            if (!tryConsumeIngredients(byEntity, handslot)) return;

            if (CurrentStage < stages.Length - 1)
            {
                CurrentStage++;
                MarkShapeModified();
            }

            if (CurrentStage >= stages.Length - 2 && !AnimManager.IsAnimationActive("launch"))
            {
                launchingEntity = byEntity;
                launchStartPos = getCenterPos();
                StartAnimation("launch");
            }

            genNextInteractionStage();
        }

        private Vec3f getCenterPos()
        {
            AttachmentPointAndPose apap = AnimManager.Animator?.GetAttachmentPointPose("Center");
            if (apap != null)
            {
                var mat = new Matrixf();
                mat.RotateY(ServerPos.Yaw + GameMath.PIHALF);
                apap.Mul(mat);
                return mat.TransformVector(new Vec4f(0, 0, 0, 1)).XYZ;
            }

            return null;
        }

        private void genNextInteractionStage()
        {
            if (CurrentStage + 1 >= stages.Length)
            {
                nextConstructWis = null;
                return;
            }

            var stage = stages[CurrentStage+1];

            if (stage.RequireStacks == null)
            {
                nextConstructWis = null;
                return;
            }

            var wis = new List<WorldInteraction>();

            int i = 0;
            foreach (var ingred in stage.RequireStacks)
            {
                List<ItemStack> stacksl = new();

                foreach (var val in storedWildCards)
                {
                    ingred.FillPlaceHolder(val.Key, val.Value);
                }
                if (!ingred.Resolve(Api.World, "Require stack for construction stage " + (CurrentStage + 1) + " on entity " + this.Code))
                {
                    return;
                }
                i++;
                foreach (var obj in Api.World.Collectibles)
                {
                    var stack = new ItemStack(obj);
                    if (ingred.SatisfiesAsIngredient(stack, false))
                    {
                        stack.StackSize = ingred.Quantity;
                        stacksl.Add(stack);
                    }
                }

                var stacks = stacksl.ToArray();

                wis.Add(new WorldInteraction()
                {
                    ActionLangCode = stage.ActionLangCode,
                    Itemstacks = stacks,
                    GetMatchingStacks = (wi, bs, es) => stacks,
                    MouseButton = EnumMouseButton.Right
                });
            }

            if (stage.RequireStacks.Length == 0)
            {
                wis.Add(new WorldInteraction()
                {
                    ActionLangCode = stage.ActionLangCode,
                    MouseButton = EnumMouseButton.Right
                });
            }

            nextConstructWis = wis.ToArray();
        }

        private bool tryConsumeIngredients(EntityAgent byEntity, ItemSlot handslot)
        {
            var sapi = Api as ICoreServerAPI;
            var plr = (byEntity as EntityPlayer).Player as IServerPlayer;

            var stage = stages[CurrentStage+1];
            var hotbarinv = plr.InventoryManager.GetHotbarInventory();

            List<KeyValuePair<ItemSlot, int>> takeFrom = new List<KeyValuePair<ItemSlot, int>>();
            List<ConstructionIgredient> requireIngreds = new List<ConstructionIgredient>();
            if (stage.RequireStacks == null) return true;

            for (int i = 0; i < stage.RequireStacks.Length; i++) requireIngreds.Add(stage.RequireStacks[i].Clone());

            Dictionary<string, string> storeWildCard = new();


            bool skipMatCost = plr?.WorldData.CurrentGameMode == EnumGameMode.Creative && byEntity.Controls.CtrlKey;


            foreach (var slot in hotbarinv)
            {
                if (slot.Empty) continue;
                if (requireIngreds.Count == 0) break;

                for (int i = 0; i < requireIngreds.Count; i++)
                {
                    var ingred = requireIngreds[i];

                    foreach (var val in storedWildCards) {
                        ingred.FillPlaceHolder(val.Key, val.Value);
                    }
                    ingred.Resolve(Api.World, "Require stack for construction stage "+i+" on entity " + this.Code);

                    if (!skipMatCost && ingred.SatisfiesAsIngredient(slot.Itemstack, false))
                    {
                        int amountToTake = Math.Min(ingred.Quantity, slot.Itemstack.StackSize);
                        takeFrom.Add(new KeyValuePair<ItemSlot, int>(slot, amountToTake));

                        ingred.Quantity -= amountToTake;
                        if (ingred.Quantity <= 0)
                        {
                            requireIngreds.RemoveAt(i);
                            i--;

                            if (ingred.StoreWildCard != null)
                            {
                                storeWildCard[ingred.StoreWildCard] = slot.Itemstack.Collectible.Variant[ingred.StoreWildCard];
                            }
                        }
                    } else if (skipMatCost)
                    {
                        if (ingred.StoreWildCard != null)
                        {
                            storeWildCard[ingred.StoreWildCard] = slot.Itemstack.Collectible.Variant[ingred.StoreWildCard];
                        }
                    }
                }
            }

            if (!skipMatCost && requireIngreds.Count > 0)
            {
                var ingred = requireIngreds[0];
                string langCode = plr.LanguageCode;
                plr.SendIngameError("missingstack", null, ingred.Quantity, ingred.IsWildCard ? Lang.GetL(langCode, ingred.Name??"") : ingred.ResolvedItemstack.GetName());
                return false;
            }

            foreach (var val in storeWildCard)
            {
                this.storedWildCards[val.Key] = val.Value;
            }

            if (!skipMatCost)
            {
                bool soundPlayed = false;
                foreach (var kvp in takeFrom)
                {
                    if (!soundPlayed)
                    {
                        AssetLocation soundLoc = null;
                        var stack = kvp.Key.Itemstack;
                        if (stack.Block != null) soundLoc = stack.Block.Sounds?.Place;

                        if (soundLoc == null)
                        {
                            soundLoc = stack.Collectible.GetBehavior<CollectibleBehaviorGroundStorable>()?.StorageProps?.PlaceRemoveSound;
                        }

                        if (soundLoc != null)
                        {
                            soundPlayed = true;
                            Api.World.PlaySoundAt(soundLoc, this, null);
                        }
                    }

                    kvp.Key.TakeOut(kvp.Value);
                    kvp.Key.MarkDirty();
                }
            }

            storeWildcards();
            WatchedAttributes.MarkPathDirty("wildcards");

            return true;
        }

        private ItemSlot tryTakeFrom(CraftingRecipeIngredient requireStack, List<ItemSlot> skipSlots, IReadOnlyCollection<ItemSlot> fromSlots)
        {
            foreach (var slot in fromSlots)
            {
                if (slot.Empty) continue;
                if (skipSlots.Contains(slot)) continue;

                if (requireStack.SatisfiesAsIngredient(slot.Itemstack, true))
                {
                    return slot;
                }
            }

            return null;
        }

        public override void OnGameTick(float dt)
        {
            base.OnGameTick(dt);

            var prg = AnimManager.Animator?.GetAnimationState("launch").AnimProgress ?? 0;
            if (prg >= 0.99)
            {
                AnimManager.StopAnimation("launch");
                CurrentStage = 0;
                MarkShapeModified();
                if (World.Side == EnumAppSide.Server) Spawn();
            }
        }


        private void Spawn()
        {
            var nowOff = getCenterPos();
            Vec3f offset = nowOff == null ? new Vec3f() : nowOff - launchStartPos;

            EntityProperties type = World.GetEntityType(new AssetLocation("boat-sailed-" + material));
            var entity = World.ClassRegistry.CreateEntity(type);

            if ((int)Math.Abs(ServerPos.Yaw * GameMath.RAD2DEG) == 90 || (int)Math.Abs(ServerPos.Yaw * GameMath.RAD2DEG) == 270) {
                offset.X *= 1.1f;
            }

            offset.Y = 0.5f;

            entity.ServerPos.SetFrom(ServerPos).Add(offset);
            entity.ServerPos.Motion.Add(offset.X / 50.0, 0, offset.Z / 50.0);

            var plr = (launchingEntity as EntityPlayer)?.Player;
            if (plr != null)
            {
                entity.WatchedAttributes.SetString("createdByPlayername", plr.PlayerName);
                entity.WatchedAttributes.SetString("createdByPlayerUID", plr.PlayerUID);
            }

            entity.Pos.SetFrom(entity.ServerPos);
            World.SpawnEntity(entity);
        }

        public override WorldInteraction[] GetInteractionHelp(IClientWorldAccessor world, EntitySelection es, IClientPlayer player)
        {
            var wis = base.GetInteractionHelp(world, es, player);
            if (nextConstructWis == null) return wis;

            wis = wis.Append(nextConstructWis);

            if (CurrentStage == 0)
            {
                wis = wis.Append(new WorldInteraction()
                {
                    HotKeyCode = "sneak",
                    RequireFreeHand = true,
                    MouseButton = EnumMouseButton.Right,
                    ActionLangCode = "rollers-deconstruct"
                });
            }

            return wis;
        }

        public override void ToBytes(BinaryWriter writer, bool forClient)
        {
            storeWildcards();
            base.ToBytes(writer, forClient);
        }

        private void storeWildcards()
        {
            var tree = new TreeAttribute();
            foreach (var val in storedWildCards) tree[val.Key] = new StringAttribute(val.Value);
            WatchedAttributes["wildcards"] = tree;
        }

        public override void FromBytes(BinaryReader reader, bool isSync)
        {
            base.FromBytes(reader, isSync);
            loadWildcards();
        }

        public override string GetInfoText()
        {
            return base.GetInfoText() + "\n" + Lang.Get("Material: {0}", Lang.Get("material-" + material));
        }

        private void loadWildcards()
        {
            storedWildCards.Clear();
            var tree = WatchedAttributes["wildcards"] as TreeAttribute;
            if (tree != null)
            {
                foreach (var val in tree)
                {
                    storedWildCards[val.Key] = (val.Value as StringAttribute).value;
                }
            }

            if (storedWildCards.TryGetValue("wood", out var wood))
            {
                material = wood;
                if (material == null || material.Length == 0) storedWildCards["wood"] = material = "oak";
            }
        }
    }
}
