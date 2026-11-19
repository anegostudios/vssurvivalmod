using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

#nullable disable

namespace Vintagestory.GameContent
{
    public class BlockEntityFirepit : BlockEntityOpenableContainer, IHeatSource, IFirePit, ITemperatureSensitive
    {
        protected const int FuelId = 0;
        protected const int InputId = 1;
        protected const int OutputId = 2;

        internal InventorySmelting inventory;

        // Temperature before the half second tick
        public float prevFurnaceTemperature = 20;
        // flag to determine whether static or dynamic rendering is used
        private bool prevForceStatic = false;
        // Current temperature of the furnace
        public float furnaceTemperature = 20;
        // Current temperature of the ore (Degree Celsius * deg
        //public float oreTemperature = 20;
        // Maximum temperature that can be reached with the currently used fuel
        public int maxTemperature;
        // For how long the ore has been cooking
        public float inputStackCookingTime;
        // How much of the current fuel is consumed
        public float fuelBurnTime;
        // How much fuel is available
        public float maxFuelBurnTime;
        // How much smoke the current fuel burns?
        public float smokeLevel;
        /// <summary>
        /// If true, then the fire pit is currently hot enough to ignite fuel
        /// </summary>
        public bool canIgniteFuel;

        public float cachedFuel;

        public double extinguishedTotalHours;


        GuiDialogBlockEntityFirepit clientDialog;
        bool clientSidePrevBurning;

        FirepitContentsRenderer renderer;

        bool shouldRedraw;

        public bool IsHot => IsBurning;
        public float emptyFirepitBurnTimeMulBonus = 4f;

        byte tickAccumulator;
        bool isExtinct;
        bool CanHeatInput;
        bool CanHeatOutput;
        int inputMaxTemp;
        int outputMaxTemp;

        #region Config

        public virtual bool BurnsAllFuell
        {
            get { return true; }
        }
        public virtual float HeatModifier
        {
            get { return 1f; }
        }
        public virtual float BurnDurationModifier
        {
            get { return 1f; }
        }


        // Resting temperature
        public virtual int enviromentTemperature()
        {
            return 20;
        }

        // seconds it requires to melt the ore once beyond melting point
        public virtual float maxCookingTime()
        {
            return inputSlot.Itemstack == null ? 30f : inputSlot.Itemstack.Collectible.GetMeltingDuration(Api.World, inventory, inputSlot);
        }

        public override string InventoryClassName
        {
            get { return "stove"; }
        }

        public virtual string DialogTitle
        {
            get { return Lang.Get("Firepit"); }
        }

        public override InventoryBase Inventory
        {
            get { return inventory; }
        }

        #endregion


        public BlockEntityFirepit()
        {
            inventory = new InventorySmelting(null, null);
            inventory.SlotModified += OnSlotModified;
        }


        ICoreClientAPI capi;
        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            inventory.pos = Pos;
            inventory.LateInitialize("smelting-" + Pos.X + "/" + Pos.Y + "/" + Pos.Z, api);

            if (!Block.Code.Path.Contains("construct"))   // Firepit under contruction does not require to be ticked; when construction is finished the block will be exchanged
            {
                OnConstructionCompleted();
                UpdateExtinctStatus();
            }

            if (api is ICoreClientAPI capi)
            {
                this.capi = capi;
                renderer = new FirepitContentsRenderer(capi, Pos);
                capi.Event.RegisterRenderer(renderer, EnumRenderStage.Opaque, "firepit");
                capi.Event.RegisterEventBusListener(OnSetTransform, 0.5, "onsettransform");
                UpdateRenderer();
            }
        }

        public override void OnExchanged(Block block)
        {
            base.OnExchanged(block);
            UpdateExtinctStatus();
        }

        public void UpdateExtinctStatus()
        {
            isExtinct = Block.Variant["burnstate"] == "extinct";
        }

        public void OnConstructionCompleted()
        {
            RegisterGameTickListener(On100msTick, 100);
        }

