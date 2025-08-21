using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

#nullable disable

namespace Vintagestory.GameContent
{
    public class BlockBarrel : BlockLiquidContainerBase
    {
        public override bool AllowHeldLiquidTransfer => false;

        public AssetLocation emptyShape { get; protected set; } = AssetLocation.Create("block/wood/barrel/empty");
        public AssetLocation sealedShape { get; protected set; } = AssetLocation.Create("block/wood/barrel/closed");
        public AssetLocation contentsShape { get; protected set; } = AssetLocation.Create("block/wood/barrel/contents");
        public AssetLocation opaqueLiquidContentsShape { get; protected set; } = AssetLocation.Create("block/wood/barrel/opaqueliquidcontents");
        public AssetLocation liquidContentsShape { get; protected set; } = AssetLocation.Create("block/wood/barrel/liquidcontents");

        public override int GetContainerSlotId(BlockPos pos) => 1;

        public override int GetContainerSlotId(ItemStack containerStack) => 1;


        #region Render
        public override void OnBeforeRender(ICoreClientAPI capi, ItemStack itemstack, EnumItemRenderTarget target, ref ItemRenderInfo renderinfo)
        {
            Dictionary<string, MultiTextureMeshRef> meshrefs;

            if (capi.ObjectCache.TryGetValue("barrelMeshRefs" + Code, out object obj))
            {
                meshrefs = obj as Dictionary<string, MultiTextureMeshRef>;
            }
            else
            {
                capi.ObjectCache["barrelMeshRefs" + Code] = meshrefs = new Dictionary<string, MultiTextureMeshRef>();
            }

            ItemStack[] contentStacks = GetContents(capi.World, itemstack);
            if (contentStacks == null || contentStacks.Length == 0) return;

            bool issealed = itemstack.Attributes.GetBool("sealed");

            string meshkey = GetBarrelMeshkey(contentStacks[0], contentStacks.Length > 1 ? contentStacks[1] : null);


            if (!meshrefs.TryGetValue(meshkey, out MultiTextureMeshRef meshRef))
            {
                MeshData meshdata = GenMesh(contentStacks[0], contentStacks.Length > 1 ? contentStacks[1] : null, issealed);
                meshrefs[meshkey] = meshRef = capi.Render.UploadMultiTextureMesh(meshdata);
            }

            renderinfo.ModelRef = meshRef;
        }


        public string GetBarrelMeshkey(ItemStack contentStack, ItemStack liquidStack)
        {
            string s = contentStack?.StackSize + "x" + contentStack?.GetHashCode();
            s += liquidStack?.StackSize + "x" + liquidStack?.GetHashCode();
            return s;
        }


        public override void OnUnloaded(ICoreAPI api)
        {
            ICoreClientAPI capi = api as ICoreClientAPI;
            if (capi == null) return;

            if (capi.ObjectCache.TryGetValue("barrelMeshRefs", out object obj))
            {
                Dictionary<int, MultiTextureMeshRef> meshrefs = obj as Dictionary<int, MultiTextureMeshRef>;

                foreach (var val in meshrefs)
                {
                    val.Value.Dispose();
                }

                capi.ObjectCache.Remove("barrelMeshRefs");
            }
        }


        // Override to drop the barrel empty and drop its contents instead
        public override void OnBlockBroken(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1)
        {
            // First, check for behaviors preventing default, for example Reinforcement system
            bool preventDefault = false;
            foreach (BlockBehavior behavior in BlockBehaviors)
            {
                EnumHandling handled = EnumHandling.PassThrough;

                behavior.OnBlockBroken(world, pos, byPlayer, ref handled);
                if (handled == EnumHandling.PreventDefault) preventDefault = true;
                if (handled == EnumHandling.PreventSubsequent) return;
            }

            if (preventDefault) return;


            if (world.Side == EnumAppSide.Server && (byPlayer == null || byPlayer.WorldData.CurrentGameMode != EnumGameMode.Creative))
            {
                ItemStack[] drops = new ItemStack[] { new ItemStack(this) };

                for (int i = 0; i < drops.Length; i++)
                {
                    world.SpawnItemEntity(drops[i], pos, null);
                }

                world.PlaySoundAt(Sounds.GetBreakSound(byPlayer), pos, 0, byPlayer);
            }

            if (EntityClass != null)
            {
                BlockEntity entity = world.BlockAccessor.GetBlockEntity(pos);
                if (entity != null)
                {
                    entity.OnBlockBroken(byPlayer);
                }
            }

            world.BlockAccessor.SetBlock(0, pos);
        }





