using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vintagestory.API;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class BlockBarrel : BlockLiquidContainerBase
    {

        public override int GetContainerSlotId(IWorldAccessor world, BlockPos pos)
        {
            return 1;
        }

        public override int GetContainerSlotId(IWorldAccessor world, ItemStack containerStack)
        {
            return 1;
        }

        #region Render
        public override void OnBeforeRender(ICoreClientAPI capi, ItemStack itemstack, EnumItemRenderTarget target, ref ItemRenderInfo renderinfo)
        {
            Dictionary<int, MeshRef> meshrefs;

            object obj;
            if (capi.ObjectCache.TryGetValue("barrelMeshRefs", out obj))
            {
                meshrefs = obj as Dictionary<int, MeshRef>;
            }
            else
            {
                capi.ObjectCache["barrelMeshRefs"] = meshrefs = new Dictionary<int, MeshRef>();
            }

            ItemStack[] contentStacks = GetContents(capi.World, itemstack);
            if (contentStacks == null || contentStacks.Length == 0) return;

            bool issealed = itemstack.Attributes.GetBool("sealed");

            int hashcode = GetBarrelHashCode(capi.World, contentStacks[0], contentStacks.Length > 1 ? contentStacks[1] : null);

            MeshRef meshRef;

            if (!meshrefs.TryGetValue(hashcode, out meshRef))
            {
                MeshData meshdata = GenMesh(contentStacks[0], contentStacks.Length > 1 ? contentStacks[1] : null, issealed);
                //meshdata.Rgba2 = null;
                meshrefs[hashcode] = meshRef = capi.Render.UploadMesh(meshdata);
            }

            renderinfo.ModelRef = meshRef;
        }



        public int GetBarrelHashCode(IClientWorldAccessor world, ItemStack contentStack, ItemStack liquidStack)
        {
            string s = contentStack?.StackSize + "x" + contentStack?.Collectible.Code.ToShortString();
            s+= liquidStack?.StackSize + "x" + liquidStack?.Collectible.Code.ToShortString();
            return s.GetHashCode();
        }


        public override void OnUnloaded(ICoreAPI api)
        {
            ICoreClientAPI capi = api as ICoreClientAPI;
            if (capi == null) return;

            object obj;
            if (capi.ObjectCache.TryGetValue("barrelMeshRefs", out obj))
            {
                Dictionary<int, MeshRef> meshrefs = obj as Dictionary<int, MeshRef>;

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
            if (world.Side == EnumAppSide.Server && (byPlayer == null || byPlayer.WorldData.CurrentGameMode != EnumGameMode.Creative))
            {
                ItemStack[] drops = new ItemStack[] { new ItemStack(this) };

                for (int i = 0; i < drops.Length; i++)
                {
                    world.SpawnItemEntity(drops[i], new Vec3d(pos.X + 0.5, pos.Y + 0.5, pos.Z + 0.5), null);
                }

                world.PlaySoundAt(Sounds.GetBreakSound(byPlayer), pos.X, pos.Y, pos.Z, byPlayer);
            }

            if (EntityClass != null)
            {
                BlockEntity entity = world.BlockAccessor.GetBlockEntity(pos);
                if (entity != null)
                {
                    entity.OnBlockBroken();
                }
            }

            world.BlockAccessor.SetBlock(0, pos);
        }





        public override void OnHeldAttackStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandHandling handling)
        {
            
        }

        public override int TryPutContent(IWorldAccessor world, ItemStack containerStack, ItemStack contentStack, int desiredItems)
        {
            return 0;
        }


        public MeshData GenMesh(ItemStack contentStack, ItemStack liquidContentStack, bool issealed, BlockPos forBlockPos = null)
        {
            ICoreClientAPI capi = api as ICoreClientAPI;

            Shape shape = capi.Assets.TryGet("shapes/block/wood/barrel/"+ (issealed ? "closed" : "empty") +".json").ToObject<Shape>();
            MeshData barrelMesh;
            capi.Tesselator.TesselateShape(this, shape, out barrelMesh);

            if (!issealed)
            {
                MeshData contentMesh = getContentMesh(contentStack, forBlockPos, "contents.json");
                if (contentMesh != null) barrelMesh.AddMeshData(contentMesh);

                bool isopaque = liquidContentStack?.ItemAttributes?["waterTightContainerProps"]?["isopaque"].AsBool(false) == true;
                bool isliquid = liquidContentStack?.ItemAttributes?["waterTightContainerProps"].Exists == true;
                if (liquidContentStack != null && (isliquid || contentStack == null))
                {
                    string shapefilename = isliquid && !isopaque ? "liquidcontents.json" : "contents.json";
                    contentMesh = getContentMesh(liquidContentStack, forBlockPos, shapefilename);
                    if (contentMesh != null) barrelMesh.AddMeshData(contentMesh);
                }

                if (forBlockPos != null)
                {
                    // Water flags
                    barrelMesh.CustomInts = new CustomMeshDataPartInt(barrelMesh.FlagsCount);
                    barrelMesh.CustomInts.Values.Fill(0x4000000); // light foam only
                    barrelMesh.CustomInts.Count = barrelMesh.FlagsCount;

                    barrelMesh.CustomFloats = new CustomMeshDataPartFloat(barrelMesh.FlagsCount * 2);
                    barrelMesh.CustomFloats.Count = barrelMesh.FlagsCount * 2;
                }
            }


            return barrelMesh;
        }

        #endregion


        protected MeshData getContentMesh(ItemStack stack, BlockPos forBlockPos, string shapefilename)
        {
            ICoreClientAPI capi = api as ICoreClientAPI;

            WaterTightContainableProps props = GetInContainerProps(stack);
            ITexPositionSource contentSource = null;


            float fillHeight = 0;

            if (props != null)
            {
                contentSource = new ContainerTextureSource(capi, stack, props.Texture);
                fillHeight = GameMath.Min(1f, stack.StackSize / props.ItemsPerLitre / Math.Max(50, props.MaxStackSize)) * 10f / 16f;

                if (props.Texture == null) return null;
            }
            else
            {
                JsonObject obj = stack?.ItemAttributes?["inContainerTexture"];
                if (obj != null && obj.Exists)
                {
                    contentSource = new ContainerTextureSource(capi, stack, obj.AsObject<CompositeTexture>());
                    fillHeight = GameMath.Min(10 / 16f, 0.7f * stack.StackSize / stack.Collectible.MaxStackSize);
                } else
                {
                    if (stack?.Block != null && (stack.Block.DrawType == EnumDrawType.Cube || stack.Block.Shape.Base.Path.Contains("basic/cube")))
                    {
                        contentSource = new BlockTopTextureSource(capi, stack.Block);
                        fillHeight = GameMath.Min(10 / 16f, 0.7f * 1);
                    } else if (stack != null)
                    {
                        // looks silly :D
                        /*if (stack.Class == EnumItemClass.Block)
                        {
                            contentSource = new ContainerTextureSource(capi, stack, stack.Block.Textures.FirstOrDefault().Value);
                        } else
                        {
                            contentSource = new ContainerTextureSource(capi, stack, stack.Item.FirstTexture);
                        }
                        

                        fillHeight = GameMath.Min(10 / 16f, 0.7f * stack.StackSize / stack.Collectible.MaxStackSize);*/
                    }
                }
            }



            if (stack != null && contentSource != null)
            {
                Shape shape = capi.Assets.TryGet("shapes/block/wood/barrel/" + shapefilename).ToObject<Shape>();
                MeshData contentMesh;
                capi.Tesselator.TesselateShape("barrel", shape, out contentMesh, contentSource, new Vec3f(Shape.rotateX, Shape.rotateY, Shape.rotateZ));

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



        public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot)
        {
            return new WorldInteraction[]
            {
                new WorldInteraction()
                {
                    ActionLangCode = "heldhelp-place",
                    HotKeyCode = "sneak",
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

            if (Attributes?["capacityLitres"].Exists == true)
            {
                capacityLitresFromAttributes = Attributes["capacityLitres"].AsInt(50);
            }


            if (api.Side != EnumAppSide.Client) return;
            ICoreClientAPI capi = api as ICoreClientAPI;

            interactions = ObjectCacheUtil.GetOrCreate(api, "liquidContainerBase", () =>
            {
                List<ItemStack> liquidContainerStacks = new List<ItemStack>();

                foreach (CollectibleObject obj in api.World.Collectibles)
                {
                    if ((obj is BlockBowl && obj.LastCodePart() != "raw") || obj is ILiquidSource || obj is ILiquidSink || obj is BlockWateringCan)
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
                        HotKeyCode = "sneak",
                        Itemstacks = linenStack,
                        GetMatchingStacks = (wi, bs, ws) =>
                        {
                            BlockEntityBarrel bebarrel = api.World.BlockAccessor.GetBlockEntity(bs.Position) as BlockEntityBarrel;
                            if (bebarrel?.inventory[1].Itemstack?.Item?.Code?.Path == "cottagecheeseportion") return linenStack;
                            return null;
                        }
                    }
                };
            });
        }



        public override void OnHeldInteractStart(ItemSlot itemslot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling)
        {
            base.OnHeldInteractStart(itemslot, byEntity, blockSel, entitySel, firstEvent, ref handHandling);
        }


        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
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

            if (!handled && !byPlayer.WorldData.EntityControls.Sneak && blockSel.Position != null)
            {
                if (bebarrel != null)
                {
                    bebarrel.OnBlockInteract(byPlayer);
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

            float litres = GetCurrentLitres(world, pos);
            if (litres <= 0) text = "";

            BlockEntityBarrel bebarrel = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityBarrel;
            if (bebarrel != null)
            {
                ItemSlot slot = bebarrel.Inventory[0];
                if (!slot.Empty)
                {
                    if (text.Length > 0) text += "\n";
                    else text += Lang.Get("Contents:") + "\n";

                    text += Lang.Get("{0}x {1}", slot.Itemstack.StackSize, slot.Itemstack.GetName());

                    text += BlockBucket.PerishableInfoCompact(api, slot, 0, false);
                }

                if (bebarrel.Sealed && bebarrel.CurrentRecipe != null)
                {
                    double hoursPassed = world.Calendar.TotalHours - bebarrel.SealedSinceTotalHours;
                    string timePassedText = hoursPassed > 24 ? Lang.Get("{0} days", Math.Round(hoursPassed / api.World.Calendar.HoursPerDay, 1)) : Lang.Get("{0} hours", Math.Round(hoursPassed));
                    string timeTotalText = bebarrel.CurrentRecipe.SealHours > 24 ? Lang.Get("{0} days", Math.Round(bebarrel.CurrentRecipe.SealHours / api.World.Calendar.HoursPerDay, 1)) : Lang.Get("{0} hours", Math.Round(bebarrel.CurrentRecipe.SealHours));
                    text += "\n" + Lang.Get("Sealed for {0} / {1}", timePassedText, timeTotalText);
                }
                
            }


            return text;
        }


        public override void TryFillFromBlock(EntityItem byEntityItem, BlockPos pos)
        {
            // Don't fill when dropped as item in water
        }


    }
}