        protected void OnSetTransform(string eventName, ref EnumHandling handling, IAttribute data)
        {
            var target = (data as TreeAttribute).GetString("target");

            if (target != "infirepitTransform") return;

            if (Pos.DistanceTo(capi.World.Player.Entity.Pos.XYZ) > 20) return; // Just for nearby shelves to reduce chunk redraws

            var hslot = capi.World.Player.InventoryManager.ActiveHotbarSlot;
            var collObj = hslot.Itemstack.Collectible;

            var tf = ModelTransform.CreateFromTreeAttribute(data as TreeAttribute);
            if (collObj?.Attributes?.KeyExists("inFirePitProps") != true)
            {
                if (collObj.Attributes == null) collObj.Attributes = new JsonObject(new JObject());
                collObj.Attributes.Token["inFirePitProps"]["transform"] = JToken.FromObject(tf);
            }

            renderer.Transform = tf;
        }


        private void OnSlotModified(int slotid)
        {
            Block = Api.World.BlockAccessor.GetBlock(Pos);

            UpdateCachedStackAttributes(slotid);
            UpdateRenderer();
            MarkDirty(Api.Side == EnumAppSide.Server); // Save useless triple-remesh by only letting the server decide when to redraw
            shouldRedraw = true;

            if (Api is ICoreClientAPI && clientDialog != null)
            {
                SetDialogValues(clientDialog.Attributes);
            }

            Api.World.BlockAccessor.GetChunkAtBlockPos(Pos)?.MarkModified();
        }

        private void UpdateCachedStackAttributes(int slotid)
        {
            // We cache certain properties of the input and output itemstacks, for performance - otherwise all these have to be read every tick
            if (slotid == InputId)
            {
                var stack = inputStack;
                JsonObject allowHeating = stack?.ItemAttributes?["allowHeating"];
                CanHeatInput = allowHeating != null && allowHeating.AsBool();

                if (stack != null)
                {
                    CombustibleProperties combustibleProps = stack.Collectible.GetCombustibleProperties(Api.World, stack, null);
                    var maxTemperatureAttribute = stack.ItemAttributes?["maxTemperature"];
                    inputMaxTemp = Math.Max(combustibleProps == null ? 0 : combustibleProps.MaxTemperature, maxTemperatureAttribute == null ? 0 : maxTemperatureAttribute.AsInt(0));
                }
            }
            else if (slotid == OutputId)
            {
                var stack = outputStack;
                JsonObject allowHeating = stack?.ItemAttributes?["allowHeating"];
                CanHeatOutput = allowHeating != null && allowHeating.AsBool();

                if (stack != null)
                {
                    CombustibleProperties combustibleProps = stack.Collectible.GetCombustibleProperties(Api.World, stack, null);
                    var maxTemperatureAttribute = stack.ItemAttributes?["maxTemperature"];
                    outputMaxTemp = Math.Max(combustibleProps == null ? 0 : combustibleProps.MaxTemperature, maxTemperatureAttribute == null ? 0 : maxTemperatureAttribute.AsInt(0));
                }
            }
        }

        public bool IsSmoldering => canIgniteFuel;

        public bool IsBurning
        {
            get { return this.fuelBurnTime > 0; }
        }


        private void On100msTick(float dt)
        {
            OnBurnTick(dt);
            if (++tickAccumulator >= 5)
            {
                tickAccumulator = 0;
                On500msTick(dt);
            }
        }


        // Sync to client every 500ms
        private void On500msTick(float dt)
        {
            if (Api is ICoreServerAPI && (IsBurning || prevFurnaceTemperature != furnaceTemperature))
            {
                MarkDirty();
            }

            prevFurnaceTemperature = furnaceTemperature;
        }




