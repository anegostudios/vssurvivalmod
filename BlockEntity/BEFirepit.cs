using System;
using System.Collections.Generic;
using System.IO;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public enum EnumFirepitModel
    {
        Normal = 0,
        Cooking = 1,
        Wide = 2
    }

    public interface IInFirepitMeshSupplier
    {
        /// <summary>
        /// Return the mesh you want to be rendered in the firepit. You can return null to signify that you do not wish to use a custom mesh.
        /// </summary>
        /// <param name="stack"></param>
        /// <param name="world"></param>
        /// <param name="pos"></param>
        /// <param name="firepitModel"></param>
        /// <returns></returns>
        MeshData GetMeshWhenInFirepit(ItemStack stack, IWorldAccessor world, BlockPos pos, ref EnumFirepitModel firepitModel);
    }

    public class InFirePitProps
    {
        public ModelTransform Transform;
        public EnumFirepitModel UseFirepitModel;
    }

    public interface IInFirepitRenderer : IRenderer
    {
        /// <summary>
        /// Called every 100ms in case you want to do custom stuff, such as playing a sound after a certain temperature
        /// </summary>
        /// <param name="temperature"></param>
        void OnUpdate(float temperature);

        /// <summary>
        /// Called when the itemstack has been moved to the output slot
        /// </summary>
        void OnCookingComplete();
    }

    public interface IInFirepitRendererSupplier
    {
        /// <summary>
        /// Return the renderer that perfroms the rendering of your block/item in the firepit. You can return null to signify that you do not wish to use a custom renderer
        /// </summary>
        /// <param name="stack"></param>
        /// <param name="world"></param>
        /// <param name="pos"></param>
        /// <returns></returns>
        IInFirepitRenderer GetRendererWhenInFirepit(ItemStack stack, BlockEntityFirepit firepit, bool forOutputSlot);

        /// <summary>
        /// The model type the firepit should be using while you render your custom item
        /// </summary>
        /// <param name="stack"></param>
        /// <param name="world"></param>
        /// <param name="pos"></param>
        /// <returns></returns>
        EnumFirepitModel GetDesiredFirepitModel(ItemStack stack, BlockEntityFirepit firepit, bool forOutputSlot);
    }



    public class BlockEntityFirepit : BlockEntityOpenableContainer
    {
        ILoadedSound ambientSound;

        internal InventorySmelting inventory;

        
        // Temperature before the half second tick
        public float prevFurnaceTemperature = 20;

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

        public double extinguishedTotalHours;


        GuiDialogBlockEntityFirepit clientDialog;
        bool clientSidePrevBurning;
        
        FirepitContentsRenderer renderer;


        MeshData[] meshes
        {
            get
            {
                return ObjectCacheUtil.GetOrCreate(Api, "firepit-meshes", () =>
                {
                    Block block = Api.World.BlockAccessor.GetBlock(Pos);
                    if (block.BlockId == 0) return null;

                    MeshData[] meshes = new MeshData[17];
                    ITesselatorAPI mesher = ((ICoreClientAPI)Api).Tesselator;

                    // 0: Extinct
                    // 1: Extinct-cooking
                    // 2: Extinct-small-crucible
                    // 3: Lit
                    // 4: Lit-cooking
                    // 5: Lit-small-crucible
                    meshes = new MeshData[6];
                    mesher.TesselateShape(block, Api.Assets.TryGet("shapes/block/wood/firepit/extinct.json").ToObject<Shape>(), out meshes[0]);
                    mesher.TesselateShape(block, Api.Assets.TryGet("shapes/block/wood/firepit/extinct-cooking.json").ToObject<Shape>(), out meshes[1]);
                    mesher.TesselateShape(block, Api.Assets.TryGet("shapes/block/wood/firepit/extinct-wide.json").ToObject<Shape>(), out meshes[2]);
                    mesher.TesselateShape(block, Api.Assets.TryGet("shapes/block/wood/firepit/lit.json").ToObject<Shape>(), out meshes[3]);
                    mesher.TesselateShape(block, Api.Assets.TryGet("shapes/block/wood/firepit/lit-cooking.json").ToObject<Shape>(), out meshes[4]);
                    mesher.TesselateShape(block, Api.Assets.TryGet("shapes/block/wood/firepit/lit-wide.json").ToObject<Shape>(), out meshes[5]);

                    meshes[0].Tints = null;
                    meshes[1].Tints = null;
                    meshes[2].Tints = null;
                    meshes[3].Tints = null;
                    meshes[4].Tints = null;
                    meshes[5].Tints = null;

                    return meshes;
                });
            }
        }



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

        public virtual float SoundLevel
        {
            get { return 0.66f; }
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
            inventory.LateInitialize("smelting-1", api);
            

            RegisterGameTickListener(OnBurnTick, 100);
            RegisterGameTickListener(OnSyncTick, 500);
            
            if (api is ICoreClientAPI)
            {
                renderer = new FirepitContentsRenderer(api as ICoreClientAPI, Pos);

                (api as ICoreClientAPI).Event.RegisterRenderer(renderer, EnumRenderStage.Opaque);

                UpdateRenderer();
            }
        }


        public void ToggleAmbientSounds(bool on)
        {
            if (Api.Side != EnumAppSide.Client) return;

            if (on)
            {
                ambientSound = ((IClientWorldAccessor)Api.World).LoadSound(new SoundParams()
                {
                    Location = new AssetLocation("sounds/environment/fireplace.ogg"),
                    ShouldLoop = true,
                    Position = Pos.ToVec3f().Add(0.5f, 0.25f, 0.5f),
                    DisposeOnFinish = false,
                    Volume = SoundLevel
                });
                ambientSound.Start();
                
            }
            else
            {
                ambientSound.Stop();
                ambientSound.Dispose();
                ambientSound = null;
            }
            
        }


        private void OnSlotModifid(int slotid)
        {
            Block = Api.World.BlockAccessor.GetBlock(Pos);

            if (slotid == 1 || slotid == 2)
            {
                UpdateRenderer();
                MarkDirty(Api.Side == EnumAppSide.Server); // Save useless triple-remesh by only letting the server decide when to redraw
            }

            if (Api is ICoreClientAPI && clientDialog != null)
            {
                SetDialogValues(clientDialog.Attributes);
            }

            Api.World.BlockAccessor.GetChunkAtBlockPos(Pos)?.MarkModified();
        }
        


        public bool IsBurning
        {
            get { return this.fuelBurnTime > 0; }
        }


        public int getInventoryStackLimit()
        {
            return 64;
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
                fuelBurnTime -= dt;

                if (fuelBurnTime <= 0)
                {
                    fuelBurnTime = 0;
                    maxFuelBurnTime = 0;
                    if (!canSmelt()) // This check avoids light flicker when a piece of fuel is consumed and more is available
                    {
                        setStoveBurning(false);
                        extinguishedTotalHours = Api.World.Calendar.TotalHours;
                    }
                }
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
            if (!IsBurning && canSmelt())
            {
                igniteFuel();
            }


            // Furnace is not burning: Cool down furnace and ore also turn of fire
            if (!IsBurning)
            {
                furnaceTemperature = changeTemperature(furnaceTemperature, enviromentTemperature(), dt);
            }

        }


        // Sync to client every 500ms
        private void OnSyncTick(float dt)
        {
            if (Api is ICoreServerAPI && (IsBurning || prevFurnaceTemperature != furnaceTemperature))
            {
                MarkDirty();
            }

            prevFurnaceTemperature = furnaceTemperature;
        }

        
        public float changeTemperature(float fromTemp, float toTemp, float dt)
        {
            float diff = Math.Abs(fromTemp - toTemp);

            dt = dt + dt * (diff / 35);


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
            //dt *= 20;

            float oldTemp = InputStackTemp;
            float nowTemp = oldTemp;

            // Only Heat ore. Cooling happens already in the itemstack
            if (oldTemp < furnaceTemperature)
            {
                float newTemp = changeTemperature(oldTemp, furnaceTemperature, 2 * dt);
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
            float meltingPoint = inputSlot.Itemstack.Collectible.GetMeltingPoint(Api.World, inventory, inputSlot);
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
            //dt *= 20;

            float oldTemp = OutputStackTemp;
            float nowTemp = oldTemp;

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
                    nowTemp = newTemp;
                }
            }
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
            setStoveBurning(true);
        }


        public void igniteWithFuel(CombustibleProperties fuelCopts, float durationMultiplier)
        {
            maxFuelBurnTime = fuelBurnTime = fuelCopts.BurnDuration * BurnDurationModifier * durationMultiplier;
            maxTemperature = (int)(fuelCopts.BurnTemperature * HeatModifier);
            smokeLevel = fuelCopts.SmokeLevel;
            setStoveBurning(true);
        }


        public void setStoveBurning(bool burning)
        {
            BlockFirepit block = Api.World.BlockAccessor.GetBlock(Pos) as BlockFirepit;
            if (block == null) return;
            
            if (burning)
            {
                if (block.Ignite(Api.World, Pos)) MarkDirty(true);
            } else
            {
                if (block.Extinguish(Api.World, Pos)) MarkDirty(true);
            }
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
            return
                inputStack != null
                && inputStack.Collectible.CanSmelt(Api.World, inventory, inputSlot.Itemstack, outputSlot.Itemstack)
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
            if (Api.World is IServerWorldAccessor)
            {
                byte[] data;

                using (MemoryStream ms = new MemoryStream())
                {
                    BinaryWriter writer = new BinaryWriter(ms);
                    writer.Write("BlockEntityStove");
                    writer.Write(DialogTitle);
                    TreeAttribute tree = new TreeAttribute();
                    inventory.ToTreeAttributes(tree);
                    tree.ToBytes(writer);
                    data = ms.ToArray();
                }

                ((ICoreServerAPI)Api).Network.SendBlockEntityPacket(
                    (IServerPlayer)byPlayer,
                    Pos.X, Pos.Y, Pos.Z,
                    (int)EnumBlockStovePacket.OpenGUI,
                    data
                );

                byPlayer.InventoryManager.OpenInventory(inventory);
            }

            return true;
        }


        public override void FromTreeAtributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAtributes(tree, worldForResolving);
            Inventory.FromTreeAttributes(tree.GetTreeAttribute("inventory"));

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

            if (Api?.Side == EnumAppSide.Client)
            {
                UpdateRenderer();

                if (clientDialog != null) SetDialogValues(clientDialog.Attributes);
            }


            if (Api?.Side == EnumAppSide.Client && clientSidePrevBurning != IsBurning)
            {
                ToggleAmbientSounds(IsBurning);
                clientSidePrevBurning = IsBurning;
                MarkDirty(true);
            }
        }


        void UpdateRenderer()
        {
            if (renderer == null) return;

            ItemStack contentStack = inputStack == null ? outputStack : inputStack;
            ItemStack prevStack = renderer.ContentStack;

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
                IInFirepitRenderer childrenderer = (contentStack.Collectible as IInFirepitRendererSupplier).GetRendererWhenInFirepit(contentStack, this, contentStack == outputStack);
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
        }




        public override void OnBlockRemoved()
        {
            base.OnBlockRemoved();

            if (ambientSound != null)
            {
                ambientSound.Stop();
                ambientSound.Dispose();
            }

            if (renderer != null)
            {
                renderer.Unregister();
                renderer = null;
            }

            if (clientDialog != null)
            {
                clientDialog.TryClose();
                clientDialog?.Dispose();
                clientDialog = null;
            }
        }

        public override void OnBlockBroken()
        {
            base.OnBlockBroken();
        }

        ~BlockEntityFirepit()
        {
            if (ambientSound != null) ambientSound.Dispose();
        }

        public override void OnReceivedClientPacket(IPlayer player, int packetid, byte[] data)
        {
            if (packetid < 1000)
            {
                Inventory.InvNetworkUtil.HandleClientPacket(player, packetid, data);

                // Tell server to save this chunk to disk again
                Api.World.BlockAccessor.GetChunkAtBlockPos(Pos.X, Pos.Y, Pos.Z).MarkModified();

                return;
            }

            if (packetid == (int)EnumBlockStovePacket.CloseGUI)
            {
                if (player.InventoryManager != null)
                {
                    player.InventoryManager.CloseInventory(Inventory);
                }
            }
        }

        public override void OnReceivedServerPacket(int packetid, byte[] data)
        {
            if (packetid == (int)EnumBlockStovePacket.OpenGUI)
            {
                using (MemoryStream ms = new MemoryStream(data))
                {
                    BinaryReader reader = new BinaryReader(ms);

                    string dialogClassName = reader.ReadString();
                    string dialogTitle = reader.ReadString();

                    TreeAttribute tree = new TreeAttribute();
                    tree.FromBytes(reader);
                    Inventory.FromTreeAttributes(tree);
                    Inventory.ResolveBlocksOrItems();

                    IClientWorldAccessor clientWorld = (IClientWorldAccessor)Api.World;

                    SyncedTreeAttribute dtree = new SyncedTreeAttribute();
                    SetDialogValues(dtree);

                    if (clientDialog != null)
                    {
                        clientDialog.TryClose();
                        clientDialog = null;
                    } else
                    {
                        clientDialog = new GuiDialogBlockEntityFirepit(dialogTitle, Inventory, Pos, dtree, Api as ICoreClientAPI);
                        clientDialog.OnClosed += () => { clientDialog.Dispose(); clientDialog = null; };
                        clientDialog.TryOpen();
                        
                    }
                    
                }
            }

            if (packetid == (int)EnumBlockContainerPacketId.CloseInventory)
            {
                IClientWorldAccessor clientWorld = (IClientWorldAccessor)Api.World;
                clientWorld.Player.InventoryManager.CloseInventory(Inventory);
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

        public override void OnLoadCollectibleMappings(IWorldAccessor worldForResolve, Dictionary<int, AssetLocation> oldBlockIdMapping, Dictionary<int, AssetLocation> oldItemIdMapping)
        {
            foreach (var slot in Inventory)
            {
                if (slot.Itemstack == null) continue;
                if (!slot.Itemstack.FixMapping(oldBlockIdMapping, oldItemIdMapping, worldForResolve))
                {
                    slot.Itemstack = null;
                } else
                {
                    slot.Itemstack.Collectible.OnLoadCollectibleMappings(worldForResolve, slot, oldBlockIdMapping, oldItemIdMapping);
                }
            }

            foreach (ItemSlot slot in inventory.CookingSlots)
            {
                if (slot.Itemstack == null) continue;
                if (!slot.Itemstack.FixMapping(oldBlockIdMapping, oldItemIdMapping, Api.World))
                {
                    slot.Itemstack = null;
                }
                else
                {
                    slot.Itemstack.Collectible.OnLoadCollectibleMappings(worldForResolve, slot, oldBlockIdMapping, oldItemIdMapping);
                }
            }
        }



        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tesselator)
        {
            if (Block == null || Block.Code.Path.Contains("construct")) return false;

            // 0: Extinct
            // 1: Extinct-cooking
            // 2: Extinct-wide
            // 3: Lit
            // 4: Lit-cooking
            // 5: Lit-wide
            int index = IsBurning ? 3 : 0;

            ItemStack contentStack = inputStack == null ? outputStack : inputStack;
            MeshData contentmesh = getContentMesh(contentStack, ref index, tesselator);
            if (contentmesh != null)
            {
                mesher.AddMeshData(contentmesh);
            }

            mesher.AddMeshData(meshes[index]);

            return true;
        }

        private MeshData getContentMesh(ItemStack contentStack, ref int index, ITesselatorAPI tesselator)
        {
            if (contentStack == null) return null;

            if (contentStack.Collectible is IInFirepitMeshSupplier)
            {
                EnumFirepitModel model = EnumFirepitModel.Normal;
                MeshData mesh = (contentStack.Collectible as IInFirepitMeshSupplier).GetMeshWhenInFirepit(contentStack, Api.World, Pos, ref model);

                if (mesh != null)
                {
                    index += (int)model;
                    return mesh;
                }
            }
            
            if (contentStack.Collectible is IInFirepitRendererSupplier)
            {
                EnumFirepitModel model = (contentStack.Collectible as IInFirepitRendererSupplier).GetDesiredFirepitModel(contentStack, this, contentStack == outputStack);
                index += (int)model;
                return null;
            }

            InFirePitProps renderProps = GetRenderProps(contentStack);
            
            if (renderProps != null)
            {
                index += (int)renderProps.UseFirepitModel;

                if (contentStack.Class != EnumItemClass.Item)
                {
                    MeshData ingredientMesh;
                    tesselator.TesselateBlock(contentStack.Block, out ingredientMesh);

                    ingredientMesh.ModelTransform(renderProps.Transform);

                    // Lower by 1 voxel if extinct
                    if (!IsBurning && renderProps.UseFirepitModel != EnumFirepitModel.Cooking) ingredientMesh.Translate(0, -1 / 16f, 0);

                    return ingredientMesh;
                }

                return null;
            }
            else
            {
                if (renderer.RequireSpit) index += 1;
                return null; // Mesh drawing is handled by the FirepitContentsRenderer
            }
            
        }

        public override void OnBlockUnloaded()
        {
            base.OnBlockUnloaded();

            renderer?.Unregister();
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
    }
}