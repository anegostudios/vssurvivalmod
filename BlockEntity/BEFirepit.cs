using System;
using System.Collections.Generic;
using System.IO;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace Vintagestory.GameContent
{
    public class BlockEntityFirepit : BlockEntityContainer, IBlockShapeSupplier
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
        public float oreCookingTime;
        // How much of the current fuel is consumed
        public float fuelBurnTime;
        // How much fuel is available
        public float maxFuelBurnTime;
        // How much smoke the current fuel burns?
        public float smokeLevel;


        IGuiDialog clientDialog;
        bool clientSidePrevBurning;
        
        Block ownBlock;
        FirepitContentsRenderer renderer;
        


        MeshData[] meshes
        {
            get
            {
                object value = null;
                api.ObjectCache.TryGetValue("firepit-meshes", out value);
                return (MeshData[])value;
            }
            set { api.ObjectCache["firepit-meshes"] = value; }
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
            return inputSlot.Itemstack == null ? 30f : inputSlot.Itemstack.Collectible.GetMeltingDuration(api.World, inventory, inputSlot);
        }

        public override string InventoryClassName
        {
            get { return "stove"; }
        }

        public virtual string DialogTitle
        {
            get { return "Firepit"; }
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

            inventory.pos = pos;
            inventory.LateInitialize("smelting-1", api);
            Inventory.AfterBlocksLoaded(api.World);
            

            RegisterGameTickListener(OnBurnTick, 100);
            RegisterGameTickListener(OnSyncTick, 500);

            if (ambientSound == null && api.Side == EnumAppSide.Client)
            {
                ambientSound = ((IClientWorldAccessor)api.World).LoadSound(new SoundParams()
                {
                    Location = new AssetLocation("sounds/environment/fireplace.ogg"),
                    ShouldLoop = true,
                    Position = pos.ToVec3f().Add(0.5f, 0.25f, 0.5f),
                    DisposeOnFinish = false,
                    Volume = SoundLevel
                });
            }

            if (api is ICoreClientAPI)
            {
                renderer = new FirepitContentsRenderer(api as ICoreClientAPI, pos);

                (api as ICoreClientAPI).Event.RegisterRenderer(renderer, EnumRenderStage.Opaque);

                if (meshes == null) GenMeshes();
            }

            ownBlock = api.World.BlockAccessor.GetBlock(pos);
        }


        private void OnSlotModifid(int slotid)
        {
            ownBlock = api.World.BlockAccessor.GetBlock(pos);

            if (slotid == 1)
            {
                if (inputStack?.Collectible?.CombustibleProps?.SmeltedStack?.ResolvedItemstack?.Collectible?.NutritionProps != null)
                {
                    renderer?.SetContents(inputStack);
                } else
                {
                    renderer?.SetContents(null);
                }
                
                MarkDirty(true);
            }

            if (api is ICoreClientAPI && clientDialog != null)
            {
                SetDialogValues(clientDialog.Attributes);
            }
        }

        internal void GenMeshes()
        {
            Block block = api.World.BlockAccessor.GetBlock(pos);
            if (block.BlockId == 0) return;

            MeshData[] meshes = new MeshData[17];
            ITesselatorAPI mesher = ((ICoreClientAPI)api).Tesselator;

            // 0: Extinct
            // 1: Extinct-cooking
            // 2: Extinct-small-crucible
            // 3: Lit
            // 4: Lit-cooking
            // 5: Lit-small-crucible
            meshes = new MeshData[6];
            mesher.TesselateShape(block, api.Assets.TryGet("shapes/block/wood/firepit/extinct.json").ToObject<Shape>(), out meshes[0]);
            mesher.TesselateShape(block, api.Assets.TryGet("shapes/block/wood/firepit/extinct-cooking.json").ToObject<Shape>(), out meshes[1]);
            mesher.TesselateShape(block, api.Assets.TryGet("shapes/block/wood/firepit/extinct-wide.json").ToObject<Shape>(), out meshes[2]);
            mesher.TesselateShape(block, api.Assets.TryGet("shapes/block/wood/firepit/lit.json").ToObject<Shape>(), out meshes[3]);
            mesher.TesselateShape(block, api.Assets.TryGet("shapes/block/wood/firepit/lit-cooking.json").ToObject<Shape>(), out meshes[4]);
            mesher.TesselateShape(block, api.Assets.TryGet("shapes/block/wood/firepit/lit-wide.json").ToObject<Shape>(), out meshes[5]);

            meshes[0].Tints = null;
            meshes[1].Tints = null;
            meshes[2].Tints = null;
            meshes[3].Tints = null;
            meshes[4].Tints = null;
            meshes[5].Tints = null;

            this.meshes = meshes;
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
            if (api is ICoreClientAPI) return;

            // Use up fuel
            if (fuelBurnTime > 0)
            {
                fuelBurnTime -= dt;

                if (fuelBurnTime <= 0)
                {
                    fuelBurnTime = 0;
                    maxFuelBurnTime = 0;
                    setStoveBurning(false);
                }
            }

            // Furnace is burning: Heat furnace
            if (IsBurning)
            {
                furnaceTemperature = changeTemperature(furnaceTemperature, maxTemperature, dt);
            }

            // Ore follows furnace temperature
            if (canSmeltInput())
            {
                heatOre(dt);
            } else
            {
                oreCookingTime = 0;
            }

            // Finished smelting? Turn to smelted item
            if (oreCookingTime > maxCookingTime())
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
            if (api is ICoreServerAPI && (IsBurning || prevFurnaceTemperature != furnaceTemperature))
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

            bool smeltableInput = canSmeltInput();

            return
                    (BurnsAllFuell || smeltableInput)
                    // Require fuel
                    && fuelCopts.BurnTemperature * HeatModifier > 0
            ;
        }



        public void heatOre(float dt)
        {
            //dt *= 20;

            // Only Heat ore. Cooling happens already in the itemstack
            if (OreTemperature < furnaceTemperature)
            {
                OreTemperature = changeTemperature(OreTemperature, furnaceTemperature, 2 * dt);
                int maxTemp = inputStack.Attributes.GetInt("maxHeatableTemp", 0);
                if (maxTemp > 0)
                {
                    OreTemperature = Math.Min(OreTemperature, maxTemp);
                }
            }

            // Begin smelting when hot enough
            float meltingPoint = inputSlot.Itemstack.Collectible.GetMeltingPoint(api.World, inventory, inputSlot);
            if (OreTemperature >= meltingPoint)
            {
                float diff = OreTemperature / meltingPoint;
                oreCookingTime += GameMath.Clamp((int)(diff), 1, 30) * dt;
            }
            else
            {
                if (oreCookingTime > 0) oreCookingTime--;
            }
        }


        public float OreTemperature
        {
            get
            {
                ItemStack stack = inputStack;
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
                            float stackTemp = cookingStack.Collectible.GetTemperature(api.World, cookingStack);
                            lowestTemp = haveStack ? Math.Min(lowestTemp, stackTemp) : stackTemp;
                            haveStack = true;
                        }

                    }

                    return lowestTemp;

                } else
                {
                    return stack.Collectible.GetTemperature(api.World, stack);
                }
            }
            set
            {
                if (inputStack == null) return;
                if (inventory.CookingSlots.Length > 0)
                {
                    for (int i = 0; i < inventory.CookingSlots.Length; i++)
                    {
                        inventory.CookingSlots[i].Itemstack?.Collectible.SetTemperature(api.World, inventory.CookingSlots[i].Itemstack, value);
                    }
                } else
                {
                    inputStack.Collectible.SetTemperature(api.World, inputStack, value);
                }
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
            BlockFirepit block = api.World.BlockAccessor.GetBlock(pos) as BlockFirepit;
            if (block == null) return;
            
            if (burning)
            {
                block.Ignite(api.World, pos);
            } else
            {
                block.Extinguish(api.World, pos);
            }

            MarkDirty(true);
        }


        
        public bool canSmeltInput()
        {
            return 
                inputStack != null 
                && inputStack.Collectible.CanSmelt(api.World, inventory, inputSlot.Itemstack, outputSlot.Itemstack) 
                && (inputStack.Collectible.CombustibleProps == null || !inputStack.Collectible.CombustibleProps.RequiresContainer)
            ;
        }


        public void smeltItems()
        {
            inputStack.Collectible.DoSmelt(api.World, inventory, inputSlot, outputSlot);
            OreTemperature = enviromentTemperature();
            oreCookingTime = 0;
            MarkDirty();
        }


        #region Events

        public override bool OnPlayerRightClick(IPlayer byPlayer, BlockSelection blockSel)
        {
            if (api.World is IServerWorldAccessor)
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

                ((ICoreServerAPI)api).Network.SendBlockEntityPacket(
                    (IServerPlayer)byPlayer,
                    pos.X, pos.Y, pos.Z,
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

            if (api != null)
            {
                Inventory.AfterBlocksLoaded(api.World);
            }
            

            furnaceTemperature = tree.GetFloat("furnaceTemperature");
            maxTemperature = tree.GetInt("maxTemperature");
            oreCookingTime = tree.GetFloat("oreCookingTime");
            fuelBurnTime = tree.GetFloat("fuelBurnTime");
            maxFuelBurnTime = tree.GetFloat("maxFuelBurnTime");

            if (api?.Side == EnumAppSide.Client && clientDialog != null)
            {
                if (inputStack?.Collectible?.CombustibleProps?.SmeltedStack?.ResolvedItemstack?.Collectible?.NutritionProps != null)
                {
                    renderer?.SetContents(inputStack);
                }
                else
                {
                    renderer?.SetContents(null);
                }

                SetDialogValues(clientDialog.Attributes);
            }


            if (api?.Side == EnumAppSide.Client && clientSidePrevBurning != IsBurning)
            {
                ambientSound.Toggle(IsBurning);
                clientSidePrevBurning = IsBurning;
                MarkDirty(true);
            }
        }


        void SetDialogValues(ITreeAttribute dialogTree)
        {
            dialogTree.SetFloat("furnaceTemperature", furnaceTemperature);

            dialogTree.SetInt("maxTemperature", maxTemperature);
            dialogTree.SetFloat("oreCookingTime", oreCookingTime);
            dialogTree.SetFloat("maxFuelBurnTime", maxFuelBurnTime);
            dialogTree.SetFloat("fuelBurnTime", fuelBurnTime);

            if (inputSlot.Itemstack != null)
            {
                float meltingDuration = inputSlot.Itemstack.Collectible.GetMeltingDuration(api.World, inventory, inputSlot);

                dialogTree.SetFloat("oreTemperature", OreTemperature);
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
            tree.SetFloat("oreCookingTime", oreCookingTime);
            tree.SetFloat("fuelBurnTime", fuelBurnTime);
            tree.SetFloat("maxFuelBurnTime", maxFuelBurnTime);
        }




        public override void OnBlockRemoved()
        {
            base.OnBlockRemoved();

            if (api.World is IServerWorldAccessor)
            {
                Inventory.DropAll(pos.ToVec3d().Add(0.5, 0.5, 0.5));
            }

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
                api.World.BlockAccessor.GetChunkAtBlockPos(pos.X, pos.Y, pos.Z).MarkModified();

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

                    IClientWorldAccessor clientWorld = (IClientWorldAccessor)api.World;

                    SyncedTreeAttribute dtree = new SyncedTreeAttribute();
                    SetDialogValues(dtree);

                    clientDialog = clientWorld.OpenDialog(dialogClassName, dialogTitle, Inventory, pos, dtree);
                }
            }

            if (packetid == (int)EnumBlockContainerPacketId.CloseInventory)
            {
                IClientWorldAccessor clientWorld = (IClientWorldAccessor)api.World;
                clientWorld.Player.InventoryManager.CloseInventory(Inventory);
            }
        }

        #endregion

        #region Helper getters


        public IItemSlot fuelSlot
        {
            get { return inventory.GetSlot(0); }
        }

        public IItemSlot inputSlot
        {
            get { return inventory.GetSlot(1); }
        }

        public IItemSlot outputSlot
        {
            get { return inventory.GetSlot(2); }
        }

        public IItemSlot[] otherCookingSlots
        {
            get { return inventory.CookingSlots; }
        }

        public ItemStack fuelStack
        {
            get { return inventory.GetSlot(0).Itemstack; }
            set { inventory.GetSlot(0).Itemstack = value; inventory.GetSlot(0).MarkDirty(); }
        }

        public ItemStack inputStack
        {
            get { return inventory.GetSlot(1).Itemstack; }
            set { inventory.GetSlot(1).Itemstack = value; inventory.GetSlot(1).MarkDirty(); }
        }

        public ItemStack outputStack
        {
            get { return inventory.GetSlot(2).Itemstack; }
            set { inventory.GetSlot(2).Itemstack = value; inventory.GetSlot(2).MarkDirty(); }
        }


        public CombustibleProperties fuelCombustibleOpts
        {
            get { return getCombustibleOpts(0); }
        }

        public CombustibleProperties getCombustibleOpts(int slotid)
        {
            IItemSlot slot = inventory.GetSlot(slotid);
            if (slot.Itemstack == null) return null;
            return slot.Itemstack.Collectible.CombustibleProps;
        }

        #endregion


        public override void OnStoreCollectibleMappings(Dictionary<int, AssetLocation> blockIdMapping, Dictionary<int, AssetLocation> itemIdMapping)
        {
            int q = Inventory.QuantitySlots;
            for (int i = 0; i < q; i++)
            {
                ItemSlot slot = Inventory.GetSlot(i);
                if (slot.Itemstack == null) continue;

                if (slot.Itemstack.Class == EnumItemClass.Item)
                {
                    itemIdMapping[slot.Itemstack.Item.Id] = slot.Itemstack.Item.Code;
                }
                else
                {
                    blockIdMapping[slot.Itemstack.Block.BlockId] = slot.Itemstack.Block.Code;
                }
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
            }
        }

        public override void OnLoadCollectibleMappings(IWorldAccessor worldForResolve, Dictionary<int, AssetLocation> oldBlockIdMapping, Dictionary<int, AssetLocation> oldItemIdMapping)
        {
            int q = Inventory.QuantitySlots;
            for (int i = 0; i < q; i++)
            {
                ItemSlot slot = Inventory.GetSlot(i);
                if (slot.Itemstack == null) continue;
                slot.Itemstack.FixMapping(oldBlockIdMapping, oldItemIdMapping, worldForResolve);
            }

            foreach (ItemSlot slot in inventory.CookingSlots)
            {
                if (slot.Itemstack == null) continue;
                slot.Itemstack.FixMapping(oldBlockIdMapping, oldItemIdMapping, api.World);
            }
        }



        public bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tesselator)
        {
            if (ownBlock == null || ownBlock.Code.Path.Contains("construct")) return false;

            // 0: Extinct
            // 1: Extinct-cooking
            // 2: Extinct-small-crucible
            // 3: Lit
            // 4: Lit-cooking
            // 5: Lit-small-crucible
            int index = IsBurning ? 3 : 0;

            ModelTransform inFirePitTransform = null;
            if (inputStack?.ItemAttributes?.KeyExists("inFirePitTransform") == true)
            {
                inFirePitTransform = inputStack.ItemAttributes["inFirePitTransform"].AsObject<ModelTransform>();
                inFirePitTransform.EnsureDefaultValues();
            }
            if (inFirePitTransform != null)
            {
                index += 2;

                MeshData ingredientMesh;
                if (inputStack.Class == EnumItemClass.Item)
                {
                    tesselator.TesselateItem(inputStack.Item, out ingredientMesh);
                } else
                {
                    tesselator.TesselateBlock(inputStack.Block, out ingredientMesh);
                }
                
                ingredientMesh.ModelTransform(inFirePitTransform);
                mesher.AddMeshData(ingredientMesh);

            }
            else if (inputStack?.Collectible.CombustibleProps?.SmeltedStack != null && inputStack.Collectible.CombustibleProps.SmeltedStack?.ResolvedItemstack?.Collectible?.NutritionProps != null)
            {
                index += 1;

                if (inputStack.Class == EnumItemClass.Block)
                {
                    MeshData ingredientMesh;
                    (api as ICoreClientAPI).Tesselator.TesselateItem(inputStack.Item, out ingredientMesh);
                    mesher.AddMeshData(ingredientMesh);
                }
            }


            mesher.AddMeshData(meshes[index]);

            return true;
        }

        public override void OnBlockUnloaded()
        {
            base.OnBlockUnloaded();

            renderer?.Unregister();
        }

    }
}