        private void OnBurnTick(float dt)
        {
            // Only tick on the server and merely sync to client
            if (Api is ICoreClientAPI)
            {
                renderer?.contentStackRenderer?.OnUpdate(InputStackTemp);
                return;
            }

            // Use up fuel
            if (fuelBurnTime > 0)
            {
                bool lowFuelConsumption = Math.Abs(furnaceTemperature - maxTemperature) < 50 && inputSlot.Empty;

                fuelBurnTime -= dt / (lowFuelConsumption ? emptyFirepitBurnTimeMulBonus : 1);

                if (fuelBurnTime <= 0)
                {
                    fuelBurnTime = 0;
                    maxFuelBurnTime = 0;
                    if (!canSmelt()) // This check avoids light flicker when a piece of fuel is consumed and more is available
                    {
                        setBlockState("extinct");
                        extinguishedTotalHours = Api.World.Calendar.TotalHours;
                    }
                }
            }

            // Furnace is burning: Heat furnace
            if (IsBurning)
            {
                furnaceTemperature = changeTemperature(furnaceTemperature, maxTemperature, dt);
            }
            else
            {
                // Too cold to ignite fuel after 2 hours
                if (isExtinct && Api.World.Calendar.TotalHours - extinguishedTotalHours > 2)
                {
                    canIgniteFuel = false;
                    setBlockState("cold");
                }
            }

            bool canSmeltInput = this.canSmeltInput();

            // Ore follows furnace temperature
            if (CanHeatInput || canSmeltInput)
            {
                heatInput(dt);
            }
            else
            {
                inputStackCookingTime = 0;
            }

            if (CanHeatOutput)
            {
                heatOutput(dt);
            }


            // Finished smelting? Turn to smelted item
            if (canSmeltInput && inputStackCookingTime > maxCookingTime())
            {
                smeltItems();
            }


            // Furnace is not burning: Cool down furnace and ore also turn of fire
            if (!IsBurning)
            {
                // Furnace is not burning and can burn: Ignite the fuel
                if (canIgniteFuel && canSmelt())
                {
                    igniteFuel();
                }

                furnaceTemperature = changeTemperature(furnaceTemperature, enviromentTemperature(), dt);
            }

        }


        public EnumIgniteState GetIgnitableState(float secondsIgniting)
        {
            if (fuelSlot.Empty) return EnumIgniteState.NotIgnitablePreventDefault;
            if (IsBurning) return EnumIgniteState.NotIgnitablePreventDefault;

            return secondsIgniting > 3 ? EnumIgniteState.IgniteNow : EnumIgniteState.Ignitable;
        }




        public float changeTemperature(float fromTemp, float toTemp, float dt)
        {
            float diff = Math.Abs(fromTemp - toTemp);

            dt = dt + dt * (diff / 28);


            if (diff < dt)
            {
                return toTemp;
            }

            if (fromTemp > toTemp)
            {
                dt = -dt;
            }

            if (Math.Abs(fromTemp - toTemp) < 1)
            {
                return toTemp;
            }

            return fromTemp + dt;
        }







        private bool canSmelt()
        {
            CombustibleProperties fuelCopts = fuelCombustibleOpts;
            if (fuelCopts == null) return false;

            return
                    (BurnsAllFuell || CanHeatInput)
                    // Require fuel
                    && fuelCopts.BurnTemperature * HeatModifier > 0
            ;
        }



        public void heatInput(float dt)
        {
            float oldTemp = InputStackTemp;
            float nowTemp = oldTemp;
            float meltingPoint = inputSlot.Itemstack.Collectible.GetMeltingPoint(Api.World, inventory, inputSlot);
            float stackSize = inputSlot.Itemstack.StackSize;

            // Only Heat ore. Cooling happens already in the itemstack
            if (oldTemp < furnaceTemperature)
            {
                float f = (1 + GameMath.Clamp((furnaceTemperature - oldTemp) / 30, 0, 1.6f)) * dt;
                if (nowTemp >= meltingPoint) f /= 11; // Technically inconsistent - slows down heating of all items when only one is being smelted. But the overall effect is good.

                float newTemp = changeTemperature(oldTemp, furnaceTemperature, f);
                newTemp = (newTemp + (stackSize - 1) * oldTemp) / stackSize;
                if (inputMaxTemp > 0)
                {
                    newTemp = Math.Min(inputMaxTemp, newTemp);
                }

                if (oldTemp != newTemp)
                {
                    InputStackTemp = newTemp;
                    nowTemp = newTemp;
                }
            }

            // Begin smelting when hot enough
            if (nowTemp >= meltingPoint)
            {
                float diff = nowTemp / meltingPoint;
                inputStackCookingTime += GameMath.Clamp((int)(diff), 1, 30) * dt;
            }
            else
            {
                if (inputStackCookingTime > 0) inputStackCookingTime--;
            }
        }



