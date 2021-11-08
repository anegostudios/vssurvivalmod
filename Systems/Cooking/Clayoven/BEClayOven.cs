using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace Vintagestory.GameContent
{
    class BlockEntityOven : BlockEntityDisplay, IHeatSource
    {
        // One Vec3f object only, for performance
        static readonly Vec3f centre = new Vec3f(0.5f, 0, 0.5f);

        /// <summary>
        /// The number of times to re-render the baked item in the rising stage
        /// </summary>
        public static int BakingStageThreshold = 100; // Every 1% growth, retesselate



        /// <summary>
        /// The maximum baking (or smelting) temperature of items which this oven will accept to be placed
        /// Modded clay ovens wishing to raise this will need to override BlockClayOven.OnLoaded()
        /// </summary>
        public const int maxBakingTemperatureAccepted = 260;

        /// <summary>
        /// The maximum temperature this oven will reach in the burning stage
        /// </summary>
        public virtual float maxTemperature => 300;
        /// <summary>
        /// The number of "regular" bread items the oven can accept
        /// </summary>
        public virtual int itemCapacity => 4;
        /// <summary>
        /// The number of logs of firewood the oven can accept
        /// </summary>
        public virtual int fuelitemCapacity => 6;

        /// <summary>
        /// Is there currently burning fuel in the oven?
        /// </summary>
        bool burning;
        /// <summary>
        /// Used client side to know whether to play the burning sound
        /// </summary>
        bool clientSidePrevBurning;

        /// <summary>
        /// Temperature before the half second tick
        /// </summary>
        public float prevOvenTemperature = 20;
        /// <summary>
        /// Current temperature of the oven
        /// </summary>
        public float ovenTemperature = 20;

        /// <summary>
        /// The length of time remaining, for this fuel to continue burning
        /// </summary>
        private float fuelBurnTime;
        /// <summary>
        /// Data about the level of browning/baking reached for the baked items
        /// </summary>
        private readonly OvenItemData[] bakingData;
        /// <summary>
        /// Some random numbers for the fuel logs (they are not exactly orthogonal): stored in an array to ensure consistency from state to state
        /// </summary>
        private int[] woodrand;
        private bool woodrandDone = false;
        private ItemStack lastRemoved = null;
        /// <summary>
        /// For rendering: degrees of rotation of contents depending on block variant - 0 is east
        /// </summary>
        private int rotation;
        Random prng;
        private int syncCount;

        ILoadedSound ambientSound;

        /// <summary>
        /// Slots 0-3: Baking items  -~-  Slot 4: Fuel
        /// </summary>
        internal InventoryOven ovenInv;

        public BlockEntityOven()
        {
            bakingData = new OvenItemData[itemCapacity];
            for (int i = 0; i < itemCapacity; i++)
            {
                bakingData[i] = new OvenItemData();
            }
            woodrand = new int[fuelitemCapacity];
            ovenInv = new InventoryOven("oven-0", itemCapacity, fuelitemCapacity);
            meshes = new MeshData[itemCapacity + fuelitemCapacity];
        }

        public override InventoryBase Inventory => ovenInv;

        public override string InventoryClassName => "oven";

        public ItemSlot FuelSlot { get { return ovenInv[itemCapacity]; } }

        public bool IsBurning { get { return burning; } }

        public bool HasItems
        {
            get
            {
                for (int i = 0; i < itemCapacity; i++)
                {
                    if (!ovenInv[i].Empty) return true;
                }
                return false;
            }
        }

        public override void Initialize(ICoreAPI api)
        {
            capi = api as ICoreClientAPI;
            base.Initialize(api);
            ovenInv.LateInitialize(InventoryClassName + "-" + Pos, api);

            RegisterGameTickListener(OnBurnTick, 100);
            this.prng = new Random((int)(this.Pos.GetHashCode()));
            this.SetRotation();
        }

        private void SetRotation()
        {
            switch (Block.Variant["side"])
            {
                case "south":
                    this.rotation = 270;
                    break;
                case "west":
                    this.rotation = 180;
                    break;
                case "east":
                    this.rotation = 0;
                    break;
                default:
                    this.rotation = 90;
                    break;
            }
        }

        public float GetHeatStrength(IWorldAccessor world, BlockPos heatSourcePos, BlockPos heatReceiverPos)
        {
            return Math.Max((this.ovenTemperature - 20f) / (this.maxTemperature - 20f) * 8f, 0f);
        }


        #region Interaction: AI for placing and taking items

        public virtual bool OnInteract(IPlayer byPlayer, BlockSelection bs)
        {
            ItemSlot slot = byPlayer.InventoryManager.ActiveHotbarSlot;

            if (slot.Empty)
            {
                if (TryTake(byPlayer))
                {
                    byPlayer.InventoryManager.BroadcastHotbarSlot();
                    return true;
                }
                return false;
            }
            else
            {
                CollectibleObject colObj = slot.Itemstack.Collectible;
                if (colObj.Attributes?.IsTrue("isFirewood") == true)
                {
                    if (TryFuel(slot))
                    {
                        AssetLocation sound = slot.Itemstack?.Block?.Sounds?.Place;
                        Api.World.PlaySoundAt(sound != null ? sound : new AssetLocation("sounds/player/build"), byPlayer.Entity, byPlayer, true, 16);
                        byPlayer.InventoryManager.BroadcastHotbarSlot();
                        (byPlayer as IClientPlayer)?.TriggerFpAnimation(EnumHandInteract.HeldItemInteract);
                        return true;
                    }

                    return false;

                }
                else if (colObj.Attributes?["bakingProperties"] != null || colObj.CombustibleProps?.SmeltingType == EnumSmeltType.Bake && colObj.CombustibleProps.MeltingPoint < maxBakingTemperatureAccepted)  //Can't meaningfully bake anything requiring heat over 260 in the basic clay oven
                {
                    if (slot.Itemstack.Equals(Api.World, lastRemoved, GlobalConstants.IgnoredStackAttributes) && !ovenInv[0].Empty)
                    {
                        if (TryTake(byPlayer))
                        {
                            byPlayer.InventoryManager.BroadcastHotbarSlot();
                            return true;
                        }
                    }
                    else
                    {
                        if (TryPut(slot))
                        {
                            AssetLocation sound = slot.Itemstack?.Block?.Sounds?.Place;
                            Api.World.PlaySoundAt(sound != null ? sound : new AssetLocation("sounds/player/buildhigh"), byPlayer.Entity, byPlayer, true, 16);
                            byPlayer.InventoryManager.BroadcastHotbarSlot();
                            return true;
                        }
                        else
                        {
                            if (slot.Itemstack.Block?.GetBehavior<BlockBehaviorCanIgnite>() == null)
                            {
                                ICoreClientAPI capi = Api as ICoreClientAPI;
                                if (capi != null && (slot.Empty || slot.Itemstack.Attributes.GetBool("bakeable", true) == false)) capi.TriggerIngameError(this, "notbakeable", Lang.Get("This item is not bakeable."));
                                else if (capi != null && !slot.Empty) capi.TriggerIngameError(this, "notbakeable", burning ? Lang.Get("Wait until the fire is out") : Lang.Get("Oven is full"));
                                
                                return true;
                            }
                        }
                    }

                    return false;
                }
                else if (TryTake(byPlayer))
                //TryTake with non-empty hotbar slot, filling available empty slots in player inventory
                {
                    byPlayer.InventoryManager.BroadcastHotbarSlot();
                    return true;
                }
            }

            return false;
        }

        protected virtual bool TryFuel(ItemSlot slot)
        {
            if (IsBurning || HasItems) return false;

            if (FuelSlot.Empty || FuelSlot.Itemstack.StackSize < fuelitemCapacity)
            {
                int moved = slot.TryPutInto(Api.World, FuelSlot);

                if (moved > 0)
                {
                    MarkDirty(true);
                    lastRemoved = null;
                }

                return moved > 0;
            }

            return false;
        }

        protected virtual bool TryPut(ItemSlot slot)
        {
            if (IsBurning || !FuelSlot.Empty) return false;

            BakingProperties bakingProps = BakingProperties.ReadFrom(slot.Itemstack);
            if (bakingProps == null) return false;

            if (slot.Itemstack.Attributes.GetBool("bakeable", true) == false) return false;

            // For large items (pies) check all 4 oven slots are empty before adding the item

            if (bakingProps.LargeItem)
            {
                for (int index = 0; index < itemCapacity; index++)
                {
                    if (!ovenInv[index].Empty)
                    {
                        return false;
                    }
                }
            }

            for (int index = 0; index < itemCapacity; index++)
            {
                if (ovenInv[index].Empty)
                {
                    int moved = slot.TryPutInto(Api.World, ovenInv[index]);

                    if (moved > 0)
                    {
                        // We store the baked level data into the BlockEntity itself, for continuity and to avoid adding unwanted attributes to the ItemStacks (which e.g. could cause them not to stack)
                        bakingData[index] = new OvenItemData(ovenInv[index].Itemstack);
                        updateMesh(index);

                        MarkDirty(true);
                        lastRemoved = null;
                    }

                    return moved > 0;
                }
                else if (index == 0)
                {
                    // Disallow other items from being inserted if slot 0 holds a large item (a pie)

                    BakingProperties slot0Props = BakingProperties.ReadFrom(ovenInv[index].Itemstack);
                    if (slot0Props != null && slot0Props.LargeItem) return false;
                }
            }
            return false;
        }

        protected virtual bool TryTake(IPlayer byPlayer)
        {
            for (int index = itemCapacity; index >= 0; index--)
            {
                if (index == itemCapacity && !FuelSlot.Empty && FuelSlot.Itemstack.Collectible.Attributes?.IsTrue("isFirewood") == true)
                    continue;

                if (!ovenInv[index].Empty)
                {
                    ItemStack stack = ovenInv[index].TakeOut(1);
                    lastRemoved = stack == null ? null : stack.Clone();
                    if (byPlayer.InventoryManager.TryGiveItemstack(stack))  //TODO GENERALLY ##  this behaviour is annoying if the player has a different hotbar slot highlighted
                    {
                        AssetLocation sound = stack.Block?.Sounds?.Place;
                        Api.World.PlaySoundAt(sound != null ? sound : new AssetLocation("sounds/player/throw"), byPlayer.Entity, byPlayer, true, 16);
                    }

                    if (stack.StackSize > 0)
                    {
                        Api.World.SpawnItemEntity(stack, Pos.ToVec3d().Add(0.5, 0.5, 0.5));
                    }

                    bakingData[index].CurHeightMul = 1; // Reset risenLevel to avoid brief render of unwanted size on next item inserted, if server/client not perfectly in sync - note this only really works if the newly inserted item can be assumed to have risenLevel of 0 i.e. dough
                    updateMesh(index);
                    MarkDirty(true);
                    return true;
                }

            }
            return false;
        }

        public virtual ItemStack[] CanAdd(ItemStack[] itemstacks)
        {
            if (IsBurning) return null;
            if (!FuelSlot.Empty) return null;
            if (ovenTemperature <= EnvironmentTemperature() + 25) return null;   // Don't invite player to insert bakeable items in a cold oven - 25 degrees allows some hysteresis if SEASONS causes changes in enviro temperature
            for (int i = 0; i < itemCapacity; i++)
            {
                if (ovenInv[i].Empty) return itemstacks;
            }
            return null;
        }

        public virtual ItemStack[] CanAddAsFuel(ItemStack[] itemstacks)
        {
            if (IsBurning) return null;
            for (int i = 0; i < itemCapacity; i++)
            {
                if (!ovenInv[i].Empty) return null;
            }
            return (FuelSlot.StackSize < fuelitemCapacity) ? itemstacks : null;
        }

        public bool TryIgnite()
        {
            if (!CanIgnite()) return false;

            burning = true;
            fuelBurnTime = 45 + FuelSlot.StackSize * 5;  //approximately 1 hour of game time for a full stack of fuel
            MarkDirty();
            ambientSound?.Start();
            return true;
        }

        public bool CanIgnite()
        {
            return !FuelSlot.Empty && !burning;
        }

        #endregion


        protected virtual void OnBurnTick(float dt)
        {
            dt *= 1.25f;   //slight speedup because everything just felt too slow...

            // Only tick on the server and merely sync to client
            if (Api is ICoreClientAPI)
            {
                // TODO: maybe update rendered transitions gradually?  similar to renderer?.contentStackRenderer?.OnUpdate(InputStackTemp);
                return;
            }

            // Use up fuel
            if (fuelBurnTime > 0)
            {
                fuelBurnTime -= dt;

                if (fuelBurnTime <= 0)
                {
                    fuelBurnTime = 0;
                    burning = false;
                    CombustibleProperties props = FuelSlot.Itemstack.Collectible.CombustibleProps;
                    if (props?.SmeltedStack == null)
                    {
                        FuelSlot.Itemstack = null;
                        for (int i = 0; i < itemCapacity; i++) bakingData[i].CurHeightMul = 1;
                    }
                    else
                    {
                        // Allows for wood ash inserted by mods (for example, LazyWarlock's Tweaks)
                        int count = FuelSlot.StackSize;
                        FuelSlot.Itemstack = props.SmeltedStack.ResolvedItemstack.Clone();
                        FuelSlot.Itemstack.StackSize = count * props.SmeltedRatio;
                    }
                    MarkDirty(true);
                }
            }

            // Heat furnace slowly if fire is burning, or else cool down very slowly
            if (IsBurning)
            {
                ovenTemperature = ChangeTemperature(ovenTemperature, maxTemperature, dt * FuelSlot.StackSize / fuelitemCapacity);
            }
            else
            {
                int environmentTemperature = EnvironmentTemperature();
                if (ovenTemperature > environmentTemperature)
                {
                    HeatInput(dt);
                    ovenTemperature = ChangeTemperature(ovenTemperature, environmentTemperature, dt / 24f);
                }
            }

            
            // Sync to client every 500ms
            if (++syncCount % 5 == 0 && (IsBurning || prevOvenTemperature != ovenTemperature || !Inventory.Empty))
            {
                MarkDirty();
                prevOvenTemperature = ovenTemperature;
            }
        }

        protected virtual void HeatInput(float dt)
        {
            for (int slotIndex = 0; slotIndex < itemCapacity; slotIndex++)
            {
                ItemStack stack = ovenInv[slotIndex].Itemstack;
                if (stack != null)
                {
                    float nowTemp = HeatStack(stack, dt, slotIndex);
                    // Begin baking - or at least rising - when hot enough
                    if (nowTemp >= 100f)
                    {
                        IncrementallyBake(dt * 1.2f, slotIndex);
                    }
                }
            }
        }

        protected virtual float HeatStack(ItemStack stack, float dt, int i)
        {
            float oldTemp = bakingData[i].temp;
            float nowTemp = oldTemp;

            if (oldTemp < ovenTemperature)
            {
                float f = (1 + GameMath.Clamp((ovenTemperature - oldTemp) / 28, 0, 1.6f)) * dt;
                nowTemp = ChangeTemperature(oldTemp, ovenTemperature, f);
                int maxTemp = Math.Max(stack.Collectible.CombustibleProps?.MaxTemperature ?? 0, stack.ItemAttributes?["maxTemperature"].AsInt(0) ?? 0);
                if (maxTemp > 0)
                {
                    nowTemp = Math.Min(maxTemp, nowTemp);
                }
            }
            else if (oldTemp > ovenTemperature)
            {
                float f = (1 + GameMath.Clamp((oldTemp - ovenTemperature) / 28, 0, 1.6f)) * dt;
                nowTemp = ChangeTemperature(oldTemp, ovenTemperature, f);
            }

            if (oldTemp != nowTemp)
            {
                bakingData[i].temp = nowTemp;
            }

            return nowTemp;
        }

        protected virtual void IncrementallyBake(float dt, int slotIndex)
        {
            ItemSlot slot = Inventory[slotIndex];
            OvenItemData bakeData = bakingData[slotIndex];

            float targetTemp = bakeData.BrowningPoint;
            if (targetTemp == 0) targetTemp = 160f;  //prevents any possible divide by zero
            float diff = bakeData.temp / targetTemp;
            float timeFactor = bakeData.TimeToBake;
            if (timeFactor == 0) timeFactor = 1;  //prevents any possible divide by zero
            float delta = GameMath.Clamp((int)diff, 1, 30) * dt / timeFactor;

            float currentLevel = bakeData.BakedLevel;
            if (bakeData.temp > targetTemp)
            {
                currentLevel = bakeData.BakedLevel + delta;
                bakeData.BakedLevel = currentLevel;
            }

            var bakeProps = BakingProperties.ReadFrom(slot.Itemstack);
            float levelFrom = bakeProps?.LevelFrom ?? 0f;
            float levelTo = bakeProps?.LevelTo ?? 1f;
            float startHeightMul = bakeProps?.StartScaleY ?? 1f;
            float endHeightMul = bakeProps?.EndScaleY ?? 1f;

            float progress = GameMath.Clamp((currentLevel - levelFrom) / (levelTo - levelFrom), 0, 1);
            float heightMul = GameMath.Mix(startHeightMul, endHeightMul, progress);
            float nowHeightMulStaged = (int)(heightMul * BakingStageThreshold) / (float)BakingStageThreshold;

            bool reDraw = nowHeightMulStaged != bakeData.CurHeightMul;
            
            bakeData.CurHeightMul = nowHeightMulStaged;

            // see if increasing the partBaked by delta, has moved this stack up to the next "bakedStage", i.e. a different item

            if (currentLevel > levelTo)
            {
                float nowTemp = bakeData.temp;
                string resultCode = bakeProps?.ResultCode;

                if (resultCode != null)
                {
                    ItemStack resultStack = null;
                    if (slot.Itemstack.Class == EnumItemClass.Block)
                    {
                        Block block = Api.World.GetBlock(new AssetLocation(resultCode));
                        if (block != null)
                        {
                            resultStack = new ItemStack(block);
                        }
                    }
                    else
                    {
                        Item item = Api.World.GetItem(new AssetLocation(resultCode));
                        if (item != null) resultStack = new ItemStack(item);
                    }


                    if (resultStack != null)
                    {
                        var collObjCb = ovenInv[slotIndex].Itemstack.Collectible as IBakeableCallback;

                        if (collObjCb != null)
                        {
                            collObjCb.OnBaked(ovenInv[slotIndex].Itemstack, resultStack);
                        }

                        ovenInv[slotIndex].Itemstack = resultStack;
                        bakingData[slotIndex] = new OvenItemData(resultStack);
                        bakingData[slotIndex].temp = nowTemp;

                        reDraw = true;
                    }
                }
                else
                {
                    // Allow the oven also to 'smelt' low-temperature bakeable items which do not have specific baking properties

                    ItemSlot result = new DummySlot(null);
                    if (slot.Itemstack.Collectible.CanSmelt(Api.World, ovenInv, slot.Itemstack, null))
                    {
                        slot.Itemstack.Collectible.DoSmelt(Api.World, ovenInv, ovenInv[slotIndex], result);
                        if (!result.Empty)
                        {
                            ovenInv[slotIndex].Itemstack = result.Itemstack;
                            bakingData[slotIndex] = new OvenItemData(result.Itemstack);
                            bakingData[slotIndex].temp = nowTemp;
                            reDraw = true;
                        }
                    }
                }
            }

            if (reDraw)
            {
                updateMesh(slotIndex);
                MarkDirty(true);
            }
        }




        /// <summary>
        /// Resting temperature - note it can change
        /// </summary>
        protected virtual int EnvironmentTemperature()
        {
            var conds = Api.World.BlockAccessor.GetClimateAt(Pos, EnumGetClimateMode.NowValues);   //TODO: Is there a performance issue here?
            return conds == null ? 20 : (int)conds.Temperature;
        }

        public virtual float ChangeTemperature(float fromTemp, float toTemp, float dt)
        {
            float diff = Math.Abs(fromTemp - toTemp);
            diff *= GameMath.Sqrt(diff);
            dt += dt * (diff / 480);
            if (diff < dt)
            {
                return toTemp;
            }
            if (fromTemp > toTemp)
            {
                dt = -dt / 2;
            }
            if (Math.Abs(fromTemp - toTemp) < 1)
            {
                return toTemp;
            }
            return fromTemp + dt;
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);

            ovenInv.FromTreeAttributes(tree);
            burning = tree.GetInt("burn") > 0;
            rotation = tree.GetInt("rota");
            ovenTemperature = tree.GetFloat("temp");
            fuelBurnTime = tree.GetFloat("tfuel");

            for (int i = 0; i < itemCapacity; i++)
            {
                bakingData[i] = OvenItemData.ReadFromTree(tree, i);
            }

            if (Api?.Side == EnumAppSide.Client)
            {
                updateMeshes();
                if (clientSidePrevBurning != IsBurning)
                {
                    ToggleAmbientSounds(IsBurning);
                    clientSidePrevBurning = IsBurning;
                    MarkDirty(true);
                }
            }
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);

            ovenInv.ToTreeAttributes(tree);
            tree.SetInt("burn", burning ? 1 : 0);
            tree.SetInt("rota", rotation);
            tree.SetFloat("temp", ovenTemperature);
            tree.SetFloat("tfuel", fuelBurnTime);

            for (int i = 0; i < itemCapacity; i++)
            {
                bakingData[i].WriteToTree(tree, i);
            }
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder sb)
        {
            if (ovenTemperature <= 25)
            {
                sb.AppendLine(Lang.Get("Temperature: {0}", Lang.Get("Cold")));
            }
            else
            {
                sb.AppendLine(Lang.Get("Temperature: {0}°C", (int)ovenTemperature));
                if (ovenTemperature < 100 && !IsBurning)
                {
                    sb.AppendLine(Lang.Get("Reheat to continue baking"));
                }
            }

            sb.AppendLine();

            for (int index = 0; index < itemCapacity; index++)
            {
                if (!ovenInv[index].Empty)
                {
                    ItemStack stack = ovenInv[index].Itemstack;
                    sb.Append(stack.GetName());
                    sb.AppendLine(string.Format(" ({0}°C)", (int)bakingData[index].temp));
                }
            }
        }


        #region Sounds

        public virtual void ToggleAmbientSounds(bool on)
        {
            if (Api.Side != EnumAppSide.Client) return;

            if (on)
            {
                if (ambientSound == null || !ambientSound.IsPlaying)
                {
                    ambientSound = ((IClientWorldAccessor)Api.World).LoadSound(new SoundParams()
                    {
                        Location = new AssetLocation("sounds/environment/fireplace.ogg"),
                        ShouldLoop = true,
                        Position = Pos.ToVec3f().Add(0.5f, 0.1f, 0.5f),
                        DisposeOnFinish = false,
                        Volume = 0.66f
                    });

                    ambientSound.Start();
                }
            }
            else
            {
                ambientSound.Stop();
                ambientSound.Dispose();
                ambientSound = null;
            }

        }

        public override void OnBlockRemoved()
        {
            base.OnBlockRemoved();

            if (ambientSound != null)
            {
                ambientSound.Stop();
                ambientSound.Dispose();
            }
        }

        #endregion


        #region Rendering

        protected override void updateMeshes()
        {
            for (int i = 0; i < meshes.Length; i++)
            {
                updateMesh(i);
            }
        }

        protected override void updateMesh(int index)
        {
            if (Api == null || Api.Side == EnumAppSide.Server) return;
            ItemStack stack;
            bool isWood = false;
            float scaleY = 0;
            if (index < itemCapacity)
            {
                if (Inventory[index].Empty)
                {
                    meshes[index] = null;
                    return;
                }
                stack = Inventory[index].Itemstack;

                scaleY = bakingData[index].CurHeightMul;
            }
            else
            {
                int count = FuelSlot.Empty ? 0 : FuelSlot.Itemstack.StackSize;
                if (count <= index - itemCapacity)
                {
                    meshes[index] = null;
                    return;
                }
                stack = FuelSlot.Itemstack.Clone();
                stack.StackSize = 1;
                isWood = stack.Collectible.Attributes?.IsTrue("isFirewood") == true;
            }

            bool isLargeItem = false;
            if (index == 0)
            {
                BakingProperties props = BakingProperties.ReadFrom(stack);
                if (props == null) return;
                isLargeItem = props.LargeItem;
            }
            MeshData mesh = genMesh(stack, index);
            if (mesh != null)
            {
                translateMesh(mesh, index, isWood, isLargeItem, scaleY);
                meshes[index] = mesh;
            }
        }

        /// <summary>
        /// Adjust the mesh of the in-oven item, whether it is firewood, bread or pie
        /// </summary>
        /// <param name="mesh"></param>
        /// <param name="index"></param>
        /// <param name="isWood"></param>
        /// <param name="scaleY">Adjustment to the rendered height of the item, in arbitrary units determined by RISING_RENDER_MAX</param>
        protected void translateMesh(MeshData mesh, int index, bool isWood, bool isLargeItem, float scaleY)
        {
            float x, y, z, scaleDown;

            if (isWood)
            {
                if (!woodrandDone) WoodRandomiserSetup();
                scaleDown = 0.46f;
                scaleY = 1f;
                float deg = (woodrand[index - itemCapacity] - 4) * 0.6f;
                float offsetRandom = (woodrand[fuelitemCapacity - 1 - index + itemCapacity] - 4) / 256f;
                if (index < itemCapacity + 3)
                {
                    x = 13 / 32f + offsetRandom;
                    y = -1.49f / 16f;
                    z = ((index - 5) * 3 + 8) / 16f;
                    deg += 90f;
                    mesh.Rotate(centre, 0, deg * GameMath.DEG2RAD, 0);
                }
                else
                {
                    x = ((8 - index) * 3 + 7) / 16f;
                    y = 0.31f / 16f;
                    z = 16 / 32f + offsetRandom;
                    mesh.Rotate(centre, 0, deg * GameMath.DEG2RAD, 0);
                }
            }
            else
            {
                // Standard size baked goods e.g. dough
                woodrandDone = false;
                x = (index % 2 == 0) ? 21 / 32f : 11 / 32f;
                y = 1.01f / 16f;
                z = (index > 1) ? 42 / 64f : 22 / 64f;
                if (isLargeItem)
                {
                    x = 0.5f;
                    z = 0.5f;
                }

                scaleDown = 0.78f;
            }

            mesh.Scale(centre, scaleDown, scaleDown * scaleY, scaleDown);
            mesh.Translate(x - 0.5f, y, z - 0.5f);
            if (this.rotation > 0) mesh.Rotate(centre, 0, rotation * GameMath.DEG2RAD, 0);
        }

        protected virtual void WoodRandomiserSetup()
        {
            Random rng = new Random(this.Pos.GetHashCode());
            for (int i = 0; i < fuelitemCapacity; i++)
            {
                woodrand[i] = rng.Next(0, 9);
            }
            woodrandDone = true;
        }

        public virtual void RenderParticleTick(IAsyncParticleManager manager, BlockPos pos, float windAffectednessAtPos, float secondsTicking, AdvancedParticleProperties[] particles)
        {
            if (this.fuelBurnTime < 3) return;
            int logsCount = FuelSlot.StackSize;
            bool fireFull = logsCount > 3;  //fireFull means it's a fire with 2 rows of logs (between 4 and 6 logs) - flames will start higher
            double[] x = new double[4];
            float[] z = new float[4];
            for (int i = 0; i < particles.Length; i++)
            {
                //reduced number of bright yellow flames - and reduces as the fuel burns
                if (i >= 12 && prng.Next(0, 90) > this.fuelBurnTime) continue;

                //limit orange flames when fuel is almost burned
                if (i >= 8 && i < 12 && prng.Next(0, 12) > this.fuelBurnTime) continue;

                //also randomise red flames
                if (i >= 4 && i < 4 && prng.Next(0, 6) == 0) continue;

                //adjust flames to the number of logs, if less than 3 logs
                if (i >= 4 && logsCount < 3)
                {
                    bool rotated = this.rotation >= 180;
                    if (!rotated && z[i % 2] > logsCount * 0.2f + 0.14f) continue;
                    if (rotated && z[i % 2] < (3 - logsCount) * 0.2f + 0.14f) continue;
                }

                AdvancedParticleProperties bps = particles[i];
                bps.WindAffectednesAtPos = 0f;
                bps.basePos.X = pos.X;
                bps.basePos.Y = pos.Y + (fireFull ? 3 / 32f : 1 / 32f);
                bps.basePos.Z = pos.Z;

                //i >= 4 is flames; i < 4 is smoke
                if (i >= 4)
                {
                    bool rotate = this.rotation % 180 > 0;
                    if (fireFull) rotate = !rotate;
                    bps.basePos.Z += rotate ? x[i % 2] : z[i % 2];
                    bps.basePos.X += rotate ? z[i % 2] : x[i % 2];
                    bps.basePos.Y += (fireFull ? 4 : 3) / 32f;
                    switch (this.rotation)
                    {
                        case 0:
                            bps.basePos.X -= fireFull ? 0.08f : 0.12f;
                            break;
                        case 180:
                            bps.basePos.X += fireFull ? 0.08f : 0.12f;
                            break;
                        case 90:
                            bps.basePos.Z += fireFull ? 0.08f : 0.12f;
                            break;
                        default:
                            bps.basePos.Z -= fireFull ? 0.08f : 0.12f;
                            break;
                    }
                }
                else
                //set up flame positions with RNG (this way all three flame evolution particles will be in approx. same position)
                {
                    x[i] = prng.NextDouble() * 0.4f + 0.33f;   // the multiplier and offset gets the flame position aligned with the top surface of the logs
                    z[i] = 0.26f + prng.Next(0, 3) * 0.2f + (float)prng.NextDouble() * 0.08f;
                }

                manager.Spawn(bps);
            }
        }

        #endregion

    }
}