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

#nullable disable

namespace Vintagestory.GameContent
{
    public enum EnumVoxelMaterial
    {
        Empty = 0,
        Metal = 1,
        Slag = 2,
        Placeholder1 = 3,
    }

    public class BlockEntityAnvil : BlockEntity, IRotatable, ITemperatureSensitive
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
            smallMetalSparks.ParticleModel = EnumParticleModel.Quad;
            smallMetalSparks.LifeLength = 0.03f;
            smallMetalSparks.MinVelocity = new Vec3f(-2f, 1f, -2f);
            smallMetalSparks.AddVelocity = new Vec3f(4f, 2f, 4f);
            smallMetalSparks.MinQuantity = 6;
            smallMetalSparks.AddQuantity = 12;
            smallMetalSparks.MinSize = 0.1f;
            smallMetalSparks.MaxSize = 0.1f;
            smallMetalSparks.SizeEvolve = new EvolvingNatFloat(EnumTransformFunction.LINEAR, -0.1f);


            bigMetalSparks = new SimpleParticleProperties(
                4, 8,
                ColorUtil.ToRgba(255, 255, 233, 83),
                new Vec3d(), new Vec3d(),
                new Vec3f(-1f, 1f, -1f),
                new Vec3f(2f, 4f, 2f),
                0.5f,
                1f,
                0.25f, 0.25f
            );
            bigMetalSparks.VertexFlags = 128;
            bigMetalSparks.AddPos.Set(1 / 16f, 0, 1 / 16f);
            bigMetalSparks.SizeEvolve = new EvolvingNatFloat(EnumTransformFunction.LINEAR, -0.25f);
            bigMetalSparks.Bounciness = 1f;
            bigMetalSparks.addLifeLength = 2f;
            bigMetalSparks.GreenEvolve = new EvolvingNatFloat(EnumTransformFunction.LINEAR, -233f);
            bigMetalSparks.BlueEvolve = new EvolvingNatFloat(EnumTransformFunction.LINEAR, -83f);




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
        public int SelectedRecipeId = -1;
        public byte[,,] Voxels = new byte[16, 6, 16]; // Only the first 2 bits of each byte are used and serialized


        // Temporary data
        float voxYOff = 10 / 16f;
        Cuboidf[] selectionBoxes = new Cuboidf[1];
        public int OwnMetalTier;
        AnvilWorkItemRenderer workitemRenderer;
        public int rotation = 0;
        public float MeshAngle;
        MeshData currentMesh;

