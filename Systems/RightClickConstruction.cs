using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.GameContent
{
    public class ConstructionIngredient : CraftingRecipeIngredient
    {
        public string StoreWildCard;

        public override ConstructionIngredient Clone()
        {
            ConstructionIngredient result = new();

            CloneTo(result);

            return result;
        }

        protected override void CloneTo(object cloneTo)
        {
            base.CloneTo(cloneTo);

            if (cloneTo is ConstructionIngredient ingredient)
            {
                ingredient.StoreWildCard = StoreWildCard;
            }
        }
    }

    public class ConstructionStage
    {
        public string[] AddElements;
        public string[] RemoveElements;
        public ConstructionIngredient[] RequireStacks;
        public string ActionLangCode = "rollers-construct";
    }

    public class RightClickConstruction
    {
        public ConstructionStage[] Stages;
        public int CurrentCompletedStage;
        public delegate Vec3d PositionSupplierDelegate();
        public Dictionary<string, string> StoredWildCards = new();

        protected WorldInteraction[] nextConstructWis;
        protected string codeForErrorLogging;
        protected PositionSupplierDelegate positionForSound;
        protected int dimension;
        protected ICoreAPI api;

        public RightClickConstruction() { }

        public void LateInit(ConstructionStage[] stages, ICoreAPI api, PositionSupplierDelegate positionForSound, string codeForErrorLogging)
        {
            this.Stages = stages;
            this.api = api;
            this.codeForErrorLogging = codeForErrorLogging;
            this.positionForSound = positionForSound;
        }

        /// <summary>
        /// Returns true if ingredients were consumed
        /// </summary>
        /// <param name="byEntity"></param>
        /// <param name="handslot"></param>
        /// <returns></returns>
        public bool OnInteract(EntityAgent byEntity, ItemSlot handslot)
        {
            if (CurrentCompletedStage >= Stages.Length - 1) return false;

            if (!tryConsumeIngredients(byEntity, handslot)) return false;

            if (CurrentCompletedStage < Stages.Length - 1)
            {
                CurrentCompletedStage++;
            }

            GenInteractionHelp();
            return true;
        }

        public string[] getShapeElements()
        {
            HashSet<string> addElements = new();

            int cstage = CurrentCompletedStage;
            for (int i = 0; i <= cstage; i++)
            {
                ConstructionStage stage = Stages[i];
                if (stage.AddElements != null)
                {
                    foreach (string addele in stage.AddElements)
                    {
                        addElements.Add(addele + "/*");
                    }
                }

                if (stage.RemoveElements != null)
                {
                    foreach (string remele in stage.RemoveElements)
                    {
                        addElements.Remove(remele + "/*");
                    }
                }
            }

            return addElements.ToArray();
        }

        public MeshData Tesselate(TesselationMetaData meta, ITesselatorAPI tesselator, Shape shape)
        {
            meta.SelectiveElements = getShapeElements();
            tesselator.TesselateShape(meta, shape, out MeshData meshdata);
            return meshdata;
        }



        protected void GenInteractionHelp()
        {
            if (CurrentCompletedStage + 1 >= Stages.Length)
            {
                nextConstructWis = null;
                return;
            }

            ConstructionStage stage = Stages[CurrentCompletedStage + 1];

            if (stage.RequireStacks == null)
            {
                nextConstructWis = null;
                return;
            }

            List<WorldInteraction> wis = new();

            int i = 0;
            foreach (ConstructionIngredient ingred in stage.RequireStacks)
            {
                List<ItemStack> stacksl = new();

                foreach (KeyValuePair<string, string> val in StoredWildCards)
                {
                    ingred.FillPlaceHolder(val.Key, val.Value);
                }

                if (!ingred.Resolve(api.World, "Require stack for construction stage " + (CurrentCompletedStage + 1) + " on " + this.codeForErrorLogging))
                {
                    return;
                }

                i++;
                foreach (CollectibleObject obj in api.World.Collectibles)
                {
                    ItemStack stack = new(obj);
                    if (ingred.SatisfiesAsIngredient(stack, false))
                    {
                        stack.StackSize = ingred.Quantity;
                        stacksl.Add(stack);
                    }
                }

                ItemStack[] stacks = stacksl.ToArray();

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

        public ItemStack[] GetDrops(float dropRatio = 1f, Random rnd = null)
        {
            if (CurrentCompletedStage <= 1) return Array.Empty<ItemStack>();

            List<ItemStack> stacks = new();

            for (int i = 0; i < CurrentCompletedStage; i++)
            {
                ConstructionStage stage = Stages[i];
                if (stage.RequireStacks == null) continue;

                foreach (ConstructionIngredient ingred in stage.RequireStacks)
                {
                    List<ItemStack> stacksl = new();
                    foreach (KeyValuePair<string, string> val in StoredWildCards)
                    {
                        ingred.FillPlaceHolder(val.Key, val.Value);
                    }

                    ConstructionIngredient resolveIngred = ingred;

                    if (ingred.StoreWildCard != null)
                    {
                        resolveIngred = ingred.Clone();
                        resolveIngred.Code.Path = resolveIngred.Code.Path.Replace("*", StoredWildCards[ingred.StoreWildCard]);
                    }

                    if (resolveIngred.Resolve(api.World, "Drop stack for construction stage " + i + " on " + this.codeForErrorLogging))
                    {
                        ItemStack stack = resolveIngred.ResolvedItemStack.Clone();
                        stack.StackSize = GameMath.RoundRandom(rnd, stack.StackSize * dropRatio);
                        stacks.Add(stack);
                    }
                }
            }

            return stacks.ToArray();
        }

        private bool tryConsumeIngredients(EntityAgent byEntity, ItemSlot handslot)
        {
            IPlayer plr = (byEntity as EntityPlayer).Player;

            ConstructionStage stage = Stages[CurrentCompletedStage + 1];
            IInventory hotbarinv = plr.InventoryManager.GetHotbarInventory();

            List<KeyValuePair<ItemSlot, int>> takeFrom = new();
            List<ConstructionIngredient> requireIngreds = new();
            if (stage.RequireStacks == null) return true;

            for (int i = 0; i < stage.RequireStacks.Length; i++) requireIngreds.Add(stage.RequireStacks[i].Clone());

            Dictionary<string, string> storeWildCard = new();

            bool skipMatCost = plr?.WorldData.CurrentGameMode == EnumGameMode.Creative && byEntity.Controls.CtrlKey;

            for (int i = 0; i < requireIngreds.Count; i++)
            {
                ConstructionIngredient ingred = requireIngreds[i];
                foreach (KeyValuePair<string, string> val in StoredWildCards)
                {
                    ingred.FillPlaceHolder(val.Key, val.Value);
                }
                if (!ingred.Resolve(api.World, "Require stack for construction stage " + i + " on " + codeForErrorLogging))
                {
                    if (ingred.MatchingType == EnumRecipeMatchType.Exact) return false;
                }
            }



            foreach (ItemSlot slot in hotbarinv)
            {
                if (slot.Empty) continue;
                if (requireIngreds.Count == 0) break;

                for (int i = 0; i < requireIngreds.Count; i++)
                {
                    ConstructionIngredient ingred = requireIngreds[i];

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
                    }
                    else if (skipMatCost)
                    {
                        if (ingred.StoreWildCard != null)
                        {
                            storeWildCard["wood"] = "oak"; // skipMatCost is just for creative mode debugging, so don't care if this is hardcoded here
                            storeWildCard["metal"] = "iron";
                        }
                    }
                }
            }

            if (!skipMatCost && requireIngreds.Count > 0)
            {
                ConstructionIngredient ingred = requireIngreds[0];
                if (plr is IClientPlayer cplr)
                {
                    (plr.Entity.Api as ICoreClientAPI).TriggerIngameError(
                        this,
                        "missingstack",
                        Lang.Get("ingameerror-missingstack", ingred.Quantity, ingred.MatchingType != EnumRecipeMatchType.Exact ? Lang.Get(ingred.Name ?? "") : ingred.ResolvedItemStack.GetName())
                    );
                }

                return false;
            }

            foreach (KeyValuePair<string, string> val in storeWildCard)
            {
                this.StoredWildCards[val.Key] = val.Value;
            }

            if (!skipMatCost)
            {
                bool soundPlayed = false;
                foreach (KeyValuePair<ItemSlot, int> kvp in takeFrom)
                {
                    if (!soundPlayed)
                    {
                        AssetLocation soundLoc = null;
                        ItemStack stack = kvp.Key.Itemstack;
                        if (stack.Block != null) soundLoc = stack.Block.Sounds?.Place.Location;

                        if (soundLoc == null)
                        {
                            soundLoc = stack.Collectible.GetBehavior<CollectibleBehaviorGroundStorable>()?.StorageProps?.PlaceRemoveSound;
                        }

                        if (soundLoc != null)
                        {
                            soundPlayed = true;
                            Vec3d pos = positionForSound?.Invoke();
                            api.World.PlaySoundAt(new SoundAttributes() { Location = soundLoc }, pos.X, pos.Y, pos.Z, dimension, null);
                        }
                    }

                    kvp.Key.TakeOut(kvp.Value);
                    kvp.Key.MarkDirty();
                }
            }

            return true;
        }

        public void ToTreeAttributes(ITreeAttribute tree)
        {
            TreeAttribute subtree = new();
            foreach (KeyValuePair<string, string> val in StoredWildCards) subtree[val.Key] = new StringAttribute(val.Value);
            tree["wildcards"] = subtree;

            tree.SetInt("currentStage", CurrentCompletedStage);
        }

        public void FromTreeAttributes(ITreeAttribute tree)
        {
            StoredWildCards.Clear();
            TreeAttribute subtree = tree["wildcards"] as TreeAttribute;
            if (subtree != null)
            {
                foreach (KeyValuePair<string, IAttribute> val in subtree)
                {
                    StoredWildCards[val.Key] = (val.Value as StringAttribute).value;
                }
            }

            CurrentCompletedStage = tree.GetInt("currentStage", 0);
        }

        public WorldInteraction[] GetInteractionHelp(IWorldAccessor world, IPlayer player)
        {
            GenInteractionHelp();
            return nextConstructWis;
        }
    }
}
