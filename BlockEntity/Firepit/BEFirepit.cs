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
        internal InventorySmelting inventory;

        // Temperature before the half second tick
        public float prevFurnaceTemperature = 20;
        private float cachedTemperature = 20;
        private double lastTempUpdate;

        private float GetInterpolatedTemperature()
        {
            double now = Api.World.Calendar.TotalHours;
            if (now - lastTempUpdate > 0.01) { cachedTemperature = furnaceTemperature; lastTempUpdate = now; return furnaceTemperature; }
            float t = (float)((now - lastTempUpdate) / 0.01);
            return cachedTemperature + (furnaceTemperature - cachedTemperature) * t;
        }

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
            inventory.SlotModified += OnSlotModifid;
        }



        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            inventory.pos = Pos;
            inventory.LateInitialize("smelting-" + Pos.X + "/" + Pos.Y + "/" + Pos.Z, api);
            
            RegisterGameTickListener(OnBurnTick, 100);
            RegisterGameTickListener(On500msTick, 500);

            if (api is ICoreClientAPI)
            {
                renderer = new FirepitContentsRenderer(api as ICoreClientAPI, Pos);
                (api as ICoreClientAPI).Event.RegisterRenderer(renderer, EnumRenderStage.Opaque, "firepit");

                UpdateRenderer();
            }
        }



        private void OnSlotModifid(int slotid)
        {
            Block = Api.World.BlockAccessor.GetBlock(Pos);

            UpdateRenderer();
            MarkDirty(Api.Side == EnumAppSide.Server); // Save useless triple-remesh by only letting the server decide when to redraw
            shouldRedraw = true;

            if (Api is ICoreClientAPI && clientDialog != null)
            {
                SetDialogValues(clientDialog.Attributes);
            }

            Api.World.BlockAccessor.GetChunkAtBlockPos(Pos)?.MarkModified();
        }


        public bool IsSmoldering => canIgniteFuel;

        public bool IsBurning
        {
            get { return this.fuelBurnTime > 0; }
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
            if (Block.Code.Path.Contains("construct")) return;

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

            // Too cold to ignite fuel after 2 hours
            if (!IsBurning && Block.Variant["burnstate"] == "extinct" && Api.World.Calendar.TotalHours - extinguishedTotalHours > 2)
            {
                canIgniteFuel = false;
                setBlockState("cold");
            }

            // Furnace is burning: Heat furnace
            if (IsBurning)
            {
                furnaceTemperature = changeTemperature(furnaceTemperature, maxTemperature, dt);
            }

            // Ore follows furnace temperature
            if (canHeatInput())
            {
                heatInput(dt);
            } else
            {
                inputStackCookingTime = 0;
            }

            if (canHeatOutput())
            {
                heatOutput(dt);
            }


            // Finished smelting? Turn to smelted item
            if (canSmeltInput() && inputStackCookingTime > maxCookingTime())
            {
                smeltItems();
            }


            // Furnace is not burning and can burn: Ignite the fuel
            if (!IsBurning && canIgniteFuel && canSmelt())
            {
                igniteFuel();
            }


            // Furnace is not burning: Cool down furnace and ore also turn of fire
            if (!IsBurning)
            {
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
            if (diff < 0.5f) return toTemp;

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

            bool smeltableInput = canHeatInput();

            return
                    (BurnsAllFuell || smeltableInput)
                    // Require fuel
                    && fuelCopts.BurnTemperature * HeatModifier > 0
            ;
        }



        public void heatInput(float dt)
        {
            float oldTemp = InputStackTemp;
            float nowTemp = oldTemp;
            float meltingPoint = inputSlot.Itemstack.Collectible.GetMeltingPoint(Api.World, inventory, inputSlot);

            // Only Heat ore. Cooling happens already in the itemstack
            if (oldTemp < furnaceTemperature)
            {
                float f = (1 + GameMath.Clamp((furnaceTemperature - oldTemp) / 30, 0, 1.6f)) * dt;
                if (nowTemp >= meltingPoint) f /= 11;

                float newTemp = changeTemperature(oldTemp, furnaceTemperature, f);
                int maxTemp = Math.Max(inputStack.Collectible.CombustibleProps == null ? 0 : inputStack.Collectible.CombustibleProps.MaxTemperature, inputStack.ItemAttributes?["maxTemperature"] == null ? 0 : inputStack.ItemAttributes["maxTemperature"].AsInt(0));
                if (maxTemp > 0)
                {
                    newTemp = Math.Min(maxTemp, newTemp);
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
                int maxTemp = Math.Max(outputStack.Collectible.CombustibleProps == null ? 0 : outputStack.Collectible.CombustibleProps.MaxTemperature, outputStack.ItemAttributes?["maxTemperature"] == null ? 0 : outputStack.ItemAttributes["maxTemperature"].AsInt(0));
                if (maxTemp > 0)
                {
                    newTemp = Math.Min(maxTemp, newTemp);
                }

                if (oldTemp != newTemp)
                {
                    OutputStackTemp = newTemp;
                }
            }
        }

        public void CoolNow(float amountRel)
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

            if (inventory.CookingSlots.Length > 0)
            {
                bool haveStack = false;
                float lowestTemp = 0;
                for (int i = 0; i < inventory.CookingSlots.Length; i++)
                {
                    ItemStack cookingStack = inventory.CookingSlots[i].Itemstack;
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
            if (inventory.CookingSlots.Length > 0)
            {
                for (int i = 0; i < inventory.CookingSlots.Length; i++)
                {
                    inventory.CookingSlots[i].Itemstack?.Collectible.SetTemperature(Api.World, inventory.CookingSlots[i].Itemstack, value);
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
            CombustibleProperties fuelCopts = stack.Collectible.CombustibleProps;

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



        public bool canHeatInput()
        {
            return
                canSmeltInput() || (inputStack?.ItemAttributes?["allowHeating"] != null && inputStack.ItemAttributes["allowHeating"].AsBool())
            ;
        }

        public bool canHeatOutput()
        {
            return
                outputStack?.ItemAttributes?["allowHeating"] != null && outputStack.ItemAttributes["allowHeating"].AsBool();
            ;
        }

        public bool canSmeltInput()
        {
            if (inputStack == null) return false;

            if (inputStack.Collectible.OnSmeltAttempt(inventory)) MarkDirty(true);

            return
                inputStack.Collectible.CanSmelt(Api.World, inventory, inputSlot.Itemstack, outputSlot.Itemstack)
                && (inputStack.Collectible.CombustibleProps == null || !inputStack.Collectible.CombustibleProps.RequiresContainer)
            ;
        }


        public void smeltItems()
        {
            inputStack.Collectible.DoSmelt(Api.World, inventory, inputSlot, outputSlot);
            InputStackTemp = enviromentTemperature();
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




        public override void OnReceivedClientPacket(IPlayer player, int packetid, byte[] data)
        {
            base.OnReceivedClientPacket(player, packetid, data);
        }

        public override void OnReceivedServerPacket(int packetid, byte[] data)
        {
            if (packetid == (int)EnumBlockEntityPacketId.Close)
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
            }


            furnaceTemperature = tree.GetFloat("furnaceTemperature");
            maxTemperature = tree.GetInt("maxTemperature");
            inputStackCookingTime = tree.GetFloat("oreCookingTime");
            fuelBurnTime = tree.GetFloat("fuelBurnTime");
            maxFuelBurnTime = tree.GetFloat("maxFuelBurnTime");
            extinguishedTotalHours = tree.GetDouble("extinguishedTotalHours");
            canIgniteFuel = tree.GetBool("canIgniteFuel", true);
            cachedFuel = tree.GetFloat("cachedFuel", 0);

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

            ItemStack contentStack = inputStack == null ? outputStack : inputStack;

            bool useOldRenderer =
                renderer.ContentStack != null &&
                renderer.contentStackRenderer != null &&
                contentStack?.Collectible is IInFirepitRendererSupplier &&
                renderer.ContentStack.Equals(Api.World, contentStack, GlobalConstants.IgnoredStackAttributes)
            ;

            if (useOldRenderer) return; // Otherwise the cooking sounds restarts all the time

            renderer.contentStackRenderer?.Dispose();
            renderer.contentStackRenderer = null;

            if (contentStack?.Collectible is IInFirepitRendererSupplier)
            {
                IInFirepitRenderer childrenderer = (contentStack?.Collectible as IInFirepitRendererSupplier).GetRendererWhenInFirepit(contentStack, this, contentStack == outputStack);
                if (childrenderer != null)
                {
                    renderer.SetChildRenderer(contentStack, childrenderer);
                    return;
                }
            }

            InFirePitProps props = GetRenderProps(contentStack);
            if (contentStack?.Collectible != null && !(contentStack?.Collectible is IInFirepitMeshSupplier) && props != null)
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
            get { return inventory[0]; }
        }

        public ItemSlot inputSlot
        {
            get { return inventory[1]; }
        }

        public ItemSlot outputSlot
        {
            get { return inventory[2]; }
        }

        public ItemSlot[] otherCookingSlots
        {
            get { return inventory.CookingSlots; }
        }

        public ItemStack fuelStack
        {
            get { return inventory[0].Itemstack; }
            set { inventory[0].Itemstack = value; inventory[0].MarkDirty(); }
        }

        public ItemStack inputStack
        {
            get { return inventory[1].Itemstack; }
            set { inventory[1].Itemstack = value; inventory[1].MarkDirty(); }
        }

        public ItemStack outputStack
        {
            get { return inventory[2].Itemstack; }
            set { inventory[2].Itemstack = value; inventory[2].MarkDirty(); }
        }


        public CombustibleProperties fuelCombustibleOpts
        {
            get { return getCombustibleOpts(0); }
        }

        public CombustibleProperties getCombustibleOpts(int slotid)
        {
            ItemSlot slot = inventory[slotid];
            if (slot.Itemstack == null) return null;
            return slot.Itemstack.Collectible.CombustibleProps;
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

        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tesselator)
        {
            if (Block == null || Block.Code.Path.Contains("construct")) return false;

            
            ItemStack contentStack = inputStack == null ? outputStack : inputStack;
            MeshData contentmesh = getContentMesh(contentStack, tesselator);
            if (contentmesh != null)
            {
                mesher.AddMeshData(contentmesh);
            }

            string burnState = Block.Variant["burnstate"];
            string contentState = CurrentModel.ToString().ToLowerInvariant();
            if (burnState == "cold" && fuelSlot.Empty) burnState = "extinct";
            if (burnState == null) return true;

            mesher.AddMeshData(getOrCreateMesh(burnState, contentState));

            return true;
        }

        private MeshData getContentMesh(ItemStack contentStack, ITesselatorAPI tesselator)
        {
            CurrentModel = EnumFirepitModel.Normal;

            if (contentStack == null) return null;

            if (contentStack.Collectible is IInFirepitMeshSupplier)
            {
                EnumFirepitModel model = EnumFirepitModel.Normal;
                MeshData mesh = (contentStack.Collectible as IInFirepitMeshSupplier).GetMeshWhenInFirepit(contentStack, Api.World, Pos, ref model);
                this.CurrentModel = model;

                if (mesh != null)
                {
                    return mesh;
                }

            }
            
            if (contentStack.Collectible is IInFirepitRendererSupplier)
            {
                EnumFirepitModel model = (contentStack.Collectible as IInFirepitRendererSupplier).GetDesiredFirepitModel(contentStack, this, contentStack == outputStack);
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

        InFirePitProps GetRenderProps(ItemStack contentStack)
        {
            if (contentStack?.ItemAttributes?.KeyExists("inFirePitProps") == true)
            {
                InFirePitProps props = contentStack.ItemAttributes["inFirePitProps"].AsObject<InFirePitProps>();
                props.Transform.EnsureDefaultValues();

                return props;
            }
            return null;
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