using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public enum EnumVoxelMaterial
    {
        Empty = 0,
        Metal = 1,
        Slag = 2,
        Placeholder1 = 3,
    }

    public class BlockEntityAnvil : BlockEntity
    {
        #region Particle
        
        public static SimpleParticleProperties bigMetalSparks;
        public static SimpleParticleProperties smallMetalSparks;
        public static SimpleParticleProperties slagPieces;

        static BlockEntityAnvil()
        {
            smallMetalSparks = new SimpleParticleProperties(
                2, 5,
                ColorUtil.ToRgba(255, 255, 233, 83),
                new Vec3d(), new Vec3d(),
                new Vec3f(-3f, 8f, -3f),
                new Vec3f(3f, 12f, 3f),
                0.1f,
                1f,
                0.25f, 0.25f,
                EnumParticleModel.Quad
            );
            smallMetalSparks.VertexFlags = 128;
            smallMetalSparks.AddPos.Set(1 / 16f, 0, 1 / 16f);
            smallMetalSparks.SizeEvolve = new EvolvingNatFloat(EnumTransformFunction.LINEAR, -0.05f);
            smallMetalSparks.ParticleModel = EnumParticleModel.Quad;
            smallMetalSparks.LifeLength = 0.03f;
            smallMetalSparks.MinVelocity = new Vec3f(-1f, 1f, -1f);
            smallMetalSparks.AddVelocity = new Vec3f(2f, 2f, 2f);
            smallMetalSparks.MinQuantity = 4;
            smallMetalSparks.AddQuantity = 6;
            smallMetalSparks.MinSize = 0.1f;
            smallMetalSparks.MaxSize = 0.1f;
            smallMetalSparks.SizeEvolve = new EvolvingNatFloat(EnumTransformFunction.LINEAR, -0.1f);



           


            bigMetalSparks = new SimpleParticleProperties(
                2, 8,
                ColorUtil.ToRgba(255, 255, 233, 83),
                new Vec3d(), new Vec3d(),
                new Vec3f(-1f, 0.5f, -1f),
                new Vec3f(2f, 1.5f, 2f),
                0.5f,
                1f,
                0.25f, 0.25f
            );
            bigMetalSparks.VertexFlags = 128;
            bigMetalSparks.AddPos.Set(1 / 16f, 0, 1 / 16f);
            bigMetalSparks.SizeEvolve = new EvolvingNatFloat(EnumTransformFunction.LINEAR, -0.25f);



            slagPieces = new SimpleParticleProperties(
                2, 12,
                ColorUtil.ToRgba(255, 255, 233, 83),
                new Vec3d(), new Vec3d(),
                new Vec3f(-1f, 0.5f, -1f),
                new Vec3f(2f, 1.5f, 2f),
                0.5f,
                1f,
                0.25f, 0.5f
            );
            slagPieces.AddPos.Set(1 / 16f, 0, 1 / 16f);
            slagPieces.SizeEvolve = new EvolvingNatFloat(EnumTransformFunction.LINEAR, -0.25f);

        }
        #endregion

        // Permanent data
        ItemStack workItemStack;
        int selectedRecipeId = -1;
        //public int AvailableMetalVoxels;

        public byte[,,] Voxels = new byte[16, 6, 16]; // Only the first 2 bits of each byte are used and serialized

        float voxYOff = 10 / 16f;


        // Temporary data, generated on be creation

        Dictionary<string, MetalPropertyVariant> metalsByCode;
        /// <summary>
        /// The base material used for the work item, used to check melting point
        /// </summary>
        ItemStack baseMaterial;
        
        Cuboidf[] selectionBoxes = new Cuboidf[1];
        public int OwnMetalTier;
        AnvilWorkItemRenderer workitemRenderer;
        public int rotation = 0;
        public float MeshAngle;
        MeshData currentMesh;

        public bool [,,] recipeVoxels
        {
            get
            {
                if (SelectedRecipe == null) return null;

                bool[,,] origVoxels = SelectedRecipe.Voxels;
                bool[,,] rotVoxels = new bool[origVoxels.GetLength(0), origVoxels.GetLength(1), origVoxels.GetLength(2)];

                if (rotation == 0) return origVoxels;

                for (int i = 0; i < rotation / 90; i++)
                {
                    for (int x = 0; x < origVoxels.GetLength(0); x++)
                    {
                        for (int y = 0; y < origVoxels.GetLength(1); y++)
                        {
                            for (int z = 0; z < origVoxels.GetLength(2); z++)
                            {
                                rotVoxels[z, y, x] = origVoxels[16 - x - 1, y, z];
                            }
                        }
                    }

                    origVoxels = (bool[,,])rotVoxels.Clone();
                }

                return rotVoxels;
            }
        }

        public SmithingRecipe SelectedRecipe
        {
            get { return Api.World.SmithingRecipes.FirstOrDefault(r => r.RecipeId == selectedRecipeId); }
        }

        public bool CanWorkCurrent
        {
            get { return workItemStack != null && CanWork(workItemStack); }
        }

        public bool IsIronBloom
        {
            get { return workItemStack?.Collectible?.FirstCodePart().Equals("ironbloom") == true; }
        }

        public ItemStack WorkItemStack
        {
            get { return workItemStack; }
        }

        public ItemStack BaseMaterial
        {
            get { return baseMaterial; }
        }

        public BlockEntityAnvil() : base() { }


        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            metalsByCode = new Dictionary<string, MetalPropertyVariant>();

            MetalProperty metals = api.Assets.TryGet("worldproperties/block/metal.json").ToObject<MetalProperty>();
            for (int i = 0; i < metals.Variants.Length; i++)
            {
                // Metals currently don't have a domain
                metalsByCode[metals.Variants[i].Code.Path] = metals.Variants[i]; 
            }

            if (workItemStack != null)
            {
                workItemStack.ResolveBlockOrItem(api.World);
                if (IsIronBloom)
                {
                    baseMaterial = new ItemStack(workItemStack.Collectible);
                } else
                {
                    baseMaterial = new ItemStack(api.World.GetItem(new AssetLocation("ingot-" + workItemStack.Collectible.LastCodePart())));
                }
                
            }

            if (api is ICoreClientAPI)
            {
                ICoreClientAPI capi = (ICoreClientAPI)api;
                capi.Event.RegisterRenderer(workitemRenderer = new AnvilWorkItemRenderer(this, Pos, capi), EnumRenderStage.Opaque);
                capi.Event.RegisterRenderer(workitemRenderer, EnumRenderStage.AfterFinalComposition);

                RegenMeshAndSelectionBoxes();
                capi.Tesselator.TesselateBlock(Block, out currentMesh);
            }

            string metalType = Block.LastCodePart();
            if (metalsByCode.ContainsKey(metalType)) OwnMetalTier = metalsByCode[metalType].Tier;
        }


        public bool CanWork(ItemStack stack)
        {
            float temperature = stack.Collectible.GetTemperature(Api.World, stack);
            float meltingpoint = stack.Collectible.GetMeltingPoint(Api.World, null, new DummySlot(baseMaterial));

            if (stack.Collectible.Attributes?["workableTemperature"].Exists == true)
            {
                return stack.Collectible.Attributes["workableTemperature"].AsFloat(meltingpoint / 2) <= temperature;
            }

            return temperature >= meltingpoint / 2;
        }


        internal Cuboidf[] GetSelectionBoxes(IBlockAccessor world, BlockPos pos)
        {
            return selectionBoxes;
        }

        internal bool OnPlayerInteract(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (byPlayer.InventoryManager.ActiveHotbarSlot.Itemstack?.Collectible.Tool == EnumTool.Hammer)
            {
                return RotateWorkItem(byPlayer.Entity.Controls.Sneak);
            }

            if (byPlayer.Entity.Controls.Sneak)
            {
                return TryPut(world, byPlayer, blockSel);
            } else
            {
                return TryTake(world, byPlayer, blockSel);
            }
        }

        private bool RotateWorkItem(bool ccw)
        {
            byte[,,] rotVoxels = new byte[16, 6, 16];

            for (int x = 0; x < 16; x++)
            {
                for (int y = 0; y < 6; y++)
                {
                    for (int z = 0; z < 16; z++)
                    {
                        if (ccw)
                        {
                            rotVoxels[z, y, x] = Voxels[x, y, 16 - z - 1];
                        } else
                        {
                            rotVoxels[z, y, x] = Voxels[16 - x - 1, y, z];
                        }
                        
                    }
                }
            }

            rotation = (rotation + 90) % 360;

            this.Voxels = rotVoxels;
            RegenMeshAndSelectionBoxes();
            MarkDirty();

            return true;
        }

        private bool TryTake(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (workItemStack == null) return false;

            workItemStack.Attributes.SetBytes("voxels", serializeVoxels(Voxels));
            //workItemStack.Attributes.SetInt("availableVoxels", AvailableMetalVoxels);
            workItemStack.Attributes.SetInt("selectedRecipeId", selectedRecipeId);

            if (workItemStack.Collectible is ItemIronBloom bloomItem)
            {
                workItemStack.Attributes.SetInt("hashCode", bloomItem.GetWorkItemHashCode(workItemStack));
            }

            if (!byPlayer.InventoryManager.TryGiveItemstack(workItemStack))
            {
                Api.World.SpawnItemEntity(workItemStack, Pos.ToVec3d().Add(0.5, 0.5, 0.5));
            }

            workItemStack = null;
            Voxels = new byte[16, 6, 16];
            //AvailableMetalVoxels = 0;

            RegenMeshAndSelectionBoxes();
            MarkDirty();
            rotation = 0;

            return true;
        }


        private bool TryPut(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            ItemSlot slot = byPlayer.InventoryManager.ActiveHotbarSlot;
            if (slot.Itemstack == null) return false;

            ItemStack stack = slot.Itemstack;

            string metalType = stack.Collectible.LastCodePart();
            bool viableTier = metalsByCode.ContainsKey(metalType) && metalsByCode[metalType].Tier <= OwnMetalTier + 1;
            bool viableIngot = stack.Collectible is ItemIngot && CanWork(stack) && viableTier;

            // Place ingot
            if (viableIngot && (workItemStack == null || workItemStack.Collectible.LastCodePart().Equals(stack.Collectible.LastCodePart())))
            {
                if (workItemStack == null)
                {
                    if (world is IClientWorldAccessor)
                    {
                        OpenDialog(stack);
                    }

                    CreateVoxelsFromIngot();

                    workItemStack = new ItemStack(Api.World.GetItem(new AssetLocation("workitem-" + stack.Collectible.LastCodePart())));
                    workItemStack.Collectible.SetTemperature(Api.World, workItemStack, stack.Collectible.GetTemperature(Api.World, stack));

                    baseMaterial = new ItemStack(Api.World.GetItem(new AssetLocation("ingot-" + stack.Collectible.LastCodePart())));

                    List<SmithingRecipe> recipes = Api.World.SmithingRecipes
                        .Where(r => r.Ingredient.SatisfiesAsIngredient(baseMaterial))
                        .OrderBy(r => r.Output.ResolvedItemstack.Collectible.Code)
                        .ToList()
                    ;

                    selectedRecipeId = recipes[0].RecipeId;
                    //AvailableMetalVoxels += 16;
                } else
                {
                    AddVoxelsFromIngot();

                    //AvailableMetalVoxels += 32;
                }
                

                slot.TakeOut(1);
                slot.MarkDirty();

                RegenMeshAndSelectionBoxes();
                MarkDirty();
                return true;
            }

            // Place workitem
            bool viableWorkItem = stack.Collectible.FirstCodePart().Equals("workitem") && viableTier;
            if (viableWorkItem && workItemStack == null)
            {
                try
                {
                    Voxels = deserializeVoxels(stack.Attributes.GetBytes("voxels"));
                    //AvailableMetalVoxels = stack.Attributes.GetInt("availableVoxels");
                    selectedRecipeId = stack.Attributes.GetInt("selectedRecipeId");

                    workItemStack = stack.Clone();
                }
                catch (Exception)
                {

                }

                if (selectedRecipeId < 0 && world is IClientWorldAccessor)
                {
                    OpenDialog(stack);
                }

                slot.TakeOut(1);
                slot.MarkDirty();

                RegenMeshAndSelectionBoxes();
                CheckIfFinished(byPlayer);
                MarkDirty();
                return true;
            }

            // Place iron bloom
            bool viableBloom = stack.Collectible.FirstCodePart().Equals("ironbloom") && OwnMetalTier >= 2;
            if (viableBloom && workItemStack == null)
            {
                if (stack.Attributes.HasAttribute("voxels"))
                {
                    try
                    {
                        Voxels = deserializeVoxels(stack.Attributes.GetBytes("voxels"));
                        //AvailableMetalVoxels = stack.Attributes.GetInt("availableVoxels");
                        selectedRecipeId = stack.Attributes.GetInt("selectedRecipeId");
                    }
                    catch (Exception)
                    {
                        CreateVoxelsFromIronBloom();
                    }
                } else
                {
                    CreateVoxelsFromIronBloom();
                }


                workItemStack = stack.Clone();
                workItemStack.StackSize = 1;
                workItemStack.Collectible.SetTemperature(Api.World, workItemStack, stack.Collectible.GetTemperature(Api.World, stack));

                List<SmithingRecipe> recipes = Api.World.SmithingRecipes
                        .Where(r => r.Ingredient.SatisfiesAsIngredient(stack))
                        .OrderBy(r => r.Output.ResolvedItemstack.Collectible.Code)
                        .ToList()
                    ;

                selectedRecipeId = recipes[0].RecipeId;
                baseMaterial = stack.Clone();
                baseMaterial.StackSize = 1;
                RegenMeshAndSelectionBoxes();
                CheckIfFinished(byPlayer);

                slot.TakeOut(1);
                slot.MarkDirty();
                MarkDirty();
                return true;
            }

            return false;
        }

        internal void OnBeginUse(IPlayer byPlayer, BlockSelection blockSel)
        {
        }




        internal void OnUseOver(IPlayer byPlayer, int selectionBoxIndex)
        {
            // box index 0 is the anvil itself
            if (selectionBoxIndex <= 0 || selectionBoxIndex >= selectionBoxes.Length) return;

            Cuboidf box = selectionBoxes[selectionBoxIndex];

            Vec3i voxelPos = new Vec3i((int)(16 * box.X1), (int)(16 * box.Y1) - 10, (int)(16 * box.Z1));

            OnUseOver(byPlayer, voxelPos, new BlockSelection() { Position = Pos, SelectionBoxIndex = selectionBoxIndex });
        }


        internal void OnUseOver(IPlayer byPlayer, Vec3i voxelPos, BlockSelection blockSel)
        {
            if (voxelPos == null)
            {
                return;
            }

            // Send a custom network packet for server side, because
            // serverside blockselection index is inaccurate
            if (Api.Side == EnumAppSide.Client)
            {
                SendUseOverPacket(byPlayer, voxelPos);
            }


            ItemSlot slot = byPlayer.InventoryManager.ActiveHotbarSlot;
            if (slot.Itemstack == null || !CanWorkCurrent)
            {
                return;
            }
            int toolMode = slot.Itemstack.Collectible.GetToolMode(slot, byPlayer, blockSel);

            float yaw = GameMath.Mod(byPlayer.Entity.Pos.Yaw, 2 * GameMath.PI);
            

            EnumVoxelMaterial voxelMat = (EnumVoxelMaterial)Voxels[voxelPos.X, voxelPos.Y, voxelPos.Z];

            if (voxelMat != EnumVoxelMaterial.Empty)
            {
                spawnParticles(voxelPos, voxelMat, byPlayer);

                switch (toolMode)
                {
                    case 0: OnHit(voxelPos); break;
                    case 1: OnUpset(voxelPos, BlockFacing.NORTH.FaceWhenRotatedBy(0, yaw - GameMath.PIHALF, 0)); break;
                    case 2: OnUpset(voxelPos, BlockFacing.EAST.FaceWhenRotatedBy(0, yaw - GameMath.PIHALF, 0)); break;
                    case 3: OnUpset(voxelPos, BlockFacing.SOUTH.FaceWhenRotatedBy(0, yaw - GameMath.PIHALF, 0)); break;
                    case 4: OnUpset(voxelPos, BlockFacing.WEST.FaceWhenRotatedBy(0, yaw - GameMath.PIHALF, 0)); break;
                    case 5: OnSplit(voxelPos); break;
                }

                RegenMeshAndSelectionBoxes();
                Api.World.BlockAccessor.MarkBlockDirty(Pos);
                Api.World.BlockAccessor.MarkBlockEntityDirty(Pos);
                slot.Itemstack.Collectible.DamageItem(Api.World, byPlayer.Entity, slot);

                if (!HasAnyMetalVoxel())
                {
                    //AvailableMetalVoxels = 0;
                    workItemStack = null;
                    return;
                }
            }

            CheckIfFinished(byPlayer);
            MarkDirty();
        }

        private void spawnParticles(Vec3i voxelPos, EnumVoxelMaterial voxelMat, IPlayer byPlayer)
        {
            float temp = workItemStack.Collectible.GetTemperature(Api.World, workItemStack);

            if (voxelMat == EnumVoxelMaterial.Metal && temp > 800)
            {
                bigMetalSparks.MinPos = Pos.ToVec3d().AddCopy(voxelPos.X / 16f, voxYOff + voxelPos.Y / 16f + 0.0625f, voxelPos.Z / 16f);
                bigMetalSparks.VertexFlags = (byte)GameMath.Clamp((int)(temp - 700) / 2, 32, 128);
                Api.World.SpawnParticles(bigMetalSparks, byPlayer);

                smallMetalSparks.MinPos = Pos.ToVec3d().AddCopy(voxelPos.X / 16f, voxYOff + voxelPos.Y / 16f + 0.0625f, voxelPos.Z / 16f);
                smallMetalSparks.VertexFlags = (byte)GameMath.Clamp((int)(temp - 770) / 3, 32, 128);
                Api.World.SpawnParticles(smallMetalSparks, byPlayer);
            }

            if (voxelMat == EnumVoxelMaterial.Slag)
            {
                slagPieces.Color = workItemStack.Collectible.GetRandomColor(Api as ICoreClientAPI, workItemStack);
                slagPieces.MinPos = Pos.ToVec3d().AddCopy(voxelPos.X / 16f, voxYOff + voxelPos.Y / 16f + 0.0625f, voxelPos.Z / 16f);

                Api.World.SpawnParticles(slagPieces, byPlayer);
            }
        }


        public virtual void OnHelveHammerHit()
        {
            if (workItemStack == null || !CanWorkCurrent) return;

            SmithingRecipe recipe = SelectedRecipe;

            // Helve hammer can only work plates and iron bloom
            if (!recipe.Output.Code.Path.Contains("plate") && !IsIronBloom) return;

            rotation = 0;
            int ymax = recipe.QuantityLayers;
            Vec3i usableMetalVoxel;
            if (!IsIronBloom)
            {
                usableMetalVoxel = findFreeMetalVoxel();

                if (usableMetalVoxel != null)
                {
                    
                    for (int x = 0; x < 16; x++)
                    {
                        for (int z = 0; z < 16; z++)
                        {
                            for (int y = 0; y < 5; y++)
                            {
                                bool requireMetalHere = y >= ymax ? false : recipe.Voxels[x, y, z];

                                EnumVoxelMaterial mat = (EnumVoxelMaterial)Voxels[x, y, z];

                                if (requireMetalHere && mat == EnumVoxelMaterial.Empty)
                                {
                                    Voxels[x, y, z] = (byte)EnumVoxelMaterial.Metal;
                                    Voxels[usableMetalVoxel.X, usableMetalVoxel.Y, usableMetalVoxel.Z] = (byte)EnumVoxelMaterial.Empty;

                                    if (Api.World.Side == EnumAppSide.Client)
                                    {
                                        spawnParticles(new Vec3i(x, y, z), mat == EnumVoxelMaterial.Empty ? EnumVoxelMaterial.Metal : mat, null);
                                        spawnParticles(usableMetalVoxel, EnumVoxelMaterial.Metal, null);
                                    }
                                    RegenMeshAndSelectionBoxes();
                                    CheckIfFinished(null);
                                    return;
                                }

                            }
                        }
                    }

                    Voxels[usableMetalVoxel.X, usableMetalVoxel.Y, usableMetalVoxel.Z] = (byte)EnumVoxelMaterial.Empty;
                    if (Api.World.Side == EnumAppSide.Client)
                    {
                        spawnParticles(usableMetalVoxel, EnumVoxelMaterial.Metal, null);
                    }
                    RegenMeshAndSelectionBoxes();
                    CheckIfFinished(null);
                }
            }
            else
            {

                for (int y = 5; y >= 0; y--)
                {
                    for (int z = 0; z < 16; z++)
                    {
                        for (int x = 0; x < 16; x++)
                        {
                            bool requireMetalHere = y >= ymax ? false : recipe.Voxels[x, y, z];

                            EnumVoxelMaterial mat = (EnumVoxelMaterial)Voxels[x, y, z];

                            if (requireMetalHere && mat == EnumVoxelMaterial.Metal) continue;
                            if (!requireMetalHere && mat == EnumVoxelMaterial.Empty) continue;

                            if (Api.World.Side == EnumAppSide.Client)
                            {
                                spawnParticles(new Vec3i(x, y, z), mat == EnumVoxelMaterial.Empty ? EnumVoxelMaterial.Metal : mat, null);
                            }

                            if (requireMetalHere && mat == EnumVoxelMaterial.Empty)
                            {
                                Voxels[x, y, z] = (byte)EnumVoxelMaterial.Metal;
                            }
                            else
                            {
                                Voxels[x, y, z] = (byte)EnumVoxelMaterial.Empty;
                            }

                            RegenMeshAndSelectionBoxes();
                            CheckIfFinished(null);

                            return;
                        }
                    }
                }
            }


        }

        private Vec3i findFreeMetalVoxel()
        {
            SmithingRecipe recipe = SelectedRecipe;
            int ymax = recipe.QuantityLayers;

            for (int y = 5; y >= 0; y--)
            {
                for (int z = 0; z < 16; z++)
                {
                    for (int x = 0; x < 16; x++)
                    {
                        bool requireMetalHere = y >= ymax ? false : recipe.Voxels[x, y, z];
                        EnumVoxelMaterial mat = (EnumVoxelMaterial)Voxels[x, y, z];

                        if (!requireMetalHere && mat == EnumVoxelMaterial.Metal) return new Vec3i(x, y, z);
                    }
                }
            }

            return null;
        }

        public virtual void CheckIfFinished(IPlayer byPlayer)
        {
            if (MatchesRecipe() && Api.World is IServerWorldAccessor)
            {   
                Voxels = new byte[16, 6, 16];
                //AvailableMetalVoxels = 0;
                ItemStack outstack = SelectedRecipe.Output.ResolvedItemstack.Clone();
                outstack.Collectible.SetTemperature(Api.World, outstack, workItemStack.Collectible.GetTemperature(Api.World, workItemStack));
                workItemStack = null;
                
                selectedRecipeId = -1;

                if (byPlayer == null || !byPlayer.InventoryManager.TryGiveItemstack(outstack))
                {
                    Api.World.SpawnItemEntity(outstack, Pos.ToVec3d().Add(0.5, 0.5, 0.5));
                }

                RegenMeshAndSelectionBoxes();
                MarkDirty();
                Api.World.BlockAccessor.MarkBlockDirty(Pos);
                rotation = 0;
            }
        }

        private bool MatchesRecipe()
        {
            if (SelectedRecipe == null) return false;

            int ymax = Math.Min(6, SelectedRecipe.QuantityLayers);

            for (int x = 0; x < 16; x++)
            {
                for (int y = 0; y < ymax; y++)
                {
                    for (int z = 0; z < 16; z++)
                    {
                        byte desiredMat = (byte)(recipeVoxels[x, y, z] ? EnumVoxelMaterial.Metal : EnumVoxelMaterial.Empty);

                        if (Voxels[x, y, z] != desiredMat)
                        {
                            return false;
                        }
                    }
                }
            }

            return true;
        }

 

        bool HasAnyMetalVoxel()
        {
            for (int x = 0; x < 16; x++)
            {
                for (int y = 0; y < 6; y++)
                {
                    for (int z = 0; z < 16; z++)
                    {
                        if (Voxels[x, y, z] == (byte)EnumVoxelMaterial.Metal) return true;
                    }
                }
            }

            return false;
        }



        public virtual void OnSplit(Vec3i voxelPos)
        {
            if (Voxels[voxelPos.X, voxelPos.Y, voxelPos.Z] == (byte)EnumVoxelMaterial.Slag)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    for (int dz = -1; dz <= 1; dz++)
                    {
                        int x = voxelPos.X + dx;
                        int z = voxelPos.Z + dz;

                        if (x < 0 || z < 0 || x >= 16 || z >= 16) continue;

                        if (Voxels[x, voxelPos.Y, z] == (byte)EnumVoxelMaterial.Slag)
                        {
                            Voxels[x, voxelPos.Y, z] = 0;
                        }

                    }
                }
            }

            Voxels[voxelPos.X, voxelPos.Y, voxelPos.Z] = 0;



        }

        public virtual void OnUpset(Vec3i voxelPos, BlockFacing towardsFace)
        {
            if (Voxels[voxelPos.X, voxelPos.Y, voxelPos.Z] != (byte)EnumVoxelMaterial.Metal) return;

            Vec3i npos = voxelPos.Clone().Add(towardsFace);
            Vec3i opFaceDir = towardsFace.GetOpposite().Normali;

            if (npos.X < 0 || npos.X >= 16 || npos.Y < 0 || npos.Y >= 6 || npos.Z < 0 || npos.Z >= 16) return;

            if (voxelPos.Y > 0)
            {
                if (Voxels[npos.X, npos.Y, npos.Z] == (byte)EnumVoxelMaterial.Empty && Voxels[npos.X, npos.Y - 1, npos.Z] != (byte)EnumVoxelMaterial.Empty)
                {
                    if (npos.X < 0 || npos.X >= 16 || npos.Y < 0 || npos.Y >= 6 || npos.Z < 0 || npos.Z >= 16) return;

                    Voxels[npos.X, npos.Y, npos.Z] = (byte)EnumVoxelMaterial.Metal;
                    Voxels[voxelPos.X, voxelPos.Y, voxelPos.Z] = 0;
                    return;
                }
                else
                {
                    npos.Y++;
                    
                    if (npos.Y < 6 && Voxels[npos.X, npos.Y, npos.Z] == (byte)EnumVoxelMaterial.Empty && Voxels[npos.X, npos.Y - 1, npos.Z] != (byte)EnumVoxelMaterial.Empty && Voxels[voxelPos.X + opFaceDir.X, voxelPos.Y, voxelPos.Z + opFaceDir.Z] == (byte)EnumVoxelMaterial.Empty)
                    {
                        Voxels[npos.X, npos.Y, npos.Z] = (byte)EnumVoxelMaterial.Metal;
                        Voxels[voxelPos.X, voxelPos.Y, voxelPos.Z] = (byte)EnumVoxelMaterial.Empty;
                        return;
                    }

                    if (!moveVoxelDownwards(voxelPos.Clone(), towardsFace, 1))
                    {
                        moveVoxelDownwards(voxelPos.Clone(), towardsFace, 2);
                    }
                }
                return;
            }
            

            
            /*if (Voxels[npos.X, npos.Y, npos.Z] == (byte)EnumVoxelMaterial.Empty)
            {
                if (AvailableMetalVoxels > 0)
                {
                    Voxels[npos.X, npos.Y, npos.Z] = (byte)EnumVoxelMaterial.Metal;
                    AvailableMetalVoxels--;
                }
                return;
            }*/


            npos.Y++;

            if (npos.X < 0 || npos.X >= 16 || npos.Y < 0 || npos.Y >= 6 || npos.Z < 0 || npos.Z >= 16) return;
            if (voxelPos.X + opFaceDir.X < 0 || voxelPos.X + opFaceDir.X >= 16 || voxelPos.Z + opFaceDir.Z < 0 || voxelPos.Z + opFaceDir.Z >= 16) return;

            if (npos.Y < 6 && Voxels[npos.X, npos.Y, npos.Z] == (byte)EnumVoxelMaterial.Empty && Voxels[npos.X, npos.Y - 1, npos.Z] != (byte)EnumVoxelMaterial.Empty && Voxels[voxelPos.X + opFaceDir.X, voxelPos.Y, voxelPos.Z + opFaceDir.Z] == (byte)EnumVoxelMaterial.Empty)
            {
                Voxels[npos.X, npos.Y, npos.Z] = (byte)EnumVoxelMaterial.Metal;
                Voxels[voxelPos.X, voxelPos.Y, voxelPos.Z] = (byte)EnumVoxelMaterial.Empty;
                return;
            }

        }

        private Vec3i getClosestBfs(Vec3i voxelPos, BlockFacing towardsFace, int maxDist)
        {
            Queue<Vec3i> nodesToVisit = new Queue<Vec3i>();
            HashSet<Vec3i> nodesVisited = new HashSet<Vec3i>();

            nodesToVisit.Enqueue(voxelPos);

            while (nodesToVisit.Count > 0)
            {
                Vec3i node = nodesToVisit.Dequeue();

                for (int i = 0; i < BlockFacing.HORIZONTALS.Length; i++)
                {
                    BlockFacing face = BlockFacing.HORIZONTALS[i];
                    Vec3i nnode = node.Clone().Add(face);

                    if (nnode.X < 0 || nnode.X >= 16 || nnode.Y < 0 || nnode.Y >= 6 || nnode.Z < 0 || nnode.Z >= 16) continue;
                    if (nodesVisited.Contains(nnode)) continue;
                    nodesVisited.Add(nnode);

                    double x = nnode.X - voxelPos.X;
                    double z = nnode.Z - voxelPos.Z;
                    double len = GameMath.Sqrt(x * x + z * z);

                    if (len > maxDist) continue;

                    x /= len;
                    z /= len;

                    if (towardsFace == null || Math.Abs((float)Math.Acos(towardsFace.Normalf.X * x + towardsFace.Normalf.Z * z)) < 25 * GameMath.DEG2RAD)
                    {
                        if (Voxels[nnode.X, nnode.Y, nnode.Z] == (byte)EnumVoxelMaterial.Empty)
                        {
                            return nnode;
                        }
                    }

                    if (Voxels[nnode.X, nnode.Y, nnode.Z] == (byte)EnumVoxelMaterial.Metal)
                    {
                        nodesToVisit.Enqueue(nnode);
                    }
                }
            }

            return null;
        }

        public virtual void OnHit(Vec3i voxelPos)
        {
            if (Voxels[voxelPos.X, voxelPos.Y, voxelPos.Z] != (byte)EnumVoxelMaterial.Metal) return;

            if (voxelPos.Y > 0)
            {
                int voxelsMoved = 0;

                for (int dx = -1; dx <= 1; dx++)
                {
                    for (int dz = -1; dz <=1; dz++)
                    {
                        if (dx == 0 && dz == 0) continue;
                        if (voxelPos.X + dx < 0 || voxelPos.X + dx >= 16 || voxelPos.Z + dz < 0 || voxelPos.Z + dz >= 16) continue;

                        if (Voxels[voxelPos.X + dx, voxelPos.Y, voxelPos.Z + dz] == (byte)EnumVoxelMaterial.Metal)
                        {
                            voxelsMoved += moveVoxelDownwards(voxelPos.Clone().Add(dx, 0, dz), null, 1) ? 1 : 0;
                        }
                    }
                }

                if (Voxels[voxelPos.X, voxelPos.Y, voxelPos.Z] == (byte)EnumVoxelMaterial.Metal)
                {
                    voxelsMoved += moveVoxelDownwards(voxelPos.Clone(), null, 1) ? 1 : 0;
                }


                if (voxelsMoved == 0)
                {
                    Vec3i emptySpot=null;

                    for (int dx = -1; dx <= 1; dx++)
                    {
                        for (int dz = -1; dz <= 1; dz++)
                        {
                            if (dx == 0 && dz == 0) continue;
                            if (voxelPos.X + 2*dx < 0 || voxelPos.X + 2*dx >= 16 || voxelPos.Z + 2*dz < 0 || voxelPos.Z + 2*dz >= 16) continue;

                            bool spotEmpty = Voxels[voxelPos.X + 2 * dx, voxelPos.Y, voxelPos.Z + 2 * dz] == (byte)EnumVoxelMaterial.Empty;

                            if (Voxels[voxelPos.X + dx, voxelPos.Y, voxelPos.Z + dz] == (byte)EnumVoxelMaterial.Metal && spotEmpty)
                            {
                                Voxels[voxelPos.X + dx, voxelPos.Y, voxelPos.Z + dz] = (byte)EnumVoxelMaterial.Empty;

                                if (Voxels[voxelPos.X + 2 * dx, voxelPos.Y - 1, voxelPos.Z + 2 * dz] == (byte)EnumVoxelMaterial.Empty)
                                {
                                    Voxels[voxelPos.X + 2 * dx, voxelPos.Y - 1, voxelPos.Z + 2 * dz] = (byte)EnumVoxelMaterial.Metal;
                                } else
                                {
                                    Voxels[voxelPos.X + 2 * dx, voxelPos.Y, voxelPos.Z + 2 * dz] = (byte)EnumVoxelMaterial.Metal;
                                }
                                
                            } else
                            {
                                if (spotEmpty) emptySpot = voxelPos.Clone().Add(dx, 0, dz);
                            }
                        }
                    }

                    if (emptySpot != null && Voxels[voxelPos.X, voxelPos.Y, voxelPos.Z] == (byte)EnumVoxelMaterial.Metal) 
                    {
                        Voxels[voxelPos.X, voxelPos.Y, voxelPos.Z] = (byte)EnumVoxelMaterial.Empty;

                        if (Voxels[emptySpot.X, emptySpot.Y - 1, emptySpot.Z] == (byte)EnumVoxelMaterial.Empty)
                        {
                            Voxels[emptySpot.X, emptySpot.Y - 1, emptySpot.Z] = (byte)EnumVoxelMaterial.Metal;
                        } else
                        {
                            Voxels[emptySpot.X, emptySpot.Y, emptySpot.Z] = (byte)EnumVoxelMaterial.Metal;
                        }

                        
                    }
                }
            }
        }


        bool moveVoxelDownwards(Vec3i voxelPos, BlockFacing towardsFace, int maxDist)
        {
            int origy = voxelPos.Y;

            while (voxelPos.Y > 0)
            {
                voxelPos.Y--;

                Vec3i spos = getClosestBfs(voxelPos, towardsFace, maxDist);
                if (spos == null) continue;

                Voxels[voxelPos.X, origy, voxelPos.Z] = (byte)EnumVoxelMaterial.Empty;

                for (int y = 0; y <= spos.Y; y++)
                {
                    if (Voxels[spos.X, y, spos.Z] == (byte)EnumVoxelMaterial.Empty)
                    {
                        Voxels[spos.X, y, spos.Z] = (byte)EnumVoxelMaterial.Metal;
                        return true;
                    }
                }
                
                return true;
            }

            return false;
        }

        void RegenMeshAndSelectionBoxes()
        {
            if (workitemRenderer != null)
            {
                workitemRenderer.RegenMesh(workItemStack, Voxels, recipeVoxels);
            }

            List<Cuboidf> boxes = new List<Cuboidf>();
            boxes.Add(null);

            for (int x = 0; x < 16; x++)
            {
                for (int y = 0; y < 6; y++)
                {
                    for (int z = 0; z < 16; z++)
                    {
                        if (Voxels[x, y, z] != (byte)EnumVoxelMaterial.Empty)
                        {
                            float py = y + 10;
                            boxes.Add(new Cuboidf(x / 16f, py / 16f, z / 16f, x / 16f + 1 / 16f, py / 16f + 1 / 16f, z / 16f + 1 / 16f));
                        }
                    }
                }
            }

            selectionBoxes = boxes.ToArray();
        }


        private void CreateVoxelsFromIngot()
        {
            Voxels = new byte[16, 6, 16];

            for (int x = 0; x < 7; x++)
            {
                for (int y = 0; y < 2; y++)
                {
                    for (int z = 0; z < 3; z++)
                    {
                        Voxels[4 + x, y, 6 + z] = (byte)EnumVoxelMaterial.Metal;
                    }
                    
                }
            }
        }

        private void AddVoxelsFromIngot()
        {
            for (int x = 0; x < 7; x++)
            {
                for (int z = 0; z < 3; z++)
                {
                    int y = 0;
                    int added = 0;
                    while (y < 6 && added < 2)
                    {
                        if (Voxels[4 + x, y, 6 + z] == (byte)EnumVoxelMaterial.Empty)
                        {
                            Voxels[4 + x, y, 6 + z] = (byte)EnumVoxelMaterial.Metal;
                            added++;
                        }

                        y++;
                    }
                }
            }
        }


        private void CreateVoxelsFromIronBloom()
        {
            CreateVoxelsFromIngot();

            Random rand = Api.World.Rand;

            for (int dx = -1; dx < 8; dx++)
            {
                for (int y = 0; y < 5; y++)
                {
                    for (int dz = -1; dz < 5; dz++)
                    {
                        int x = 4 + dx;
                        int z = 6 + dz;

                        if (y == 0 && Voxels[x, y, z] == (byte)EnumVoxelMaterial.Metal) continue;

                        float dist = Math.Max(0, Math.Abs(x - 7) - 1) + Math.Max(0, Math.Abs(z - 8) - 1) + Math.Max(0, y - 1f);

                        if (rand.NextDouble() < dist/3f - 0.4f + (y-1.5f)/4f)
                        {
                            continue;
                        }

                        if (rand.NextDouble() > dist/2f)
                        {
                            Voxels[x, y, z] = (byte)EnumVoxelMaterial.Metal;
                        } else
                        {
                            Voxels[x, y, z] = (byte)EnumVoxelMaterial.Slag;
                        }
                    }
                }
            }
        }


        public override void OnBlockRemoved()
        {
            workitemRenderer?.Dispose();
            workitemRenderer = null;
        }

        public override void OnBlockBroken()
        {
            if (workItemStack != null)
            {
                workItemStack.Attributes.SetBytes("voxels", serializeVoxels(Voxels));
                //workItemStack.Attributes.SetInt("availableVoxels", AvailableMetalVoxels);
                workItemStack.Attributes.SetInt("selectedRecipeId", selectedRecipeId);

                Api.World.SpawnItemEntity(workItemStack, Pos.ToVec3d().Add(0.5, 0.5, 0.5));
            }
        }


        public override void FromTreeAtributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAtributes(tree, worldForResolving);
            Voxels = deserializeVoxels(tree.GetBytes("voxels"));
            workItemStack = tree.GetItemstack("workItemStack");
            //AvailableMetalVoxels = tree.GetInt("availableVoxels");
            selectedRecipeId = tree.GetInt("selectedRecipeId", -1);
            rotation = tree.GetInt("rotation");

            if (Api != null && workItemStack != null)
            {
                workItemStack.ResolveBlockOrItem(Api.World);

                if (IsIronBloom)
                {
                    baseMaterial = new ItemStack(workItemStack.Collectible);
                }
                else
                {
                    baseMaterial = new ItemStack(Api.World.GetItem(new AssetLocation("ingot-" + workItemStack.Collectible.LastCodePart())));
                }
            }

            RegenMeshAndSelectionBoxes();

            MeshAngle = tree.GetFloat("meshAngle", MeshAngle);

            if (Api?.Side == EnumAppSide.Client)
            {
                MeshData newMesh;
                ((ICoreClientAPI)Api).Tesselator.TesselateBlock(Block, out newMesh);

                currentMesh = newMesh; // Needed so we don't get race conditions

                MarkDirty(true);
            }
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            tree.SetBytes("voxels", serializeVoxels(Voxels));
            tree.SetItemstack("workItemStack", workItemStack);
            //tree.SetInt("availableVoxels", AvailableMetalVoxels);
            tree.SetInt("selectedRecipeId", selectedRecipeId);
            tree.SetInt("rotation", rotation);

            tree.SetFloat("meshAngle", MeshAngle);
        }


        static int bitsPerByte = 2;
        static int partsPerByte = 8 / bitsPerByte;

        byte[] serializeVoxels(byte[,,] voxels)
        {
            byte[] data = new byte[16 * 6 * 16 / partsPerByte];
            int pos = 0;

            for (int x = 0; x < 16; x++)
            {
                for (int y = 0; y < 6; y++)
                {
                    for (int z = 0; z < 16; z++)
                    {
                        int bitpos = bitsPerByte * (pos % partsPerByte);
                        data[pos / partsPerByte] |= (byte)((voxels[x, y, z] & 0x3) << bitpos);
                        pos++;
                    }
                }
            }

            return data;
        }

        byte[,,] deserializeVoxels(byte[] data)
        {
            byte[,,] voxels = new byte[16, 6, 16];

            if (data == null || data.Length < 16 * 6 * 16 / partsPerByte) return voxels;

            int pos = 0;

            for (int x = 0; x < 16; x++)
            {
                for (int y = 0; y < 6; y++)
                {
                    for (int z = 0; z < 16; z++)
                    {
                        int bitpos = bitsPerByte * (pos % partsPerByte);
                        voxels[x, y, z] = (byte)((data[pos / partsPerByte] >> bitpos) & 0x3);

                        pos++;
                    }
                }
            }

            return voxels;
        }



        protected void SendUseOverPacket(IPlayer byPlayer, Vec3i voxelPos)
        {
            byte[] data;

            using (MemoryStream ms = new MemoryStream())
            {
                BinaryWriter writer = new BinaryWriter(ms);
                writer.Write(voxelPos.X);
                writer.Write(voxelPos.Y);
                writer.Write(voxelPos.Z);
                data = ms.ToArray();
            }

            ((ICoreClientAPI)Api).Network.SendBlockEntityPacket(
                Pos.X, Pos.Y, Pos.Z,
                (int)EnumAnvilPacket.OnUserOver,
                data
            );
        }


        public override void OnReceivedClientPacket(IPlayer player, int packetid, byte[] data)
        {
            if (packetid == (int)EnumAnvilPacket.SelectRecipe)
            {
                int recipeid = SerializerUtil.Deserialize<int>(data);
                SmithingRecipe recipe = Api.World.SmithingRecipes.FirstOrDefault(r => r.RecipeId == recipeid);

                if (recipe == null)
                {
                    Api.World.Logger.Error("Client tried to selected smithing recipe with id {0}, but no such recipe exists!");
                    return;
                }

                selectedRecipeId = recipe.RecipeId;

                // Tell server to save this chunk to disk again
                MarkDirty();
                Api.World.BlockAccessor.GetChunkAtBlockPos(Pos.X, Pos.Y, Pos.Z).MarkModified();
            }

            if (packetid == (int)EnumAnvilPacket.OnUserOver)
            {
                Vec3i voxelPos;
                using (MemoryStream ms = new MemoryStream(data))
                {
                    BinaryReader reader = new BinaryReader(ms);
                    voxelPos = new Vec3i(reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32());
                }

                OnUseOver(player, voxelPos, new BlockSelection() { Position = Pos });
            }
        }


        
        internal void OpenDialog(ItemStack ingredient)
        {
            if (ingredient.Collectible is ItemWorkItem)
            {
                ingredient = new ItemStack(Api.World.GetItem(new AssetLocation("ingot-" + ingredient.Collectible.LastCodePart())));
            }

            List<SmithingRecipe> recipes = Api.World.SmithingRecipes
                .Where(r => r.Ingredient.SatisfiesAsIngredient(ingredient))
                .OrderBy(r => r.Output.ResolvedItemstack.Collectible.Code) // Cannot sort by name, thats language dependent!
                .ToList()
            ;

            List<ItemStack> stacks = recipes
                .Select(r => r.Output.ResolvedItemstack)
                .ToList()
            ;

            IClientWorldAccessor clientWorld = (IClientWorldAccessor)Api.World;
            ICoreClientAPI capi = Api as ICoreClientAPI;
            
            GuiDialog dlg = new GuiDialogBlockEntityRecipeSelector(
                Lang.Get("Select smithing recipe"),
                stacks.ToArray(),
                (selectedIndex) => {
                    selectedRecipeId = recipes[selectedIndex].RecipeId;
                    capi.Network.SendBlockEntityPacket(Pos.X, Pos.Y, Pos.Z, (int)EnumClayFormingPacket.SelectRecipe, SerializerUtil.Serialize(recipes[selectedIndex].RecipeId));
                },
                () => {
                    capi.Network.SendBlockEntityPacket(Pos.X, Pos.Y, Pos.Z, (int)EnumClayFormingPacket.CancelSelect);
                },
                Pos,
                Api as ICoreClientAPI
            );

            dlg.TryOpen();
        }


        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            if (workItemStack == null || SelectedRecipe == null)
            {
                return;
            }

            float temperature = workItemStack.Collectible.GetTemperature(Api.World, workItemStack);


            dsc.AppendLine(Lang.Get("Output: {0}", SelectedRecipe.Output?.ResolvedItemstack?.GetName()));
            //dsc.AppendLine(Lang.Get("Available Voxels: {0}", AvailableMetalVoxels));

            dsc.AppendLine(Lang.Get("Temperature: {0}°C", (int)temperature));

            if (!CanWorkCurrent)
            {
                dsc.AppendLine(Lang.Get("Too cold to work"));
            }

            /*if (AvailableMetalVoxels <= 0)
            {
                dsc.AppendLine(Lang.Get("Add another hot ingot to continue smithing, or move voxels"));
            }*/
        }



        public override void OnLoadCollectibleMappings(IWorldAccessor worldForResolve, Dictionary<int, AssetLocation> oldBlockIdMapping, Dictionary<int, AssetLocation> oldItemIdMapping, int schematicSeed)
        {
            if (workItemStack?.FixMapping(oldBlockIdMapping, oldItemIdMapping, worldForResolve) == false)
            {
                workItemStack = null;
            } 
        }

        public override void OnStoreCollectibleMappings(Dictionary<int, AssetLocation> blockIdMapping, Dictionary<int, AssetLocation> itemIdMapping)
        {
            if (workItemStack != null)
            {
                if (workItemStack.Class == EnumItemClass.Item)
                {
                    blockIdMapping[workItemStack.Id] = workItemStack.Item.Code;
                }
                else
                {
                    itemIdMapping[workItemStack.Id] = workItemStack.Block.Code;
                }
            }
        }

        public override void OnBlockUnloaded()
        {
            workitemRenderer?.Dispose();
        }

        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tesselator)
        {
            mesher.AddMeshData(currentMesh.Clone().Rotate(new Vec3f(0.5f, 0.5f, 0.5f), 0, MeshAngle, 0));
            return true;
        }

    }

    public enum EnumAnvilPacket
    {
        OpenDialog = 1000,
        SelectRecipe = 1001,
        OnUserOver = 1002
    }
}