        public override void OnHeldAttackStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandHandling handling)
        {
            
        }

        public override int TryPutLiquid(BlockPos pos, ItemStack liquidStack, float desiredLitres)
        {
            return base.TryPutLiquid(pos, liquidStack, desiredLitres);
        }

        public override int TryPutLiquid(ItemStack containerStack, ItemStack liquidStack, float desiredLitres)
        {
            return base.TryPutLiquid(containerStack, liquidStack, desiredLitres);
        }



        #endregion


        #region Mesh generation

        public MeshData GenMesh(ItemStack contentStack, ItemStack liquidContentStack, bool issealed, BlockPos forBlockPos = null)
        {
            ICoreClientAPI capi = api as ICoreClientAPI;

            Shape shape = API.Common.Shape.TryGet(capi, issealed ? sealedShape : emptyShape);
            capi.Tesselator.TesselateShape(this, shape, out MeshData barrelMesh);

            if (!issealed)
            {
                var containerProps = liquidContentStack?.ItemAttributes?["waterTightContainerProps"];

                MeshData contentMesh =
                    getContentMeshFromAttributes(contentStack, liquidContentStack, forBlockPos) ??
                    getContentMeshLiquids(contentStack, liquidContentStack, forBlockPos, containerProps) ??
                    getContentMesh(contentStack, forBlockPos, contentsShape)
                ;

                if (contentMesh != null)
                {
                    barrelMesh.AddMeshData(contentMesh);
                }

                if (forBlockPos != null)
                {
                    // Water flags
                    barrelMesh.CustomInts = new CustomMeshDataPartInt(barrelMesh.FlagsCount);
                    barrelMesh.CustomInts.Values.Fill(VertexFlags.LiquidWeakFoamBitMask); // light foam only
                    barrelMesh.CustomInts.Count = barrelMesh.FlagsCount;

                    barrelMesh.CustomFloats = new CustomMeshDataPartFloat(barrelMesh.FlagsCount * 2);
                    barrelMesh.CustomFloats.Count = barrelMesh.FlagsCount * 2;
                }
            }


            return barrelMesh;
        }

        private MeshData getContentMeshLiquids(ItemStack contentStack, ItemStack liquidContentStack, BlockPos forBlockPos, JsonObject containerProps)
        {
            bool isopaque = containerProps?["isopaque"].AsBool(false) == true;
            bool isliquid = containerProps?.Exists == true;
            if (liquidContentStack != null && (isliquid || contentStack == null))
            {
                AssetLocation shapefilepath = contentsShape;
                if (isliquid) shapefilepath = isopaque ? opaqueLiquidContentsShape : liquidContentsShape;

                return getContentMesh(liquidContentStack, forBlockPos, shapefilepath);
            }

            return null;
        }

        private MeshData getContentMeshFromAttributes(ItemStack contentStack, ItemStack liquidContentStack, BlockPos forBlockPos)
        {
            if (liquidContentStack?.ItemAttributes?["inBarrelShape"].Exists == true)
            {
                var loc = AssetLocation.Create(liquidContentStack.ItemAttributes?["inBarrelShape"].AsString(), contentStack.Collectible.Code.Domain).WithPathPrefixOnce("shapes").WithPathAppendixOnce(".json");
                return getContentMesh(contentStack, forBlockPos, loc);
            }

            return null;
        }