        public void heatOutput(float dt)
        {
            float oldTemp = OutputStackTemp;

            // Only Heat ore. Cooling happens already in the itemstack
            if (oldTemp < furnaceTemperature)
            {
                float newTemp = changeTemperature(oldTemp, furnaceTemperature, 2 * dt);
                if (outputMaxTemp > 0)
                {
                    newTemp = Math.Min(outputMaxTemp, newTemp);
                }

                if (oldTemp != newTemp)
                {
                    OutputStackTemp = newTemp;
                }
            }
        }

        public void CoolNow(float amountRel, OnStackToCool onStackToCoolCallback)
        {
            Api.World.PlaySoundAt(new AssetLocation("sounds/effect/extinguish"), Pos, -0.5, null, false, 16);

            fuelBurnTime -= (float)amountRel / 10f;

            if (Api.World.Rand.NextDouble() < amountRel / 5f || fuelBurnTime <= 0)
            {
                setBlockState("cold");
                extinguishedTotalHours = -99;
                canIgniteFuel = false;
                fuelBurnTime = 0;
                maxFuelBurnTime = 0;
            }

            MarkDirty(true);
        }




        public float InputStackTemp
        {
            get
            {
                return GetTemp(inputStack);
            }
            set
            {
                SetTemp(inputStack, value);
            }
        }

        public float OutputStackTemp
        {
            get
            {
                return GetTemp(outputStack);
            }
            set
            {
                SetTemp(outputStack, value);
            }
        }


        float GetTemp(ItemStack stack)
        {
            if (stack == null) return enviromentTemperature();

            var cookingSlots = inventory.CookingSlots;
            if (cookingSlots.Length > 0)
            {
                bool haveStack = false;
                float lowestTemp = 0;
                for (int i = 0; i < cookingSlots.Length; i++)
                {
                    ItemStack cookingStack = cookingSlots[i].Itemstack;
                    if (cookingStack != null)
                    {
                        float stackTemp = cookingStack.Collectible.GetTemperature(Api.World, cookingStack);
                        lowestTemp = haveStack ? Math.Min(lowestTemp, stackTemp) : stackTemp;
                        haveStack = true;
                    }

                }

                return lowestTemp;

            }
            else
            {
                return stack.Collectible.GetTemperature(Api.World, stack);
            }
        }

        void SetTemp(ItemStack stack, float value)
        {
            if (stack == null) return;
            var cookingSlots = inventory.CookingSlots;
            if (cookingSlots.Length > 0)
            {
                for (int i = 0; i < cookingSlots.Length; i++)
                {
                    var itemstack = cookingSlots[i].Itemstack;
                    itemstack?.Collectible.SetTemperature(Api.World, itemstack, value);
                }
            }
            else
            {
                stack.Collectible.SetTemperature(Api.World, stack, value);
            }
        }




        public void igniteFuel()
        {
            igniteWithFuel(fuelStack);

            fuelStack.StackSize -= 1;

            if (fuelStack.StackSize <= 0)
            {
                fuelStack = null;
            }
        }



        public void igniteWithFuel(IItemStack stack)
        {
            CombustibleProperties fuelCopts = stack.Collectible.GetCombustibleProperties(Api.World, stack as ItemStack, null);

            maxFuelBurnTime = fuelBurnTime = fuelCopts.BurnDuration * BurnDurationModifier;
            maxTemperature = (int)(fuelCopts.BurnTemperature * HeatModifier);
            smokeLevel = fuelCopts.SmokeLevel;
            setBlockState("lit");
            MarkDirty(true);
        }




        public void setBlockState(string state)
        {
            AssetLocation loc = Block.CodeWithVariant("burnstate", state);
            Block block = Api.World.GetBlock(loc);
            if (block == null) return;

            Api.World.BlockAccessor.ExchangeBlock(block.Id, Pos);
            this.Block = block;
        }

        // Retained for mod compatibility - the casing is wrong for historic reasons
        public bool canHeatInput()
        {
            return CanHeatInput;
        }

        // Retained for mod compatibility - the casing is wrong for historic reasons
        public bool canHeatOutput()
        {
            return CanHeatOutput;
        }

        public bool canSmeltInput()
        {
            CollectibleObject input = inputStack?.Collectible;
            if (input == null) return false;

            if (input.OnSmeltAttempt(inventory)) MarkDirty(true);

            if (!input.CanSmelt(Api.World, inventory, inputSlot.Itemstack, outputSlot.Itemstack)) return false;

            CombustibleProperties combustibleProps = input.GetCombustibleProperties(Api.World, inputStack, null);
            return combustibleProps == null || !combustibleProps.RequiresContainer;
        }