        GuiDialog dlg;
        ItemStack returnOnCancelStack;


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
            get { return Api.GetSmithingRecipes().FirstOrDefault(r => r.RecipeId == SelectedRecipeId); }
        }

        public bool CanWorkCurrent
        {
            get { return workItemStack != null && workItemStack.Collectible.GetCollectibleInterface<IAnvilWorkable>().CanWork(WorkItemStack); }
        }

        public ItemStack WorkItemStack
        {
            get { return workItemStack; }
        }

        public bool IsHot => (workItemStack?.Collectible.GetTemperature(Api.World, workItemStack) ?? 0) > 20;

        public BlockEntityAnvil() : base() { }


        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            workItemStack?.ResolveBlockOrItem(api.World);

            if (api is ICoreClientAPI capi)
            {
                capi.Event.RegisterRenderer(workitemRenderer = new AnvilWorkItemRenderer(this, Pos, capi), EnumRenderStage.Opaque);
                capi.Event.RegisterRenderer(workitemRenderer, EnumRenderStage.AfterFinalComposition);

                RegenMeshAndSelectionBoxes();
                capi.Tesselator.TesselateBlock(Block, out currentMesh);
                capi.Event.ColorsPresetChanged += RegenMeshAndSelectionBoxes;
            }

            string metalType = Block.Variant["metal"];
            if (api.ModLoader.GetModSystem<SurvivalCoreSystem>().metalsByCode.TryGetValue(metalType, out MetalPropertyVariant var))
            {
                OwnMetalTier = var.Tier;
            }
        }


        internal Cuboidf[] GetSelectionBoxes(IBlockAccessor world, BlockPos pos)
        {
            return selectionBoxes;
        }

        internal bool OnPlayerInteract(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (byPlayer.InventoryManager.ActiveHotbarSlot.Itemstack?.Collectible.Tool == EnumTool.Hammer)
            {
                return RotateWorkItem(byPlayer.Entity.Controls.ShiftKey);
            }

            if (byPlayer.Entity.Controls.ShiftKey)
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

            rotation = (rotation + (ccw ? 270 : 90)) % 360;

            this.Voxels = rotVoxels;
            RegenMeshAndSelectionBoxes();
            MarkDirty();

            return true;
        }

        private bool TryTake(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (workItemStack == null) return false;

            ditchWorkItemStack(byPlayer);

            return true;
        }


        private bool TryPut(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            ItemSlot slot = byPlayer.InventoryManager.ActiveHotbarSlot;
            if (slot.Itemstack == null) return false;
            ItemStack stack = slot.Itemstack;

            IAnvilWorkable workableobj = stack.Collectible.GetCollectibleInterface<IAnvilWorkable>();

            if (workableobj == null) return false;
            int requiredTier = workableobj.GetRequiredAnvilTier(stack);
            if (requiredTier > OwnMetalTier)
            {
                if (world.Side == EnumAppSide.Client)
                {
                    (Api as ICoreClientAPI).TriggerIngameError(this, "toolowtier", Lang.Get("Working this metal needs a tier {0} anvil", requiredTier));
                }

                return false;
            }

            ItemStack newWorkItemStack = workableobj.TryPlaceOn(stack, this);
            if (newWorkItemStack != null)
            {
                if (workItemStack == null)
                {
                    workItemStack = newWorkItemStack;
                    rotation = workItemStack.Attributes.GetInt("rotation");
                }
                else if (workItemStack.Collectible is ItemWorkItem wi && wi.isBlisterSteel) return false;

                if (SelectedRecipeId < 0)
                {
                    var list = workableobj.GetMatchingRecipes(stack);
                    if (list.Count == 1)
                    {
                        SelectedRecipeId = list[0].RecipeId;
                    }
                    else
                    {
                        if (world.Side == EnumAppSide.Client)
                        {
                            OpenDialog(stack);
                        }
                    }
                }

                returnOnCancelStack = slot.TakeOut(1);
                slot.MarkDirty();
                Api.World.Logger.Audit("{0} Put 1x{1} on to Anvil at {2}.",
                    byPlayer?.PlayerName,
                    newWorkItemStack.Collectible.Code,
                    Pos
                );

                if (Api.Side == EnumAppSide.Server)
                {
                    // Let the server decide the shape, then send the stuff to client, and then show the correct voxels
                    // instead of the voxels flicker thing when both sides do it (due to voxel placement randomness in iron bloom and blister steel)
                    RegenMeshAndSelectionBoxes();
                }

                CheckIfFinished(byPlayer);
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

            if (SelectedRecipe == null)
            {
                ditchWorkItemStack();
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
                    case 1: OnUpset(voxelPos, BlockFacing.NORTH.FaceWhenRotatedBy(0, yaw - GameMath.PI, 0)); break;
                    case 2: OnUpset(voxelPos, BlockFacing.EAST.FaceWhenRotatedBy(0, yaw - GameMath.PI, 0)); break;
                    case 3: OnUpset(voxelPos, BlockFacing.SOUTH.FaceWhenRotatedBy(0, yaw - GameMath.PI, 0)); break;
                    case 4: OnUpset(voxelPos, BlockFacing.WEST.FaceWhenRotatedBy(0, yaw - GameMath.PI, 0)); break;
                    case 5: OnSplit(voxelPos); break;
                }

                RegenMeshAndSelectionBoxes();
                Api.World.BlockAccessor.MarkBlockDirty(Pos);
                Api.World.BlockAccessor.MarkBlockEntityDirty(Pos);
                slot.Itemstack.Collectible.DamageItem(Api.World, byPlayer.Entity, slot);

                if (!HasAnyMetalVoxel())
                {
                    clearWorkSpace();
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
                bigMetalSparks.AddPos.Set(1 / 16f, 0, 1 / 16f);
                bigMetalSparks.VertexFlags = (byte)GameMath.Clamp((int)(temp - 700) / 2, 32, 128);

                Api.World.SpawnParticles(bigMetalSparks, byPlayer);


                smallMetalSparks.MinPos = Pos.ToVec3d().AddCopy(voxelPos.X / 16f, voxYOff + voxelPos.Y / 16f + 0.0625f, voxelPos.Z / 16f);
                smallMetalSparks.VertexFlags = (byte)GameMath.Clamp((int)(temp - 770) / 3, 32, 128);
                smallMetalSparks.AddPos.Set(1 / 16f, 0, 1 / 16f);



                Api.World.SpawnParticles(smallMetalSparks, byPlayer);
            }

            if (voxelMat == EnumVoxelMaterial.Slag)
            {
                slagPieces.Color = workItemStack.Collectible.GetRandomColor(Api as ICoreClientAPI, workItemStack);
                slagPieces.MinPos = Pos.ToVec3d().AddCopy(voxelPos.X / 16f, voxYOff + voxelPos.Y / 16f + 0.0625f, voxelPos.Z / 16f);
                slagPieces.ColorByItem = workItemStack.Item;
                Api.World.SpawnParticles(slagPieces, byPlayer);
            }
        }


        internal string PrintDebugText()
        {
            SmithingRecipe recipe = SelectedRecipe;


            EnumHelveWorkableMode? mode = workItemStack?.Collectible.GetCollectibleInterface<IAnvilWorkable>()?.GetHelveWorkableMode(workItemStack, this);

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Workitem: " + workItemStack);
            sb.AppendLine("Recipe: " + recipe?.Name);
            sb.AppendLine("Matches recipe: " + MatchesRecipe());
            sb.AppendLine("Helve Workable: " + mode);

            return sb.ToString();
        }

        public virtual void OnHelveHammerHit()
        {
            if (workItemStack == null || !CanWorkCurrent) return;

            SmithingRecipe recipe = SelectedRecipe;
            if (recipe == null)
            {
                return;
            }

            var mode = workItemStack.Collectible.GetCollectibleInterface<IAnvilWorkable>()?.GetHelveWorkableMode(workItemStack, this);
            if (mode == EnumHelveWorkableMode.NotWorkable) return;

            rotation = 0;
            int ymax = recipe.QuantityLayers;
            Vec3i usableMetalVoxel;
            if (mode == EnumHelveWorkableMode.TestSufficientVoxelsWorkable)
            {
                usableMetalVoxel = findFreeMetalVoxel();

                for (int x = 0; x < 16; x++)
                {
                    for (int z = 0; z < 16; z++)
                    {
                        for (int y = 0; y < 6; y++)
                        {
                            bool requireMetalHere = y >= ymax ? false : recipe.Voxels[x, y, z];

                            EnumVoxelMaterial mat = (EnumVoxelMaterial)Voxels[x, y, z];

                            if (mat == EnumVoxelMaterial.Slag)
                            {
                                Voxels[x, y, z] = (byte)EnumVoxelMaterial.Empty;
                                onHelveHitSuccess(mat, null, x, y, z);
                                return;
                            }

                            if (requireMetalHere && usableMetalVoxel != null && mat == EnumVoxelMaterial.Empty)
                            {
                                Voxels[x, y, z] = (byte)EnumVoxelMaterial.Metal;
                                Voxels[usableMetalVoxel.X, usableMetalVoxel.Y, usableMetalVoxel.Z] = (byte)EnumVoxelMaterial.Empty;

                                onHelveHitSuccess(mat, usableMetalVoxel, x, y, z);
                                return;
                            }
                        }
                    }
                }

                if (usableMetalVoxel != null)
                {
                    Voxels[usableMetalVoxel.X, usableMetalVoxel.Y, usableMetalVoxel.Z] = (byte)EnumVoxelMaterial.Empty;
                    onHelveHitSuccess(EnumVoxelMaterial.Metal, null, usableMetalVoxel.X, usableMetalVoxel.Y, usableMetalVoxel.Z);
                    return;
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

                            if (requireMetalHere && mat == EnumVoxelMaterial.Empty)
                            {
                                Voxels[x, y, z] = (byte)EnumVoxelMaterial.Metal;
                            }
                            else
                            {
                                Voxels[x, y, z] = (byte)EnumVoxelMaterial.Empty;
                            }

                            onHelveHitSuccess(mat == EnumVoxelMaterial.Empty ? EnumVoxelMaterial.Metal : mat, null, x, y, z);

                            return;
                        }
                    }
                }
            }
        }

        void onHelveHitSuccess(EnumVoxelMaterial mat, Vec3i usableMetalVoxel, int x, int y, int z)
        {
            if (Api.World.Side == EnumAppSide.Client)
            {
                spawnParticles(new Vec3i(x, y, z), mat == EnumVoxelMaterial.Empty ? EnumVoxelMaterial.Metal : mat, null);
                if (usableMetalVoxel != null) spawnParticles(usableMetalVoxel, EnumVoxelMaterial.Metal, null);
            }

            RegenMeshAndSelectionBoxes();
            CheckIfFinished(null);
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
            if (SelectedRecipe == null) return;

            if (MatchesRecipe() && Api.World is IServerWorldAccessor)
            {
                Voxels = new byte[16, 6, 16];
                ItemStack outstack = SelectedRecipe.Output.ResolvedItemstack.Clone();
                outstack.Collectible.SetTemperature(Api.World, outstack, workItemStack.Collectible.GetTemperature(Api.World, workItemStack));
                workItemStack = null;

                SelectedRecipeId = -1;

                if (byPlayer?.InventoryManager.TryGiveItemstack(outstack) == true)
                {
                    Api.World.PlaySoundFor(new AssetLocation("sounds/player/collect"), byPlayer, false, 24);
                }
                else
                {
                    Api.World.SpawnItemEntity(outstack, Pos.ToVec3d().Add(0.5, 0.626, 0.5));
                }
                Api.World.Logger.Audit("{0} Took 1x{1} from Anvil at {2}.",
                    byPlayer?.PlayerName,
                    outstack.Collectible.Code,
                    Pos
                );

                RegenMeshAndSelectionBoxes();
                MarkDirty();
                Api.World.BlockAccessor.MarkBlockDirty(Pos);
                rotation = 0;
            }
        }

        public void ditchWorkItemStack(IPlayer byPlayer = null)
        {
            if (workItemStack == null) return;

            ItemStack ditchedStack;
            if (SelectedRecipe == null)
            {
                ditchedStack = returnOnCancelStack ?? workItemStack.Collectible.GetCollectibleInterface<IAnvilWorkable>().GetBaseMaterial(workItemStack);
                float temp = workItemStack.Collectible.GetTemperature(Api.World, workItemStack);
                ditchedStack.Collectible.SetTemperature(Api.World, ditchedStack, temp);
            }
            else
            {

                workItemStack.Attributes.SetBytes("voxels", serializeVoxels(Voxels));
                workItemStack.Attributes.SetInt("selectedRecipeId", SelectedRecipeId);
                workItemStack.Attributes.SetInt("rotation", rotation);

                if (workItemStack.Collectible is ItemIronBloom bloomItem)
                {
                    workItemStack.Attributes.SetInt("hashCode", bloomItem.GetWorkItemHashCode(workItemStack));
                }

                ditchedStack = workItemStack;
            }

            if (byPlayer == null || !byPlayer.InventoryManager.TryGiveItemstack(ditchedStack))
            {
                Api.World.SpawnItemEntity(ditchedStack, Pos);
            }
            Api.World.Logger.Audit("{0} Took 1x{1} from Anvil at {2}.",
                byPlayer?.PlayerName,
                ditchedStack.Collectible.Code,
                Pos
            );

            clearWorkSpace();
        }

        protected void clearWorkSpace()
        {
            workItemStack = null;
            Voxels = new byte[16, 6, 16];
            RegenMeshAndSelectionBoxes();
            MarkDirty();
            rotation = 0;
            SelectedRecipeId = -1;
        }

        private bool MatchesRecipe()
        {
            if (SelectedRecipe == null) return false;

            int ymax = Math.Min(6, SelectedRecipe.QuantityLayers);

            bool[,,] recipeVoxels = this.recipeVoxels; // Otherwise we cause lag spikes

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
            // Can only move metal
            if (Voxels[voxelPos.X, voxelPos.Y, voxelPos.Z] != (byte)EnumVoxelMaterial.Metal) return;
            // Can't move if metal is above
            if (voxelPos.Y < 5 && Voxels[voxelPos.X, voxelPos.Y + 1, voxelPos.Z] != (byte)EnumVoxelMaterial.Empty) return;

            Vec3i npos = voxelPos.Clone().Add(towardsFace);
            Vec3i opFaceDir = towardsFace.Opposite.Normali;

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

                    if (voxelPos.X + opFaceDir.X < 0 || voxelPos.X + opFaceDir.X >= 16 || voxelPos.Z + opFaceDir.Z < 0 || voxelPos.Z + opFaceDir.Z >= 16) return;

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


        protected bool moveVoxelDownwards(Vec3i voxelPos, BlockFacing towardsFace, int maxDist)
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

        protected void RegenMeshAndSelectionBoxes()
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



        public override void OnBlockRemoved()
        {
            base.OnBlockRemoved();
            workitemRenderer?.Dispose();
            workitemRenderer = null;
            if (Api is ICoreClientAPI capi) capi.Event.ColorsPresetChanged -= RegenMeshAndSelectionBoxes;
        }

        public override void OnBlockBroken(IPlayer byPlayer = null)
        {
            if (workItemStack != null)
            {
                workItemStack.Attributes.SetBytes("voxels", serializeVoxels(Voxels));
                workItemStack.Attributes.SetInt("selectedRecipeId", SelectedRecipeId);

                Api.World.SpawnItemEntity(workItemStack, Pos);
            }
        }


        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);
            Voxels = deserializeVoxels(tree.GetBytes("voxels"));
            workItemStack = tree.GetItemstack("workItemStack");
            SelectedRecipeId = tree.GetInt("selectedRecipeId", -1);
            rotation = tree.GetInt("rotation");

            if (Api != null && workItemStack != null)
            {
                workItemStack.ResolveBlockOrItem(Api.World);
            }

            RegenMeshAndSelectionBoxes();

            MeshAngle = tree.GetFloat("meshAngle", MeshAngle);

            if (Api?.Side == EnumAppSide.Client)
            {
                ((ICoreClientAPI)Api).Tesselator.TesselateBlock(Block, out MeshData newMesh);

                currentMesh = newMesh; // Needed so we don't get race conditions

                MarkDirty(true);
            }
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            tree.SetBytes("voxels", serializeVoxels(Voxels));
            tree.SetItemstack("workItemStack", workItemStack);
            tree.SetInt("selectedRecipeId", SelectedRecipeId);
            tree.SetInt("rotation", rotation);
            tree.SetFloat("meshAngle", MeshAngle);
        }


        static int bitsPerByte = 2;
        static int partsPerByte = 8 / bitsPerByte;

        public static byte[] serializeVoxels(byte[,,] voxels)
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

        public static byte[,,] deserializeVoxels(byte[] data)
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
                Pos,
                (int)EnumAnvilPacket.OnUserOver,
                data
            );
        }


        public override void OnReceivedClientPacket(IPlayer player, int packetid, byte[] data)
        {
            if (packetid == (int)EnumAnvilPacket.SelectRecipe)
            {
                int recipeid = SerializerUtil.Deserialize<int>(data);
                SmithingRecipe recipe = Api.GetSmithingRecipes().FirstOrDefault(r => r.RecipeId == recipeid);

                if (recipe == null)
                {
                    Api.World.Logger.Error("Client tried to selected smithing recipe with id {0}, but no such recipe exists!");
                    ditchWorkItemStack(player);
                    return;
                }

                var list = (WorkItemStack?.Collectible as ItemWorkItem)?.GetMatchingRecipes(workItemStack);
                if (list == null || list.FirstOrDefault(r => r.RecipeId == recipeid) == null)
                {
                    Api.World.Logger.Error("Client tried to selected smithing recipe with id {0}, but it is not a valid one for the given work item stack!", recipe.RecipeId);
                    ditchWorkItemStack(player);
                    return;
                }


                SelectedRecipeId = recipe.RecipeId;

                // Tell server to save this chunk to disk again
                MarkDirty();
                Api.World.BlockAccessor.GetChunkAtBlockPos(Pos).MarkModified();
            }

            if (packetid == (int)EnumAnvilPacket.CancelSelect)
            {
                ditchWorkItemStack(player);
                return;
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
            IAnvilWorkable workableobj = ingredient.Collectible.GetCollectibleInterface<IAnvilWorkable>();
            List<SmithingRecipe> recipes = workableobj.GetMatchingRecipes(ingredient);

            List<ItemStack> stacks = recipes
                .Select(r => r.Output.ResolvedItemstack)
                .ToList()
            ;

            IClientWorldAccessor clientWorld = (IClientWorldAccessor)Api.World;
            ICoreClientAPI capi = Api as ICoreClientAPI;

            dlg?.Dispose();
            dlg = new GuiDialogBlockEntityRecipeSelector(
                Lang.Get("Select smithing recipe"),
                stacks.ToArray(),
                (selectedIndex) => {
                    SelectedRecipeId = recipes[selectedIndex].RecipeId;
                    capi.Network.SendBlockEntityPacket(Pos, (int)EnumAnvilPacket.SelectRecipe, SerializerUtil.Serialize(recipes[selectedIndex].RecipeId));
                },
                () => {
                    capi.Network.SendBlockEntityPacket(Pos, (int)EnumAnvilPacket.CancelSelect);
                },
                Pos,
                Api as ICoreClientAPI
            );

            for (int i = 0; i < recipes.Count; i++)
            {
                ItemStack[] ingredCount = [ingredient.GetEmptyClone()];
                ingredCount[0].StackSize = (int)Math.Ceiling(recipes[i].Voxels.Cast<bool>().Count(voxel => voxel) / (double)workableobj.VoxelCountForHandbook(ingredient));
                (dlg as GuiDialogBlockEntityRecipeSelector).SetIngredientCounts(i, ingredCount);
            }

            dlg.TryOpen();
        }


        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            dsc.AppendLine(Lang.Get("Tier {0} anvil", OwnMetalTier));

            if (workItemStack == null || SelectedRecipe == null)
            {
                return;
            }

            float temperature = workItemStack.Collectible.GetTemperature(Api.World, workItemStack);

            dsc.AppendLine(Lang.Get("Output: {0}", SelectedRecipe.Output?.ResolvedItemstack?.GetName()));

            if (temperature < 25)
            {
                dsc.AppendLine(Lang.Get("Temperature: Cold"));
            } else
            {
                dsc.AppendLine(Lang.Get("Temperature: {0}°C", (int)temperature));
            }


            if (!CanWorkCurrent)
            {
                dsc.AppendLine(Lang.Get("Too cold to work"));
            }
        }



        public override void OnLoadCollectibleMappings(IWorldAccessor worldForResolve, Dictionary<int, AssetLocation> oldBlockIdMapping, Dictionary<int, AssetLocation> oldItemIdMapping, int schematicSeed, bool resolveImports)
        {
            if (workItemStack?.FixMapping(oldBlockIdMapping, oldItemIdMapping, worldForResolve) == false)
            {
                workItemStack = null;
            }
            workItemStack?.Collectible.OnLoadCollectibleMappings(worldForResolve, new DummySlot(workItemStack) ,oldBlockIdMapping, oldItemIdMapping, resolveImports);
        }

        public override void OnStoreCollectibleMappings(Dictionary<int, AssetLocation> blockIdMapping, Dictionary<int, AssetLocation> itemIdMapping)
        {
            if (workItemStack != null)
            {
                if (workItemStack.Class == EnumItemClass.Item)
                {
                    itemIdMapping[workItemStack.Id] = workItemStack.Item.Code;
                }
                else
                {
                    blockIdMapping[workItemStack.Id] = workItemStack.Block.Code;
                }
                workItemStack.Collectible.OnStoreCollectibleMappings(Api.World, new DummySlot(workItemStack), blockIdMapping, itemIdMapping);
            }
        }

        public override void OnBlockUnloaded()
        {
            base.OnBlockUnloaded();
            workitemRenderer?.Dispose();
            dlg?.TryClose();
            dlg?.Dispose();
            if (Api is ICoreClientAPI capi) capi.Event.ColorsPresetChanged -= RegenMeshAndSelectionBoxes;
        }

        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tesselator)
        {
            mesher.AddMeshData(currentMesh.Clone().Rotate(new Vec3f(0.5f, 0.5f, 0.5f), 0, MeshAngle, 0));
            return true;
        }

        public void OnTransformed(IWorldAccessor worldAccessor, ITreeAttribute tree, int degreeRotation,
            Dictionary<int, AssetLocation> oldBlockIdMapping, Dictionary<int, AssetLocation> oldItemIdMapping, EnumAxis? flipAxis)
        {
            MeshAngle = tree.GetFloat("meshAngle");
            MeshAngle -= degreeRotation * GameMath.DEG2RAD;
            tree.SetFloat("meshAngle", MeshAngle);
        }

        public void CoolNow(float amountRel)
        {
            if (workItemStack == null) return;
            float temp = workItemStack.Collectible.GetTemperature(Api.World, workItemStack);
            if (temp > 120)
            {
                Api.World.PlaySoundAt(new AssetLocation("sounds/effect/extinguish"), Pos, 0.25, null, false, 16);
            }

            workItemStack.Collectible.SetTemperature(Api.World, workItemStack, Math.Max(20, temp - amountRel * 20), false);
            MarkDirty(true);
        }
    }

    public enum EnumAnvilPacket
    {
        OpenDialog = 1000,
        SelectRecipe = 1001,
        OnUserOver = 1002,
        CancelSelect = 1003
    }
}