        protected MeshData getContentMesh(ItemStack stack, BlockPos forBlockPos, AssetLocation shapefilepath)
        {
            ICoreClientAPI capi = api as ICoreClientAPI;

            WaterTightContainableProps props = GetContainableProps(stack);
            ITexPositionSource contentSource;
            float fillHeight;

            if (props != null)
            {
                if (props.Texture == null) return null;

                contentSource = new ContainerTextureSource(capi, stack, props.Texture);
                fillHeight = GameMath.Min(1f, stack.StackSize / props.ItemsPerLitre / Math.Max(50, props.MaxStackSize)) * 10f / 16f;
            }
            else
            {
                contentSource = getContentTexture(capi, stack, out fillHeight);
            }


            if (stack != null && contentSource != null)
            {
                Shape shape = API.Common.Shape.TryGet(capi, shapefilepath);
                if (shape == null)
                {
                    api.Logger.Warning(string.Format("Barrel block '{0}': Content shape {1} not found. Will try to default to another one.", Code, shapefilepath));
                    return null;
                }
                capi.Tesselator.TesselateShape("barrel", shape, out MeshData contentMesh, contentSource, new Vec3f(Shape.rotateX, Shape.rotateY, Shape.rotateZ), props?.GlowLevel ?? 0);

                contentMesh.Translate(0, fillHeight, 0);

                if (props?.ClimateColorMap != null)
                {
                    int col = capi.World.ApplyColorMapOnRgba(props.ClimateColorMap, null, ColorUtil.WhiteArgb, 196, 128, false);
                    if (forBlockPos != null)
                    {
                        col = capi.World.ApplyColorMapOnRgba(props.ClimateColorMap, null, ColorUtil.WhiteArgb, forBlockPos.X, forBlockPos.Y, forBlockPos.Z, false);
                    }

                    byte[] rgba = ColorUtil.ToBGRABytes(col);

                    for (int i = 0; i < contentMesh.Rgba.Length; i++)
                    {
                        contentMesh.Rgba[i] = (byte)((contentMesh.Rgba[i] * rgba[i % 4]) / 255);
                    }
                }


                return contentMesh;
            }

            return null;
        }


        public static ITexPositionSource getContentTexture(ICoreClientAPI capi, ItemStack stack, out float fillHeight)
        {
            ITexPositionSource contentSource = null;
            fillHeight = 0;

            JsonObject obj = stack?.ItemAttributes?["inContainerTexture"];
            if (obj != null && obj.Exists)
            {
                contentSource = new ContainerTextureSource(capi, stack, obj.AsObject<CompositeTexture>());
                fillHeight = GameMath.Min(12 / 16f, 0.7f * stack.StackSize / stack.Collectible.MaxStackSize);
            }
            else
            {
                if (stack?.Block != null && (stack.Block.DrawType == EnumDrawType.Cube || stack.Block.Shape.Base.Path.Contains("basic/cube")) && capi.BlockTextureAtlas.GetPosition(stack.Block, "up", true) != null)
                {
                    contentSource = new BlockTopTextureSource(capi, stack.Block);
                    fillHeight = GameMath.Min(12 / 16f, 0.7f * stack.StackSize / stack.Collectible.MaxStackSize);
                }
                else if (stack != null)
                {

                    if (stack.Class == EnumItemClass.Block)
                    {
                        if (stack.Block.Textures.Count > 1) return null;

                        contentSource = new ContainerTextureSource(capi, stack, stack.Block.Textures.FirstOrDefault().Value);
                    }
                    else
                    {
                        if (stack.Item.Textures.Count > 1) return null;

                        contentSource = new ContainerTextureSource(capi, stack, stack.Item.FirstTexture);
                    }


                    fillHeight = GameMath.Min(12 / 16f, 0.7f * stack.StackSize / stack.Collectible.MaxStackSize);
                }
            }

            return contentSource;
        }

        #endregion


