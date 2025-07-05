using System;
using System.Linq;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.GameContent
{
    /** Mechanics
     * 
     * 1.  / Any well-fed hen - i.e. ready to lay - will activate task AITaskSeekBlockAndLay
     * 2.  / Once sitting on the henbox for 5 real seconds, the hen will first attempt to lay an egg in the henbox
     * 3.  / If the hen can lay an egg (fewer than 3 eggs currently) it does so; makes an egg laying sound and activates another AITask.  TODO: we could add a flapping animation
     * 4. / The egg will be fertile (it will have a chickCode) if there was a male nearby; otherwise it will be infertile
     * 5.  / If the hen cannot lay an egg (henbox is full of 3 eggs already), the hen becomes "broody" and will sit on the eggs for a long time (around three quarters of a day)
     * 6.  / That broody hen or another broody hen will continue returning to the henbox and sitting on the eggs until they eventually hatch
     * 7.  / HenBox BlockEntity tracks how long a hen (any hen) has sat on the eggs warming them - as set in the chicken-hen JSON it needs 5 in-game days
     * 8.  / When the eggs have been warmed for long enough they hatch: chicks are spawned and the henbox reverts to empty
     * 
     * HenBox tracks the parent entity and the generation of each egg separately => in future could have 1 duck egg in a henbox for example, so that 1 duckling hatches and 2 hen chicks
     */

    public class BlockEntityHenBox : BlockEntityDisplay, IAnimalNest
    {
        protected InventoryGeneric inventory;
        public override InventoryBase Inventory => inventory;
        public string inventoryClassName = "nestbox";
        public override string InventoryClassName => inventoryClassName;

        public Vec3d Position => Pos.ToVec3d().Add(0.5, 0.5, 0.5);
        public string Type => "nest";

        public Entity occupier;

        protected double timeToIncubate;
        protected double occupiedTimeLast;
        protected bool IsOccupiedClientside = false;

        protected int Capacity
        {
            get => Block.Attributes?["quantitySlots"]?.AsInt(1) ?? 1;
        }


        public BlockEntityHenBox()
        {
            container = new ConstantPerishRateContainer(() => Inventory, "inventory");
        }


        public virtual bool IsSuitableFor(Entity entity, string[] nestTypes)
        {
            return nestTypes?.Contains(((BlockHenbox)Block).NestType) == true;
        }

        public bool Occupied(Entity entity)
        {
            return occupier != null && occupier != entity;
        }

        public virtual void SetOccupier(Entity entity)
        {
            if (occupier == entity)
            {
                return;
            }
            occupier = entity;
            MarkDirty();
        }

        public virtual float DistanceWeighting => 2 / (CountEggs() + 2);

        public virtual bool TryAddEgg(ItemStack egg)
        {
            for (int i = 0; i < inventory.Count; ++i)
            {
                if (inventory[i].Empty)
                {
                    inventory[i].Itemstack = egg;
                    inventory.DidModifyItemSlot(inventory[i]);
                    double? incubationDays = (egg.Attributes["chick"] as TreeAttribute)?.GetDouble("incubationDays");
                    if (incubationDays != null) timeToIncubate = Math.Max(timeToIncubate, (double)incubationDays);
                    occupiedTimeLast = Api.World.Calendar.TotalDays;
                    MarkDirty();
                    return true;
                }
            }
            return false;
        }

        public int CountEggs()
        {
            int count = 0;
            for (int i = 0; i < inventory.Count; ++i)
            {
                if (!inventory[i].Empty)
                {
                    ++count;
                }
            }
            return count;
        }

        protected virtual void On1500msTick(float dt)
        {
            if (timeToIncubate == 0) return;

            double newTime = Api.World.Calendar.TotalDays;
            if (occupier != null && occupier.Alive)   //Does this need a more sophisticated check, i.e. is the occupier's position still here?  (Also do we reset the occupier variable to null if save and re-load?)
            {
                if (newTime > occupiedTimeLast)
                {
                    timeToIncubate -= newTime - occupiedTimeLast;
                    this.MarkDirty();
                }
            }
            occupiedTimeLast = newTime;

            if (timeToIncubate <= 0)
            {
                timeToIncubate = 0;
                Random rand = Api.World.Rand;

                for (int i = 0; i < inventory.Count; ++i)
                {
                    TreeAttribute chickData = (TreeAttribute) inventory[i].Itemstack?.Attributes["chick"];
                    if (chickData == null) continue;

                    string chickCode = chickData.GetString("code");
                    if (chickCode == null || chickCode == "") continue;

                    EntityProperties childType = Api.World.GetEntityType(chickCode);
                    if (childType == null) continue;
                    Entity childEntity = Api.World.ClassRegistry.CreateEntity(childType);
                    if (childEntity == null) continue;

                    childEntity.ServerPos.SetFrom(new EntityPos(this.Position.X + (rand.NextDouble() - 0.5f) / 5f, this.Position.Y, this.Position.Z + (rand.NextDouble() - 0.5f) / 5f, (float) rand.NextDouble() * GameMath.TWOPI));
                    childEntity.ServerPos.Motion.X += (rand.NextDouble() - 0.5f) / 200f;
                    childEntity.ServerPos.Motion.Z += (rand.NextDouble() - 0.5f) / 200f;

                    childEntity.Pos.SetFrom(childEntity.ServerPos);
                    childEntity.Attributes.SetString("origin", "reproduction");
                    childEntity.WatchedAttributes.SetInt("generation", chickData.GetInt("generation", 0));
                    EntityAgent eagent = childEntity as EntityAgent;
                    if (eagent != null) eagent.HerdId = chickData.GetLong("herdID", 0);
                    Api.World.SpawnEntity(childEntity);

                    inventory[i].Itemstack = null;
                    inventory.DidModifyItemSlot(inventory[i]);
                }
            }
        }


        public override void Initialize(ICoreAPI api)
        {
            inventoryClassName = Block.Attributes?["inventoryClassName"]?.AsString() ?? inventoryClassName;
            int capacity = Capacity;
            if (inventory == null) {
                CreateInventory(capacity, api);
            }
            else if (capacity != inventory.Count) {
                api.Logger.Warning("Nest " + Block.Code + " loaded with " + inventory.Count + " capacity when it should be " + capacity + ".");
                InventoryGeneric oldInv = inventory;
                CreateInventory(capacity, api);
                int from = 0;
                for (int to = 0; to < capacity; ++to) {
                    for (; from < oldInv.Count && oldInv[from].Empty; ++from);
                    if (from < oldInv.Count) {
                        inventory[to].Itemstack = oldInv[from].Itemstack;
                        inventory.DidModifyItemSlot(inventory[to]);
                        ++from;
                    }
                }
            }
            base.Initialize(api);

            if (api.Side == EnumAppSide.Server) {
                // Update from old save format to new one
                int eggsWithBlock = -1;
                if (Block.Code.Path.EndsWith("empty"))
                {
                    eggsWithBlock = 0;
                }
                else if (Block.Code.Path.EndsWith("1egg"))
                {
                    eggsWithBlock = 1;
                }
                else if (Block.Code.Path.EndsWith("eggs"))
                {
                    int eggs = Block.LastCodePart()[0];
                    eggsWithBlock = eggs <= '9' && eggs >= '0' ? eggs - '0' : 0;
                }
                if (eggsWithBlock >= 0)
                {
                    Block emptyNest = api.World.GetBlock(new AssetLocation(Block.FirstCodePart()));
                    api.World.BlockAccessor.ExchangeBlock(emptyNest.Id, this.Pos);
                    MarkDirty();
                }
                for (int i = 0; i < eggsWithBlock; ++i)
                {
                    inventory[i].Itemstack ??= new ItemStack(api.World.GetItem("egg-chicken-raw"));
                    inventory.DidModifyItemSlot(inventory[i]);
                }


                IsOccupiedClientside = false;
                api.ModLoader.GetModSystem<POIRegistry>().AddPOI(this);
                RegisterGameTickListener(On1500msTick, 1500);
            }
        }

        protected void CreateInventory(int capacity, ICoreAPI api)
        {
            inventory = new InventoryGeneric(capacity, InventoryClassName, Pos?.ToString(), api);
            inventory.Pos = this.Pos;
            inventory.SlotModified += OnSlotModified;
        }

        protected virtual void OnSlotModified(int slot)
        {
            MarkDirty();
        }


        public override void OnBlockRemoved()
        {
            base.OnBlockRemoved();

            if (Api.Side == EnumAppSide.Server)
            {
                Api.ModLoader.GetModSystem<POIRegistry>().RemovePOI(this);
            }
        }

        public override void OnBlockUnloaded()
        {
            base.OnBlockUnloaded();

            if (Api?.Side == EnumAppSide.Server)
            {
                Api.ModLoader.GetModSystem<POIRegistry>().RemovePOI(this);
            }
        }


        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            tree.SetDouble("inc", timeToIncubate);
            tree.SetDouble("occ", occupiedTimeLast);
            tree.SetBool("isOccupied", occupier != null && occupier.Alive);
        }


        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            TreeAttribute invTree = (TreeAttribute) tree["inventory"];
            if (inventory == null)
            {
                int capacity = invTree?.GetInt("qslots") ?? Capacity;
                CreateInventory(capacity, worldForResolving.Api);
            }
            base.FromTreeAttributes(tree, worldForResolving);

            timeToIncubate = tree.GetDouble("inc");
            occupiedTimeLast = tree.GetDouble("occ");
            for (int i = 0; i < 10; i++)
            {
                string chickCode = tree.GetString("chick" + i);
                if (chickCode != null)
                {
                    int generation = tree.GetInt("gen" + i);
                    inventory[i].Itemstack = new ItemStack(worldForResolving.GetItem("egg-chicken-raw"));
                    TreeAttribute chickTree = new TreeAttribute();
                    chickTree.SetString("code", chickCode);
                    chickTree.SetInt("generation", generation);
                    inventory[i].Itemstack.Attributes["chick"] = chickTree;
                    inventory.DidModifyItemSlot(inventory[i]);
                }
            }
            IsOccupiedClientside = tree.GetBool("isOccupied");
            RedrawAfterReceivingTreeAttributes(worldForResolving);
        }

        public virtual bool CanPlayerPlaceItem(ItemStack itemstack)
        {
            // Included for moddability, testibility, and because another comment indicates that letting players move eggs around is a desired future feature.
            // Note that if players are given the ability to place eggs inside, the perish rate should be made non-zero with accompanying adjustments.
            return false;
        }

        public bool OnInteract(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel) 
        {
            if (!world.Claims.TryAccess(byPlayer, blockSel.Position, EnumBlockAccessFlags.Use))
            {
                return false;
            }

            ItemSlot slot = byPlayer.InventoryManager.ActiveHotbarSlot;
            if (CanPlayerPlaceItem(slot.Itemstack))
            {
                // Place egg in nest
                for (int i = 0; i < inventory.Count; ++i)
                {
                    if (inventory[i].Empty)
                    {
                        AssetLocation sound = slot.Itemstack?.Block?.Sounds?.Place;
                        AssetLocation itemPlaced = slot.Itemstack?.Collectible?.Code;
                        ItemStackMoveOperation op = new ItemStackMoveOperation(Api.World, EnumMouseButton.Left, 0, EnumMergePriority.AutoMerge, 1);
                        if (slot.TryPutInto(inventory[i], ref op) > 0)
                        {
                            Api.Logger.Audit(byPlayer.PlayerName + " put 1x" + itemPlaced + " into " + Block.Code + " at " + Pos);
                            Api.World.PlaySoundAt(sound != null ? sound : new AssetLocation("sounds/player/build"), byPlayer.Entity, byPlayer, true, 16);
                            return true;
                        }
                    }
                }
                return false;
            }

            // Try to take all eggs from nest
            bool anyEggs = false;
            for (int i = 0; i < inventory.Count; ++i)
            {
                if (!inventory[i].Empty)
                {
                    string audit = inventory[i].Itemstack.Collectible?.Code;
                    int quantity = inventory[i].Itemstack.StackSize;

                    bool gave = byPlayer.InventoryManager.TryGiveItemstack(inventory[i].Itemstack);
                    int taken = quantity - (inventory[i].Itemstack?.StackSize ?? 0);
                    if (gave)
                    {
                        if (inventory[i].Itemstack != null && quantity == inventory[i].Itemstack.StackSize)
                        {
                            ItemStack stack = inventory[i].TakeOutWhole();
                            taken = quantity;
                        }

                        anyEggs = true;
                        world.Api.Logger.Audit(byPlayer.PlayerName + " took " + taken + "x " + audit + " from " + Block.Code + " at " + Pos);
                        inventory.DidModifyItemSlot(inventory[i]);
                    }
                    if (inventory[i].Itemstack != null && inventory[i].Itemstack.StackSize == 0)
                    {
                        // This can happen even if TryGiveItemstack returned false, if the player is in creative mode
                        if (!gave)
                        {
                            world.Api.Logger.Audit(byPlayer.PlayerName + " voided " + taken + "x " + audit + " from " + Block.Code + " at " + Pos);
                        }
                        // Otherwise eggs with stack size 0 will still be displayed and still occupy a slot
                        inventory[i].Itemstack = null;
                        inventory.DidModifyItemSlot(inventory[i]);
                    }
                    // If it doesn't fit, leave it in the nest
                }
            }
            if (anyEggs)
            {
                world.PlaySoundAt(new AssetLocation("sounds/player/collect"), blockSel.Position.X, blockSel.Position.Y, blockSel.Position.Z, byPlayer);
            }
            return anyEggs;
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            int eggCount = 0;
            int fertileCount = 0;
            for (int i = 0; i < inventory.Count; ++i)
            {
                if (!inventory[i].Empty)
                {
                    ++eggCount;
                    TreeAttribute chickData = (TreeAttribute) inventory[i].Itemstack.Attributes["chick"];
                    if (chickData?.GetString("code") != null)
                    {
                        ++fertileCount;
                    }
                }
            }

            if (fertileCount > 0)
            {
                if (fertileCount > 1)
                    dsc.AppendLine(Lang.Get("{0} fertile eggs", fertileCount));
                else
                    dsc.AppendLine(Lang.Get("1 fertile egg"));

                if (timeToIncubate >= 1.5)
                    dsc.AppendLine(Lang.Get("Incubation time remaining: {0:0} days", timeToIncubate));
                else if (timeToIncubate >= 0.75)
                    dsc.AppendLine(Lang.Get("Incubation time remaining: 1 day"));
                else if (timeToIncubate > 0)
                    dsc.AppendLine(Lang.Get("Incubation time remaining: {0:0} hours", timeToIncubate * 24));

                if (!IsOccupiedClientside && eggCount >= inventory.Count)
                    dsc.AppendLine(Lang.Get("A broody hen is needed!"));
            }
            else if (eggCount > 0)
            {
                dsc.AppendLine(Lang.Get("No eggs are fertilized"));
            }
        }

        protected override float[][] genTransformationMatrices()
        {
            ModelTransform[] transforms = Block.Attributes?["displayTransforms"]?.AsArray<ModelTransform>();
            if (transforms == null)
            {
                capi.Logger.Warning("No display transforms found for " + Block.Code + ", placed items may be invisible or in the wrong location.");
                transforms = new ModelTransform[DisplayedItems];
                for (int i = 0; i < transforms.Length; ++i)
                {
                    transforms[i] = new ModelTransform();
                }
            }
            if (transforms.Length != DisplayedItems)
            {
                capi.Logger.Warning("Display transforms for " + Block.Code + " block entity do not match number of displayed items, later placed items may be invisible or in the wrong location. Items: " + DisplayedItems + ", transforms: " + transforms.Length);
            }

            float[][] tfMatrices = new float[transforms.Length][];
            for (int i = 0; i < transforms.Length; ++i)
            {
                FastVec3f off = transforms[i].Translation;
                FastVec3f rot = transforms[i].Rotation;
                tfMatrices[i] = new Matrixf()
                    .Translate(off.X, off.Y, off.Z)
                    .Translate(0.5f, 0, 0.5f)
                    .RotateX(rot.X * GameMath.DEG2RAD)
                    .RotateY(rot.Y * GameMath.DEG2RAD)
                    .RotateZ(rot.Z * GameMath.DEG2RAD)
                    .Translate(-0.5f, 0, -0.5f)
                    .Values;
            }
            return tfMatrices;
        }
    }
}