        public void smeltItems()
        {
            inputStack.Collectible.DoSmelt(Api.World, inventory, inputSlot, outputSlot);
            inputStackCookingTime = 0;
            MarkDirty(true);
            inputSlot.MarkDirty();
        }


        #region Events

        public override bool OnPlayerRightClick(IPlayer byPlayer, BlockSelection blockSel)
        {
            if (Api.Side == EnumAppSide.Client)
            {
                toggleInventoryDialogClient(byPlayer, () => {
                    SyncedTreeAttribute dtree = new SyncedTreeAttribute();
                    SetDialogValues(dtree);
                    clientDialog = new GuiDialogBlockEntityFirepit(DialogTitle, Inventory, Pos, dtree, Api as ICoreClientAPI);
                    return clientDialog;
                 });
            }

            return true;
        }




        public override void OnReceivedServerPacket(int packetId, SerializedData packetData)
        {
            if (packetId == (int)EnumBlockEntityPacketId.Close)
            {
                (Api.World as IClientWorldAccessor).Player.InventoryManager.CloseInventory(Inventory);
                invDialog?.TryClose();
                invDialog?.Dispose();
                invDialog = null;
            }
        }




        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);
            //Inventory.FromTreeAttributes(tree.GetTreeAttribute("inventory")); - why twice? its already done in the base method Tyron 5.nov 2024

            if (Api != null)
            {
                Inventory.AfterBlocksLoaded(Api.World);
                UpdateCachedStackAttributes(InputId);
                UpdateCachedStackAttributes(OutputId);
            }

            furnaceTemperature = tree.GetFloat("furnaceTemperature");
            maxTemperature = tree.GetInt("maxTemperature");
            inputStackCookingTime = tree.GetFloat("oreCookingTime");
            fuelBurnTime = tree.GetFloat("fuelBurnTime");
            maxFuelBurnTime = tree.GetFloat("maxFuelBurnTime");
            extinguishedTotalHours = tree.GetDouble("extinguishedTotalHours");
            canIgniteFuel = tree.GetBool("canIgniteFuel", true);
            cachedFuel = tree.GetFloat("cachedFuel", 0);

            UpdateExtinctStatus();

            if (Api?.Side == EnumAppSide.Client)
            {
                UpdateRenderer();

                if (clientDialog != null) SetDialogValues(clientDialog.Attributes);
            }


            if (Api?.Side == EnumAppSide.Client && (clientSidePrevBurning != IsBurning || shouldRedraw))
            {
                GetBehavior<BEBehaviorFirepitAmbient>()?.ToggleAmbientSounds(IsBurning);
                clientSidePrevBurning = IsBurning;
                MarkDirty(true);
                shouldRedraw = false;
            }
        }


        void UpdateRenderer()
        {
            if (renderer == null) return;

            bool forceStatic = ForceStaticMesh();

            // If the mode has changed, we re-tessellate the block
            if (forceStatic != prevForceStatic)
            {
                MarkDirty(true);
                prevForceStatic = forceStatic;
            }

            // if we need static rendering of the pot mesh
            if (forceStatic)
            {
                // Remove the dynamic renderer if there was one
                if (renderer.contentStackRenderer != null)
                {
                    renderer.contentStackRenderer.Dispose();
                    renderer.contentStackRenderer = null;
                }
                renderer.SetContents(null, null);
                return;
            }

            ItemStack contentStack = inputStack ?? outputStack;

            IInFirepitRendererSupplier rendererSupplier = contentStack?.Collectible.GetCollectibleInterface<IInFirepitRendererSupplier>();
            bool useOldRenderer = renderer.ContentStack != null &&
                                  renderer.contentStackRenderer != null &&
                                  rendererSupplier != null &&
                                  renderer.ContentStack.Equals(Api.World, contentStack, GlobalConstants.IgnoredStackAttributes);

            if (useOldRenderer) return; // Otherwise the cooking sounds restarts all the time

            renderer.contentStackRenderer?.Dispose();
            renderer.contentStackRenderer = null;

            if (rendererSupplier != null)
            {
                IInFirepitRenderer childrenderer = rendererSupplier.GetRendererWhenInFirepit(contentStack, this, contentStack == outputStack);
                if (childrenderer != null)
                {
                    renderer.SetChildRenderer(contentStack, childrenderer);
                    return;
                }
            }

            InFirePitProps props = GetRenderProps(contentStack);
            IInFirepitMeshSupplier meshSupplier = contentStack?.Collectible.GetCollectibleInterface<IInFirepitMeshSupplier>();
            if (contentStack?.Collectible != null && meshSupplier == null && props != null)
            {
                renderer.SetContents(contentStack, props.Transform);
            }
            else
            {
                renderer.SetContents(null, null);
            }
        }


        void SetDialogValues(ITreeAttribute dialogTree)
        {
            dialogTree.SetFloat("furnaceTemperature", furnaceTemperature);

            dialogTree.SetInt("maxTemperature", maxTemperature);
            dialogTree.SetFloat("oreCookingTime", inputStackCookingTime);
            dialogTree.SetFloat("maxFuelBurnTime", maxFuelBurnTime);
            dialogTree.SetFloat("fuelBurnTime", fuelBurnTime);

            if (inputSlot.Itemstack != null)
            {
                float meltingDuration = inputSlot.Itemstack.Collectible.GetMeltingDuration(Api.World, inventory, inputSlot);

                dialogTree.SetFloat("oreTemperature", InputStackTemp);
                dialogTree.SetFloat("maxOreCookingTime", meltingDuration);
            }
            else
            {
                dialogTree.RemoveAttribute("oreTemperature");
            }

            dialogTree.SetString("outputText", inventory.GetOutputText());
            dialogTree.SetInt("haveCookingContainer", inventory.HaveCookingContainer ? 1 : 0);
            dialogTree.SetInt("quantityCookingSlots", inventory.CookingSlots.Length);
        }




        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            ITreeAttribute invtree = new TreeAttribute();
            Inventory.ToTreeAttributes(invtree);
            tree["inventory"] = invtree;

            tree.SetFloat("furnaceTemperature", furnaceTemperature);
            tree.SetInt("maxTemperature", maxTemperature);
            tree.SetFloat("oreCookingTime", inputStackCookingTime);
            tree.SetFloat("fuelBurnTime", fuelBurnTime);
            tree.SetFloat("maxFuelBurnTime", maxFuelBurnTime);
            tree.SetDouble("extinguishedTotalHours", extinguishedTotalHours);
            tree.SetBool("canIgniteFuel", canIgniteFuel);
            tree.SetFloat("cachedFuel", cachedFuel);
        }




        public override void OnBlockRemoved()
        {
            base.OnBlockRemoved();

            renderer?.Dispose();
            renderer = null;

            if (clientDialog != null)
            {
                clientDialog.TryClose();
                clientDialog?.Dispose();
                clientDialog = null;
            }
        }



        #endregion

        #region Helper getters


        public ItemSlot fuelSlot
        {
            get { return inventory[FuelId]; }
        }

        public ItemSlot inputSlot
        {
            get { return inventory[InputId]; }
        }

        public ItemSlot outputSlot
        {
            get { return inventory[OutputId]; }
        }

        public ItemSlot[] otherCookingSlots
        {
            get { return inventory.CookingSlots; }
        }

        public ItemStack fuelStack
        {
            get { return inventory[FuelId].Itemstack; }
            set { inventory[FuelId].Itemstack = value; inventory[FuelId].MarkDirty(); }
        }

        public ItemStack inputStack
        {
            get { return inventory[InputId].Itemstack; }
            set { inventory[InputId].Itemstack = value; inventory[InputId].MarkDirty(); }
        }

        public ItemStack outputStack
        {
            get { return inventory[OutputId].Itemstack; }
            set { inventory[OutputId].Itemstack = value; inventory[OutputId].MarkDirty(); }
        }


        public CombustibleProperties fuelCombustibleOpts
        {
            get { return getCombustibleOpts(0); }
        }

        public CombustibleProperties getCombustibleOpts(int slotid)
        {
            ItemSlot slot = inventory[slotid];
            if (slot.Itemstack == null) return null;
            CombustibleProperties combustibleProps = slot.Itemstack.Collectible.GetCombustibleProperties(Api.World, slot.Itemstack, null);
            return combustibleProps;
        }

        #endregion


        public override void OnStoreCollectibleMappings(Dictionary<int, AssetLocation> blockIdMapping, Dictionary<int, AssetLocation> itemIdMapping)
        {
            foreach (var slot in Inventory)
            {
                if (slot.Itemstack == null) continue;

                if (slot.Itemstack.Class == EnumItemClass.Item)
                {
                    itemIdMapping[slot.Itemstack.Item.Id] = slot.Itemstack.Item.Code;
                }
                else
                {
                    blockIdMapping[slot.Itemstack.Block.BlockId] = slot.Itemstack.Block.Code;
                }

                slot.Itemstack.Collectible.OnStoreCollectibleMappings(Api.World, slot, blockIdMapping, itemIdMapping);
            }

            foreach (ItemSlot slot in inventory.CookingSlots)
            {
                if (slot.Itemstack == null) continue;

                if (slot.Itemstack.Class == EnumItemClass.Item)
                {
                    itemIdMapping[slot.Itemstack.Item.Id] = slot.Itemstack.Item.Code;
                }
                else
                {
                    blockIdMapping[slot.Itemstack.Block.BlockId] = slot.Itemstack.Block.Code;
                }

                slot.Itemstack.Collectible.OnStoreCollectibleMappings(Api.World, slot, blockIdMapping, itemIdMapping);
            }
        }

        public override void OnLoadCollectibleMappings(IWorldAccessor worldForResolve, Dictionary<int, AssetLocation> oldBlockIdMapping, Dictionary<int, AssetLocation> oldItemIdMapping, int schematicSeed, bool resolveImports)
        {
            base.OnLoadCollectibleMappings(worldForResolve, oldBlockIdMapping, oldItemIdMapping, schematicSeed, resolveImports);

            // Why is this here? The base method already does this
            /*foreach (ItemSlot slot in inventory.CookingSlots)
            {
                if (slot.Itemstack == null) continue;
                if (!slot.Itemstack.FixMapping(oldBlockIdMapping, oldItemIdMapping, worldForResolve))
                {
                    slot.Itemstack = null;
                }
                else
                {
                    slot.Itemstack.Collectible.OnLoadCollectibleMappings(worldForResolve, slot, oldBlockIdMapping, oldItemIdMapping, resolveImports);
                }
            }*/
        }

        public EnumFirepitModel CurrentModel { get; private set; }

        public bool ForceStaticMesh()
        {
            return inputStack != null && furnaceTemperature < 50 && inputStack?.Block?.Code.Path.Contains("claypot") == true;
        }



        private void AddPotStaticMesh(ITerrainMeshPool mesher, ITesselatorAPI tesselator, ItemStack potStack)
        {
            Block potBlock = potStack.Block;
            if (potBlock == null) return;

            // Unique keys for each pot type
            string potKey = "pot-static-" + potBlock.Code;
            string lidKey = "lid-static-" + potBlock.Code;

            // Get or create a pot mesh
            MeshData potMesh = ObjectCacheUtil.GetOrCreate(Api, potKey, () =>
            {
                tesselator.TesselateShape(potBlock, Shape.TryGet(Api, "shapes/block/clay/pot-opened-empty.json"), out var mesh);
                mesh.Translate(0, 1 / 16f, 0);
                return mesh;
            });
            mesher.AddMeshData(potMesh);

            // Get or create a lid mesh
            MeshData lidMesh = ObjectCacheUtil.GetOrCreate(Api, lidKey, () =>
            {
                tesselator.TesselateShape(potBlock, Shape.TryGet(Api, "shapes/block/clay/pot-part-lid.json"), out var mesh);
                mesh.Translate(0, 6.5f / 16f, 0);
                return mesh;
            });
            mesher.AddMeshData(lidMesh);
        }


        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tesselator)
        {
            if (Block == null || Block.Code.Path.Contains("construct")) return false;


            if (ForceStaticMesh())
            {
                // For the cold pot we use a static mesh with a lid
                AddPotStaticMesh(mesher, tesselator, inputStack);
                CurrentModel = EnumFirepitModel.Wide; // We indicate that there is a pot inside
            }
            else
            {
                ItemStack contentStack = inputStack ?? outputStack;
                // Otherwise, the standard logic for getting the content mesh
                MeshData contentMesh = getContentMesh(contentStack, tesselator);
                if (contentMesh != null) mesher.AddMeshData(contentMesh);
                // getContentMesh sets CurrentModel itself
            }

            // The mesh of the block itself (fire/coals) is added as usual
            string burnState = Block.Variant["burnstate"];
            string contentState = CurrentModel.ToString().ToLowerInvariant();
            if (burnState == "cold" && fuelSlot.Empty) burnState = "extinct";
            if (burnState != null) mesher.AddMeshData(getOrCreateMesh(burnState, contentState));

            return true;
        }

        private MeshData getContentMesh(ItemStack contentStack, ITesselatorAPI tesselator)
        {
            CurrentModel = EnumFirepitModel.Normal;

            if (contentStack == null) return null;

            IInFirepitMeshSupplier meshSupplier = contentStack.Collectible.GetCollectibleInterface<IInFirepitMeshSupplier>();
            if (meshSupplier != null)
            {
                EnumFirepitModel model = EnumFirepitModel.Normal;
                MeshData mesh = meshSupplier.GetMeshWhenInFirepit(contentStack, Api.World, Pos, ref model);
                this.CurrentModel = model;

                if (mesh != null)
                {
                    return mesh;
                }

            }
            
            IInFirepitRendererSupplier rendererSupplier = contentStack.Collectible.GetCollectibleInterface<IInFirepitRendererSupplier>();
            if (rendererSupplier != null)
            {
                EnumFirepitModel model = rendererSupplier.GetDesiredFirepitModel(contentStack, this, contentStack == outputStack);
                this.CurrentModel = model;
                return null;
            }

            InFirePitProps renderProps = GetRenderProps(contentStack);
            
            if (renderProps != null)
            {
                this.CurrentModel = renderProps.UseFirepitModel;

                if (contentStack.Class != EnumItemClass.Item)
                {
                    tesselator.TesselateBlock(contentStack.Block, out MeshData ingredientMesh);

                    ingredientMesh.ModelTransform(renderProps.Transform);

                    // Lower by 1 voxel if extinct
                    if (!IsBurning && renderProps.UseFirepitModel != EnumFirepitModel.Spit) ingredientMesh.Translate(0, -1 / 16f, 0);

                    return ingredientMesh;
                }

                return null;
            }
            else
            {
                if (renderer.RequireSpit)
                {
                    this.CurrentModel = EnumFirepitModel.Spit;
                }
                return null; // Mesh drawing is handled by the FirepitContentsRenderer
            }
            
        }

        public override void OnBlockUnloaded()
        {
            base.OnBlockUnloaded();

            renderer?.Dispose();
        }

        public static InFirePitProps GetRenderProps(ItemStack contentStack)
        {
            InFirePitProps props = contentStack?.ItemAttributes?["inFirePitProps"].AsObject<InFirePitProps>();
            if (props != null)
            {
                props.Transform.EnsureDefaultValues();
            }
            return props;
        }


        public MeshData getOrCreateMesh(string burnstate, string contentstate)
        {
            Dictionary<string, MeshData> Meshes = ObjectCacheUtil.GetOrCreate(Api, "firepit-meshes", () => new Dictionary<string, MeshData>());

            string key = burnstate + "-" + contentstate;
            if (!Meshes.TryGetValue(key, out MeshData meshdata))
            {
                Block block = Api.World.BlockAccessor.GetBlock(Pos);
                if (block.BlockId == 0) return null;

                MeshData[] meshes = new MeshData[17];
                ITesselatorAPI mesher = ((ICoreClientAPI)Api).Tesselator;

                mesher.TesselateShape(block, API.Common.Shape.TryGet(Api, "shapes/block/wood/firepit/" + key + ".json"), out meshdata);
            }

            return meshdata;
        }


        public float GetHeatStrength(IWorldAccessor world, BlockPos heatSourcePos, BlockPos heatReceiverPos)
        {
            return IsBurning ? 10 : (IsSmoldering ? 0.25f : 0);
        }
    }
}