        public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot)
        {
            return new WorldInteraction[]
            {
                new WorldInteraction()
                {
                    ActionLangCode = "heldhelp-place",
                    HotKeyCode = "shift",
                    MouseButton = EnumMouseButton.Right,
                    ShouldApply = (wi, bs, es) => {
                        return true;
                    }
                }
            };
        }

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            if (Attributes != null)
            {
                capacityLitresFromAttributes = Attributes["capacityLitres"].AsInt(50);
                emptyShape = AssetLocation.Create(Attributes["emptyShape"].AsString(emptyShape), Code.Domain);
                sealedShape = AssetLocation.Create(Attributes["sealedShape"].AsString(sealedShape), Code.Domain);
                contentsShape = AssetLocation.Create(Attributes["contentsShape"].AsString(contentsShape), Code.Domain);
                opaqueLiquidContentsShape = AssetLocation.Create(Attributes["opaqueLiquidContentsShape"].AsString(opaqueLiquidContentsShape), Code.Domain);
                liquidContentsShape = AssetLocation.Create(Attributes["liquidContentsShape"].AsString(liquidContentsShape), Code.Domain);
            }

            emptyShape.WithPathPrefixOnce("shapes/").WithPathAppendixOnce(".json");
            sealedShape.WithPathPrefixOnce("shapes/").WithPathAppendixOnce(".json");
            contentsShape.WithPathPrefixOnce("shapes/").WithPathAppendixOnce(".json");
            opaqueLiquidContentsShape.WithPathPrefixOnce("shapes/").WithPathAppendixOnce(".json");
            liquidContentsShape.WithPathPrefixOnce("shapes/").WithPathAppendixOnce(".json");


            if (api.Side != EnumAppSide.Client) return;
            ICoreClientAPI capi = api as ICoreClientAPI;

            interactions = ObjectCacheUtil.GetOrCreate(api, "liquidContainerBase", () =>
            {
                List<ItemStack> liquidContainerStacks = new List<ItemStack>();

                foreach (CollectibleObject obj in api.World.Collectibles)
                {
                    if (obj is ILiquidSource || obj is ILiquidSink || obj is BlockWateringCan)
                    {
                        List<ItemStack> stacks = obj.GetHandBookStacks(capi);
                        if (stacks != null) liquidContainerStacks.AddRange(stacks);
                    }
                }

                ItemStack[] lstacks = liquidContainerStacks.ToArray();
                ItemStack[] linenStack = new ItemStack[] { new ItemStack(api.World.GetBlock(new AssetLocation("linen-normal-down"))) };

                return new WorldInteraction[] {
                    new WorldInteraction()
                    {
                        ActionLangCode = "blockhelp-bucket-rightclick",
                        MouseButton = EnumMouseButton.Right,
                        Itemstacks = lstacks,
                        GetMatchingStacks = (wi, bs, ws) =>
                        {
                            BlockEntityBarrel bebarrel = api.World.BlockAccessor.GetBlockEntity(bs.Position) as BlockEntityBarrel;
                            return bebarrel?.Sealed == false ? lstacks : null;
                        }
                    },
                    new WorldInteraction()
                    {
                        ActionLangCode = "blockhelp-barrel-takecottagecheese",
                        MouseButton = EnumMouseButton.Right,
                        HotKeyCode = "shift",
                        Itemstacks = linenStack,
                        GetMatchingStacks = (wi, bs, ws) =>
                        {
                            BlockEntityBarrel bebarrel = api.World.BlockAccessor.GetBlockEntity(bs.Position) as BlockEntityBarrel;
                            if (bebarrel?.Inventory[1].Itemstack?.Item?.Code?.Path == "cottagecheeseportion") return linenStack;
                            return null;
                        }
                    }
                };
            });
        }

        public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection blockSel, IPlayer forPlayer)
        {
            BlockEntityBarrel bebarrel = null;
            if (blockSel.Position != null)
            {
                bebarrel = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityBarrel;
            }
            if (bebarrel != null && bebarrel.Sealed) return Array.Empty<WorldInteraction>();   // No interactions shown if the barrel is sealed

            return base.GetPlacedBlockInteractionHelp(world, blockSel, forPlayer);
        }



        public override void OnHeldInteractStart(ItemSlot itemslot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling)
        {
            base.OnHeldInteractStart(itemslot, byEntity, blockSel, entitySel, firstEvent, ref handHandling);
        }


        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (blockSel != null && !world.Claims.TryAccess(byPlayer, blockSel.Position, EnumBlockAccessFlags.Use))
            {
                return false;
            }

            BlockEntityBarrel bebarrel=null;
            if (blockSel.Position != null)
            {
                bebarrel = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityBarrel;
            }
            if (bebarrel != null && bebarrel.Sealed)
            {
                return true;
            }

            bool handled = base.OnBlockInteractStart(world, byPlayer, blockSel);

            if (!handled && !byPlayer.WorldData.EntityControls.ShiftKey && blockSel.Position != null)
            {
                if (bebarrel != null)
                {
                    bebarrel.OnPlayerRightClick(byPlayer);
                }

                return true;
            }

            return handled;
        }




        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);

            ItemStack[] contentStacks = GetContents(world, inSlot.Itemstack);

            if (contentStacks != null && contentStacks.Length > 0)
            {
                ItemStack itemstack = contentStacks[0] == null ? contentStacks[1] : contentStacks[0];
                if (itemstack != null) dsc.Append(", " + Lang.Get("{0}x {1}", itemstack.StackSize, itemstack.GetName()));
            }
        }

        public override string GetPlacedBlockInfo(IWorldAccessor world, BlockPos pos, IPlayer forPlayer)
        {
            string text = base.GetPlacedBlockInfo(world, pos, forPlayer);
            string aftertext = "";
            int j = text.IndexOfOrdinal(Environment.NewLine + Environment.NewLine);
            if (j > 0)
            {
                aftertext = text.Substring(j);
                text = text.Substring(0, j);
            }

            float litres = GetCurrentLitres(pos);

            if (litres <= 0) text = "";

            BlockEntityBarrel bebarrel = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityBarrel;
            if (bebarrel != null)
            {
                ItemSlot slot = bebarrel.Inventory[0];
                if (!slot.Empty)
                {
                    if (text.Length > 0) text += " ";
                    else text += Lang.Get("Contents:") + "\n ";

                    text += Lang.Get("{0}x {1}", slot.Itemstack.StackSize, slot.Itemstack.GetName());

                    text += PerishableInfoCompact(api, slot, 0, false);
                }

                if (bebarrel.Sealed && bebarrel.CurrentRecipe != null)
                {
                    double hoursPassed = world.Calendar.TotalHours - bebarrel.SealedSinceTotalHours;
                    if (hoursPassed < 3) hoursPassed = Math.Max(0, hoursPassed + 0.2);  // Small addition to deal with possible server/client calendar desync
                    string timePassedText = hoursPassed > 24 ? Lang.Get("{0} days", Math.Floor(hoursPassed / api.World.Calendar.HoursPerDay * 10) / 10) : Lang.Get("{0} hours", Math.Floor(hoursPassed));
                    string timeTotalText = bebarrel.CurrentRecipe.SealHours > 24 ? Lang.Get("{0} days", Math.Round(bebarrel.CurrentRecipe.SealHours / api.World.Calendar.HoursPerDay, 1)) : Lang.Get("{0} hours", Math.Round(bebarrel.CurrentRecipe.SealHours));
                    text += "\n" + Lang.Get("Sealed for {0} / {1}", timePassedText, timeTotalText);
                }
            }

            return text + aftertext;
        }


        public override void TryFillFromBlock(EntityItem byEntityItem, BlockPos pos)
        {
            // Don't fill when dropped as item in water
        }


    }
}
