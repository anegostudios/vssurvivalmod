using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class BucketTextureSource : ITexPositionSource
    {
        public ItemStack forContents;
        private ICoreClientAPI capi;
        ITexPositionSource contentsTexSource;

        public BucketTextureSource(ICoreClientAPI capi, ItemStack forContents, Block blockTextureSource)
        {
            this.capi = capi;
            this.forContents = forContents;

            contentsTexSource = capi.Tesselator.GetTexSource(blockTextureSource, 0, true);
        }
        
        public TextureAtlasPosition this[string textureCode]
        {
            get
            {
                TextureAtlasPosition tp = contentsTexSource["inBucket"];
                if (tp != null) return tp;

                tp = contentsTexSource[forContents.Collectible.Code.Path.ToLowerInvariant()];
                return tp == null ? contentsTexSource["up"] : tp;
            }
        }

        public int AtlasSize => capi.BlockTextureAtlas.Size;
    }


    public class WaterTightContainableProps
    {
        public bool Containable;
        public float ItemsPerLitre;
        public AssetLocation FillSpillSound = new AssetLocation("sounds/block/water");
        public AssetLocation BlockTextureSource;
        public bool AllowSpill = true;
        public WhenSpilledProps WhenSpilled;
        public WhenFilledProps WhenFilled;

        public enum EnumSpilledAction { PlaceBlock, DropContents };

        public class WhenFilledProps
        {
            public JsonItemStack Stack;
        }

        public class WhenSpilledProps
        {
            public Dictionary<int, JsonItemStack> StackByFillLevel;
            public EnumSpilledAction Action;
            public JsonItemStack Stack;
        }
    }


    // Concept time
    // - Buckets don't directly hold liquids, they contain itemstacks. In case of liquids they are simply "portions" of that liquid. i.e. a "waterportion" item
    //
    // - The item/block that can be placed into the bucket must have the item/block attribute waterTightContainerProps: { containable: true, itemsPerLitre: 1 }
    //   'itemsPerLitre' defines how many items constitute one litre.

    // - Further item/block more attributes lets you define if a liquid can be obtained from a block source and what should come out when spilled:
    //   - waterTightContainerProps: { containable: true, whenSpilled: { action: "placeblock", stack: { class: "block", code: "water-7" } }  }
    //   or
    //   - waterTightContainerProps: { containable: true, whenSpilled: { action: "dropcontents", stack: { class: "item", code: "honeyportion" } }  }
    // 
    // - BlockBucket has methods for placing/taking liquids from a bucket stack or a placed bucket block
    public class BlockBucket : BlockContainer
    {
        public virtual float BucketCapacityLitres => 10;

        #region Take/Remove Contents

        /// <summary>
        /// Fill level in litres. From 0...BucketCapacity
        /// </summary>
        /// <param name="world"></param>
        /// <param name="bucketStack"></param>
        /// <returns></returns>
        public float GetCurrentLitres(IWorldAccessor world, ItemStack bucketStack)
        {
            WaterTightContainableProps props = GetContentProps(world, bucketStack);
            if (props == null) return 0;

            return GetContent(world, bucketStack).StackSize / props.ItemsPerLitre;
        }

        /// <summary>
        /// Fill level in litres. From 0...BucketCapacity. From a placed bucket block
        /// </summary>
        /// <param name="world"></param>
        /// <param name="bucketStack"></param>
        /// <returns></returns>
        public float GetCurrentLitres(IWorldAccessor world, BlockPos pos)
        {
            WaterTightContainableProps props = GetContentProps(world, pos);
            if (props == null) return 0;

            return GetContent(world, pos).StackSize / props.ItemsPerLitre;
        }


        /// <summary>
        /// Retrives the containable properties of the currently contained itemstack
        /// </summary>
        /// <param name="world"></param>
        /// <param name="bucketStack"></param>
        /// <returns></returns>
        public WaterTightContainableProps GetContentProps(IWorldAccessor world, ItemStack bucketStack)
        {
            ItemStack stack = GetContent(world, bucketStack);
            return GetStackProps(stack);
        }

        public WaterTightContainableProps GetStackProps(ItemStack stack) { 
            try
            {
                return stack?.ItemAttributes?["waterTightContainerProps"].AsObject<WaterTightContainableProps>();
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Retrives the containable properties of the currently contained itemstack of a placed water bucket
        /// </summary>
        /// <param name="world"></param>
        /// <param name="pos"></param>
        /// <param name="bucketStack"></param>
        /// <returns></returns>
        public WaterTightContainableProps GetContentProps(IWorldAccessor world, BlockPos pos)
        {
            BlockEntityBucket bebucket = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityBucket;
            if (bebucket == null) return null;

            ItemStack stack = bebucket.GetContent();

            try
            {
                return stack?.ItemAttributes?["waterTightContainerProps"].AsObject<WaterTightContainableProps>();
            }
            catch (Exception)
            {
                return null;
            }

        }

        /// <summary>
        /// Sets the buckets contents to given stack
        /// </summary>
        /// <param name="bucketStack"></param>
        /// <param name="content"></param>
        public void SetContent(ItemStack bucketStack, ItemStack content)
        {
            if (content == null)
            {
                SetContents(bucketStack, new ItemStack[] { });
                return;
            }
            SetContents(bucketStack, new ItemStack[] { content });
        }


        /// <summary>
        /// Sets the buckets contents to placed bucked block
        /// </summary>
        /// <param name="bucketStack"></param>
        /// <param name="content"></param>
        public void SetContent(IWorldAccessor world, BlockPos pos, ItemStack content)
        {
            BlockEntityBucket bebucket = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityBucket;
            if (bebucket == null) return;
            bebucket.SetContent(content);
        }


        /// <summary>
        /// Retrieves the contents of the bucket of a bucket stack
        /// </summary>
        /// <param name="world"></param>
        /// <param name="bucketStack"></param>
        /// <returns></returns>
        public ItemStack GetContent(IWorldAccessor world, ItemStack bucketStack)
        {
            ItemStack[] stacks = GetContents(world, bucketStack);
            return (stacks != null && stacks.Length > 0) ? stacks[0] : null;
        }

        /// <summary>
        /// Retrieves the contents of a placed bucket
        /// </summary>
        /// <param name="world"></param>
        /// <param name="pos"></param>
        /// <returns></returns>
        public ItemStack GetContent(IWorldAccessor world, BlockPos pos)
        {
            BlockEntityBucket bebucket = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityBucket;
            if (bebucket == null) return null;
            return bebucket.GetContent();
        }

        /// <summary>
        /// Tries to take out as much items/liquid as possible and returns it
        /// </summary>
        /// <param name="world"></param>
        /// <param name="bucketStack"></param>
        /// <param name="quantity"></param>
        /// <returns></returns>
        public ItemStack TryTakeContent(IWorldAccessor world, ItemStack bucketStack, int quantity)
        {
            ItemStack stack = GetContent(world, bucketStack);
            if (stack == null) return null;

            ItemStack takenStack = stack.Clone();
            takenStack.StackSize = quantity;

            stack.StackSize -= quantity;
            if (stack.StackSize <= 0) SetContent(bucketStack, null);
            else SetContent(bucketStack, stack);

            return takenStack;
        }
        

        /// <summary>
        /// Tries to take out as much items/liquid as possible from a placed bucket and returns it
        /// </summary>
        /// <param name="world"></param>
        /// <param name="pos"></param>
        /// <param name="quantity"></param>
        public ItemStack TryTakeContent(IWorldAccessor world, BlockPos pos, int quantity)
        {
            BlockEntityBucket bebucket = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityBucket;
            if (bebucket == null) return null;

            ItemStack stack = bebucket.GetContent();
            if (stack == null) return null;

            ItemStack takenStack = stack.Clone();
            takenStack.StackSize = quantity;

            stack.StackSize -= quantity;
            if (stack.StackSize <= 0) bebucket.SetContent(null);
            else bebucket.SetContent(stack);

            return takenStack;
        }

        #endregion


        #region PutContents

        /// <summary>
        /// Tries to place in items/liquid and returns actually inserted quantity
        /// </summary>
        /// <param name="world"></param>
        /// <param name="bucketStack"></param>
        /// <param name="quantity"></param>
        /// <returns></returns>
        public int TryAddContent(IWorldAccessor world, ItemStack bucketStack, ItemStack contentStack, int quantity)
        {
            ItemStack stack = GetContent(world, bucketStack);
            if (stack == null)
            {
                WaterTightContainableProps props = GetStackProps(contentStack);
                if (props == null || !props.Containable) return 0;


                int placeableItems = (int)(BucketCapacityLitres * props.ItemsPerLitre);

                ItemStack placedstack = contentStack.Clone();
                placedstack.StackSize = Math.Min(quantity, placeableItems);
                SetContent(bucketStack, placedstack);

                return Math.Min(quantity, placeableItems);
            } else
            {
                if (!stack.Equals(world, contentStack, GlobalConstants.IgnoredStackAttributes)) return 0;

                WaterTightContainableProps props = GetContentProps(world, bucketStack);

                float maxItems = BucketCapacityLitres * props.ItemsPerLitre;
                int placeableItems = (int)(maxItems - (float)stack.StackSize / props.ItemsPerLitre);

                stack.StackSize += Math.Min(placeableItems, quantity);

                return Math.Min(placeableItems, quantity);
            }
        }

        /// <summary>
        /// Tries to put as much items/liquid as possible into a placed bucket and returns it how much items it actually moved
        /// </summary>
        /// <param name="world"></param>
        /// <param name="pos"></param>
        /// <param name="quantity"></param>
        public int TryAddContent(IWorldAccessor world, BlockPos pos, ItemStack contentStack, int quantity)
        {
            ItemStack stack = GetContent(world, pos);
            if (stack == null)
            {
                WaterTightContainableProps props = GetStackProps(contentStack);
                if (props == null || !props.Containable) return 0;


                float maxItems = BucketCapacityLitres * props.ItemsPerLitre;
                int placeableItems = (int)(maxItems - (float)contentStack.StackSize / props.ItemsPerLitre);

                ItemStack placedstack = contentStack.Clone();
                placedstack.StackSize = Math.Min(quantity, placeableItems);
                SetContent(world, pos, placedstack);

                return Math.Min(quantity, placeableItems);
            }
            else
            {
                if (!stack.Equals(world, contentStack, GlobalConstants.IgnoredStackAttributes)) return 0;

                WaterTightContainableProps props = GetContentProps(world, pos);

                float maxItems = BucketCapacityLitres * props.ItemsPerLitre;
                int placeableItems = (int)(maxItems - (float)stack.StackSize);

                stack.StackSize += Math.Min(placeableItems, quantity);
                world.BlockAccessor.GetBlockEntity(pos).MarkDirty(true);

                return Math.Min(placeableItems, quantity);
            }

            
        }

        #endregion


        #region Render
        public override void OnBeforeRender(ICoreClientAPI capi, ItemStack itemstack, EnumItemRenderTarget target, ref ItemRenderInfo renderinfo)
        {
            Dictionary<int, MeshRef> meshrefs = null;

            object obj;
            if (capi.ObjectCache.TryGetValue("bucketMeshRefs", out obj))
            {
                meshrefs = obj as Dictionary<int, MeshRef>;
            }
            else
            {
                capi.ObjectCache["bucketMeshRefs"] = meshrefs = new Dictionary<int, MeshRef>();
            }

            ItemStack contentStack = GetContent(capi.World, itemstack);
            if (contentStack == null) return;

            int hashcode = GetBucketHashCode(capi.World, contentStack);

            MeshRef meshRef = null;

            if (!meshrefs.TryGetValue(hashcode, out meshRef))
            {
                meshrefs[hashcode] = meshRef = capi.Render.UploadMesh(GenMesh(capi, contentStack));
            }

            renderinfo.ModelRef = meshRef;
        }



        public int GetBucketHashCode(IClientWorldAccessor world, ItemStack contentStack)
        {
            string s = contentStack.StackSize + "x" + contentStack.Collectible.Code.ToShortString();
            return s.GetHashCode();
        }



        public override void OnUnloaded(ICoreAPI api)
        {
            ICoreClientAPI capi = api as ICoreClientAPI;
            if (capi == null) return;

            object obj;
            if (capi.ObjectCache.TryGetValue("bucketMeshRefs", out obj))
            {
                Dictionary<int, MeshRef> meshrefs = obj as Dictionary<int, MeshRef>;

                foreach (var val in meshrefs)
                {
                    val.Value.Dispose();
                }

                capi.ObjectCache.Remove("bucketMeshRefs");
            }
        }



        public MeshData GenMesh(ICoreClientAPI capi, ItemStack contentStack, BlockPos forBlockPos = null)
        {
            Shape shape = capi.Assets.TryGet("shapes/block/wood/bucket/empty.json").ToObject<Shape>();
            MeshData bucketmesh;
            capi.Tesselator.TesselateShape(this, shape, out bucketmesh);

            if (contentStack != null)
            {
                WaterTightContainableProps props = GetStackProps(contentStack);
                Block contentBlock = capi.World.GetBlock(props.BlockTextureSource);
                if (contentBlock == null) return bucketmesh;

                BucketTextureSource contentSource = new BucketTextureSource(capi, contentStack, contentBlock);
                shape = capi.Assets.TryGet("shapes/block/wood/bucket/contents.json").ToObject<Shape>();
                MeshData contentMesh;
                capi.Tesselator.TesselateShape("bucket", shape, out contentMesh, contentSource, new Vec3f(Shape.rotateX, Shape.rotateY, Shape.rotateZ));


                contentMesh.Translate(0, GameMath.Min(7 / 16f, contentStack.StackSize / props.ItemsPerLitre * 0.7f / 16f), 0);

                if (contentBlock.TintIndex > 0)
                {
                    int col = capi.ApplyColorTintOnRgba(contentBlock.TintIndex, ColorUtil.WhiteArgb, 196, 128, false);
                    if (forBlockPos != null)
                    {
                        col = capi.ApplyColorTintOnRgba(contentBlock.TintIndex, ColorUtil.WhiteArgb, forBlockPos.X, forBlockPos.Y, forBlockPos.Z, false);
                    }

                    byte[] rgba = ColorUtil.ToBGRABytes(col);

                    for (int i = 0; i < contentMesh.Rgba.Length; i++)
                    {
                        contentMesh.Rgba[i] = (byte)((contentMesh.Rgba[i] * rgba[i % 4]) / 255);
                    }
                }

                bucketmesh.AddMeshData(contentMesh);
            }

            return bucketmesh;
        }

        #endregion

        #region Held Interact

        public override void OnHeldInteractStart(IItemSlot itemslot, IEntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandHandling handHandling)
        {
            if (blockSel == null) return;
            IPlayer byPlayer = (byEntity as EntityPlayer)?.Player;

            Block targetedBlock = byEntity.World.BlockAccessor.GetBlock(blockSel.Position);
            if (targetedBlock.HasBehavior(typeof(BlockBehaviorLiquidContainer), true))
            {
                if (!byEntity.World.TestPlayerAccessBlock(byPlayer, blockSel.Position, EnumBlockAccessFlags.Use))
                {
                    return;
                }

                BlockBehaviorLiquidContainer bh = targetedBlock.GetBehavior(typeof(BlockBehaviorLiquidContainer), true) as BlockBehaviorLiquidContainer;

                if (bh.OnInteractWithBucket(itemslot, byEntity, blockSel))
                {
                    handHandling = EnumHandHandling.PreventDefaultAction;
                    return;
                }
            }


            if (!byEntity.World.TestPlayerAccessBlock(byPlayer, blockSel.Position, EnumBlockAccessFlags.BuildOrBreak))
            {
                return;
            }

            ItemStack contentStack = GetContent(byEntity.World, itemslot.Itemstack);

            bool isEmpty = contentStack == null;


            if (isEmpty)
            {
                TryFillBucketFromBlock(itemslot, byEntity, blockSel.Position); 
            }
            else
            {
                BlockBucket targetBucket = targetedBlock as BlockBucket;
                if (targetBucket != null)
                {
                    if (targetBucket.TryAddContent(byEntity.World, blockSel.Position, contentStack, 1) > 0)
                    {
                        TryTakeContent(byEntity.World, itemslot.Itemstack, 1);
                        WaterTightContainableProps props = GetContentProps(byEntity.World, itemslot.Itemstack);

                        byEntity.World.PlaySoundAt(props.FillSpillSound, blockSel.Position.X, blockSel.Position.Y, blockSel.Position.Z, byPlayer);
                    }
                       
                } else
                {
                    SpillContents(itemslot, byEntity, blockSel);
                }

                
            }

            // Prevent placing on normal use
            handHandling = EnumHandHandling.PreventDefaultAction;
        }



        public void TryFillBucketFromBlock(IItemSlot itemslot, IEntityAgent byEntity, BlockPos pos)
        {
            IPlayer byPlayer = (byEntity as EntityPlayer)?.Player;
            IBlockAccessor blockAcc = byEntity.World.BlockAccessor;

            Block block = blockAcc.GetBlock(pos);
            if (block.Attributes?["waterTightContainerProps"].Exists == false) return;

            WaterTightContainableProps props = block.Attributes?["waterTightContainerProps"]?.AsObject<WaterTightContainableProps>();
            if (props?.WhenFilled == null || !props.Containable) return;

            props.WhenFilled.Stack.Resolve(byEntity.World, "blockbucket");

            ItemStack contentStack = props.WhenFilled.Stack.ResolvedItemstack;
            contentStack.StackSize = (int)(props.ItemsPerLitre * BucketCapacityLitres);

            
            ItemStack fullBucketStack = new ItemStack(this);
            SetContent(fullBucketStack, contentStack);

            if (itemslot.Itemstack.StackSize <= 1)
            {
                itemslot.Itemstack = fullBucketStack;
            }
            else
            {
                itemslot.TakeOut(1);
                if (!byPlayer.InventoryManager.TryGiveItemstack(fullBucketStack, true))
                {
                    byEntity.World.SpawnItemEntity(fullBucketStack, byEntity.LocalPos.XYZ);
                }
            }

            itemslot.MarkDirty();
            byEntity.World.PlaySoundAt(props.FillSpillSound, pos.X, pos.Y, pos.Z, byPlayer);
        }


        public void TryFillBucketFromBlock(EntityItem byEntityItem, BlockPos pos)
        {
            IWorldAccessor world = byEntityItem.World;
            
            Block block = world.BlockAccessor.GetBlock(pos);

            if (block.Attributes?["waterTightContainerProps"].Exists == false) return;
            WaterTightContainableProps props = block.Attributes?["waterTightContainerProps"].AsObject<WaterTightContainableProps>();
            if (props?.WhenFilled == null || !props.Containable) return;

            if (props.WhenFilled.Stack.ResolvedItemstack == null) props.WhenFilled.Stack.Resolve(world, "blockbucket");

            ItemStack whenFilledStack = props.WhenFilled.Stack.ResolvedItemstack;
            ItemStack contentStack = GetContent(world, byEntityItem.Itemstack);
            bool canFill = contentStack == null || (contentStack.Equals(world, whenFilledStack, GlobalConstants.IgnoredStackAttributes) && GetCurrentLitres(world, byEntityItem.Itemstack) < BucketCapacityLitres);
            if (!canFill) return;

            whenFilledStack.StackSize = (int)(props.ItemsPerLitre * BucketCapacityLitres);

            ItemStack fullBucketStack = new ItemStack(this);
            SetContent(fullBucketStack, whenFilledStack);

            if (byEntityItem.Itemstack.StackSize <= 1)
            {
                byEntityItem.Itemstack = fullBucketStack;
            }
            else
            {
                byEntityItem.Itemstack.StackSize--;
                world.SpawnItemEntity(fullBucketStack, byEntityItem.LocalPos.XYZ);
            }

            world.PlaySoundAt(props.FillSpillSound, pos.X, pos.Y, pos.Z, null);
        }


        private bool SpillContents(IItemSlot bucketSlot, IEntityAgent byEntity, BlockSelection blockSel)
        {
            BlockPos pos = blockSel.Position;
            IPlayer byPlayer = (byEntity as EntityPlayer)?.Player;
            IBlockAccessor blockAcc = byEntity.World.BlockAccessor;
            BlockPos secondPos = blockSel.Position.AddCopy(blockSel.Face);


            WaterTightContainableProps props = GetContentProps(byEntity.World, bucketSlot.Itemstack);

            if (!props.AllowSpill) return false;
            if (props?.WhenSpilled == null) return false;

            if (props.WhenSpilled.Action == WaterTightContainableProps.EnumSpilledAction.PlaceBlock)
            {
                Block waterBlock = byEntity.World.GetBlock(props.WhenSpilled.Stack.Code);

                if (props.WhenSpilled.StackByFillLevel != null)
                {
                    float currentlitres = GetCurrentLitres(byEntity.World, bucketSlot.Itemstack);
                    JsonItemStack fillLevelStack = null;
                    props.WhenSpilled.StackByFillLevel.TryGetValue((int)currentlitres, out fillLevelStack);
                    if (fillLevelStack != null) waterBlock = byEntity.World.GetBlock(fillLevelStack.Code);
                }

                if (blockAcc.GetBlock(pos).IsLiquid())
                {
                    blockAcc.SetBlock(waterBlock.BlockId, pos);
                    blockAcc.MarkBlockDirty(pos);
                }
                else
                {
                    blockAcc.SetBlock(waterBlock.BlockId, secondPos);
                    blockAcc.MarkBlockDirty(secondPos);
                }
            }

            if (props.WhenSpilled.Action == WaterTightContainableProps.EnumSpilledAction.DropContents)
            {
                props.WhenSpilled.Stack.Resolve(byEntity.World, "bucketspill");

                ItemStack stack = props.WhenSpilled.Stack.ResolvedItemstack.Clone();
                stack.StackSize = (int)(props.ItemsPerLitre * GetContent(byEntity.World, bucketSlot.Itemstack).StackSize);

                byEntity.World.SpawnItemEntity(stack, blockSel.Position.ToVec3d().Add(blockSel.HitPosition));
            }


            ItemStack emptyBucketStack = new ItemStack(this);

            if (bucketSlot.Itemstack.StackSize <= 1)
            {
                bucketSlot.Itemstack = emptyBucketStack;
                bucketSlot.MarkDirty();
            }
            else
            {
                bucketSlot.TakeOut(1);
                if (!byPlayer.InventoryManager.TryGiveItemstack(emptyBucketStack, true))
                {
                    byEntity.World.SpawnItemEntity(emptyBucketStack, byEntity.LocalPos.XYZ);
                }
            }

            byEntity.World.PlaySoundAt(props.FillSpillSound, pos.X, pos.Y, pos.Z, byPlayer);
            return true;
        }





        #endregion



        public override void OnGroundIdle(EntityItem entityItem)
        {
            base.OnGroundIdle(entityItem);

            IWorldAccessor world = entityItem.World;
            if (world.Side != EnumAppSide.Server) return;
            
            if (entityItem.Swimming && world.Rand.NextDouble() < 0.03)
            {
                TryFillBucketFromBlock(entityItem, entityItem.LocalPos.AsBlockPos);
            }
        }

        public override void GetHeldItemInfo(ItemStack stack, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(stack, dsc, world, withDebugInfo);

            float litres = GetCurrentLitres(world, stack);
            ItemStack contentStack = GetContent(world, stack);

            if (litres <= 0) dsc.Append(Lang.Get("Empty"));

            else
            {
                string inbucketname = Lang.Get("inbucket-" + contentStack.Class.ToString().ToLowerInvariant() + "-" + contentStack.Collectible.Code.Path);
                dsc.Append(Lang.Get("Contents: {0} litres of {1}", litres, inbucketname));
            }
        }

        public override string GetPlacedBlockInfo(IWorldAccessor world, BlockPos pos, IPlayer forPlayer)
        {
            float litres = GetCurrentLitres(world, pos);
            ItemStack contentStack = GetContent(world, pos);
            

            if (litres <= 0) return Lang.Get("Empty");

            string inbucketname = Lang.Get("inbucket-" + contentStack.Class.ToString().ToLowerInvariant() + "-" + contentStack.Collectible.Code.Path);
            return Lang.Get("Contents: {0} litres of {1}", litres, inbucketname);
        }


        public override bool MatchesForCrafting(ItemStack inputStack, GridRecipe gridRecipe, CraftingRecipeIngredient ingredient)
        {
            if (gridRecipe.Attributes?["bucketProps"].Exists != true)
            {
                return base.MatchesForCrafting(inputStack, gridRecipe, ingredient);
            }

            string contentCode = gridRecipe.Attributes["bucketProps"]["requiresContent"]["code"].AsString();
            string contentType = gridRecipe.Attributes["bucketProps"]["requiresContent"]["type"].AsString();

            ItemStack contentStack = GetContent(api.World, inputStack);
            if (contentStack == null) return false;

            int q = gridRecipe.Attributes["bucketProps"]["requiresQuantity"].AsInt();

            return
                contentStack.Class.ToString().ToLowerInvariant() == contentType.ToLowerInvariant()
                && WildCardMatch(contentStack.Collectible.Code, new AssetLocation(contentCode))
                && contentStack.StackSize >= q
            ;
        }

        public override void OnConsumedByCrafting(IItemSlot[] allInputSlots, IItemSlot stackInSlot, GridRecipe gridRecipe, CraftingRecipeIngredient fromIngredient, IPlayer byPlayer, int quantity)
        {
            if (gridRecipe.Attributes?["bucketProps"].Exists != true)
            {
                base.OnConsumedByCrafting(allInputSlots, stackInSlot, gridRecipe, fromIngredient, byPlayer, quantity);
                return;
            }

            int q = gridRecipe.Attributes["bucketProps"]["requiresQuantity"].AsInt();
            TryTakeContent(byPlayer.Entity.World, stackInSlot.Itemstack, q);
        }

    }
}
