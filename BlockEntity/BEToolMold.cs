using Newtonsoft.Json;
using System;
using System.Collections.Generic;
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
    public class BlockEntityToolMold : BlockEntity, ILiquidMetalSink, ITemperatureSensitive, ITexPositionSource, IRotatable
    {
        protected ToolMoldRenderer renderer;
        public MeshData MoldMesh;
        protected Cuboidf[] fillQuadsByLevel = null;
        protected int requiredUnits = 100;
        protected float fillHeight = 1;
        protected bool breaksWhenFilled;

        public ItemStack MetalContent;
        public int FillLevel = 0;
        public bool FillSide;
        public bool Shattered;

        public float Temperature => MetalContent?.Collectible.GetTemperature(Api.World, MetalContent) ?? 0;
        public bool IsHardened => Temperature < 0.3f * MetalContent?.Collectible.GetMeltingPoint(Api.World, null, new DummySlot(MetalContent));
        public bool IsLiquid => Temperature > 0.8f * MetalContent?.Collectible.GetMeltingPoint(Api.World, null, new DummySlot(MetalContent));
        public bool IsFull => FillLevel >= requiredUnits;
        public bool CanReceiveAny => !Shattered && (Block.Variant["materialtype"] == "fired" || Block.Code.Path.Contains("burned"));
        public bool IsHot => Temperature >= 200;
        public bool BreaksWhenFilled => breaksWhenFilled;

        ICoreClientAPI capi;
        public float MeshAngle;
        bool hasMeshAngle = true; // We use this to convert pre-1.21 blocks to the new MeshAngle rotations

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            if (api is ICoreServerAPI sapi)OnLoadWithoutMeshAngle(sapi);

            if (Block == null || Block.Code == null || Block.Attributes == null) return;

            fillHeight = Block.Attributes["fillHeight"].AsFloat(1);
            requiredUnits = Block.Attributes["requiredUnits"].AsInt(100);
            breaksWhenFilled = Block.Attributes["breaksWhenFilled"].AsBool(false);

            if (Block.Attributes["fillQuadsByLevel"].Exists)
            {
                fillQuadsByLevel = Block.Attributes["fillQuadsByLevel"].AsObject<Cuboidf[]>();
            }


            if (fillQuadsByLevel == null)
            {
                fillQuadsByLevel = new Cuboidf[] { new Cuboidf(2, 0, 2, 14, 0, 14), };
            }

            capi = api as ICoreClientAPI;
            if (capi != null && !Shattered)
            {
                capi.Event.RegisterRenderer(renderer = new ToolMoldRenderer(this, capi, fillQuadsByLevel), EnumRenderStage.Opaque, "toolmoldrenderer");
                UpdateRenderer();

                if (MoldMesh == null) GenMeshes();
            }

            if (!Shattered)
            {
                RegisterGameTickListener(OnGameTick, 50);
            }
        }

        protected virtual void OnLoadWithoutMeshAngle(ICoreServerAPI sapi)
        {
            if (hasMeshAngle || (Block.Code.Domain != GlobalConstants.DefaultDomain && Block.FirstCodePart() != "toolmold")) return;

            string code = Block.Code.SecondCodePart();
            AssetLocation loc = null;

            if (code == "burned")
            {
                code = Block.LastCodePart();
                loc = new AssetLocation(Block.Code.ToShortString());

                if (code == "east" || code == "west" || code == "south")
                {
                    loc.WithoutPathAppendix("-" + code);
                }

                loc.WithPathAppendixOnce("-north");
            }
            else if (code != "blue")
            {
                loc = new AssetLocation("toolmold-blue-" + Block.CodeEndWithoutParts(2));
            }
            // for chunks that where not loaded on the initial start after the update the remapper will already have remapped those blocks
            else
            {
                loc = Block.Code;
            }

            var blockID = sapi.World.BlockAccessor.GetBlock(loc)?.BlockId ?? 0;
            if (blockID != 0)
            {
                sapi.World.BlockAccessor.ExchangeBlock(blockID, Pos);
                switch (code)
                {
                    case "gray":
                    case "east":
                        {
                            MeshAngle = -1 * GameMath.PIHALF;
                            break;
                        }
                    case "blue":
                    case "north":
                        {
                            MeshAngle = 0;
                            break;
                        }
                    case "brown":
                    case "west":
                        {
                            MeshAngle = GameMath.PIHALF;
                            break;
                        }
                    case "tan":
                    case "south":
                        {
                            MeshAngle = GameMath.PI;
                            break;
                        }
                }
            }

            hasMeshAngle = true;
        }


        private void OnGameTick(float dt)
        {
            if (renderer != null)
            {
                renderer.Level = (float)FillLevel * fillHeight / requiredUnits;
            }

            if (MetalContent != null && renderer != null)
            {
                renderer.stack = MetalContent;
                renderer.Temperature = Math.Min(1300, MetalContent.Collectible.GetTemperature(Api.World, MetalContent));
            }

        }



        public bool CanReceive(ItemStack metal)
        {
            return
                (MetalContent == null || (MetalContent.Collectible.Equals(MetalContent, metal, GlobalConstants.IgnoredStackAttributes) && !IsFull))
                && GetMoldedStacks(metal)?.Length > 0
                && !Shattered
            ;
        }


        public void BeginFill(Vec3d hitPosition)
        {
            FillSide = hitPosition.X >= 0.5f;
        }


        public bool OnPlayerInteract(IPlayer byPlayer, BlockFacing onFace, Vec3d hitPosition)
        {
            if (Shattered) return false;

            bool sneaking = byPlayer.Entity.Controls.ShiftKey;

            if (!sneaking)
            {
                if (byPlayer.Entity.Controls.HandUse != EnumHandInteract.None) return false;

                bool handled = TryTakeContents(byPlayer);

                if (!handled && FillLevel == 0)
                {
                    var activeStack = byPlayer.InventoryManager.ActiveHotbarSlot.Itemstack;
                    if (activeStack != null && activeStack.Collectible is not BlockToolMold and not BlockIngotMold) return handled;

                    var itemStack = new ItemStack(Block);
                    if (!byPlayer.InventoryManager.TryGiveItemstack(itemStack))
                    {
                        Api.World.SpawnItemEntity(itemStack, Pos.ToVec3d().Add(0.5, 0.2, 0.5));
                    }
                    Api.World.Logger.Audit("{0} Took 1x{1} from Tool mold at {2}.",
                        byPlayer.PlayerName,
                        itemStack.Collectible.Code,
                        Pos
                    );

                    Api.World.BlockAccessor.SetBlock(0, Pos);

                    if (Block.Sounds?.Place != null)
                    {
                        Api.World.PlaySoundAt(Block.Sounds.Place, Pos, -0.5, byPlayer, false);
                    }

                    handled = true;
                }

                return handled;
            }


            return false;
        }

        protected virtual bool TryTakeContents(IPlayer byPlayer)
        {
            if (Shattered) return false;
            if (BreaksWhenFilled)
            {
                (Api as ICoreClientAPI)?.TriggerIngameError(this, "breakswhenfilledrightclicked", Lang.Get("toolmold-breakswhenfilled-error"));
                return false;
            }

            if (Api is ICoreServerAPI) MarkDirty();

            if (MetalContent != null && IsFull && IsHardened)
            {
                Api.World.PlaySoundAt(new AssetLocation("sounds/block/ingot"), Pos, -0.5, byPlayer, false);

                if (Api is ICoreServerAPI)
                {
                    ItemStack[] outstacks = GetStateAwareMoldedStacks();

                    if (outstacks != null)
                    {
                        foreach (ItemStack outstack in outstacks)
                        {
                            var quantity = outstack.StackSize;
                            if (!byPlayer.InventoryManager.TryGiveItemstack(outstack))
                            {
                                Api.World.SpawnItemEntity(outstack, Pos.ToVec3d().Add(0.5, 0.2, 0.5));
                            }
                            Api.World.Logger.Audit("{0} Took {1}x{2} from Tool mold at {3}.",
                                byPlayer.PlayerName,
                                quantity,
                                outstack.Collectible.Code,
                                Pos
                            );
                        }

                        MetalContent = null;
                        FillLevel = 0;
                    }
                }

                UpdateRenderer();

                return true;
            }


            return false;
        }


        public void UpdateRenderer()
        {
            if (renderer == null) return;

            if (Shattered && renderer != null)
            {
                (Api as ICoreClientAPI).Event.UnregisterRenderer(renderer, EnumRenderStage.Opaque);
                renderer = null;
                return;
            }

            renderer.Level = (float)FillLevel * fillHeight / requiredUnits;


            if (MetalContent?.Collectible != null)
            {
                renderer.TextureName = new AssetLocation("block/metal/ingot/" + MetalContent.Collectible.LastCodePart() + ".png");
            }
            else
            {
                renderer.TextureName = null;
            }
        }

        public void ReceiveLiquidMetal(ItemStack metal, ref int amount, float temperature)
        {
            if (!IsFull && (MetalContent == null || metal.Collectible.Equals(MetalContent, metal, GlobalConstants.IgnoredStackAttributes)))
            {
                if (MetalContent == null)
                {
                    MetalContent = metal.Clone();
                    MetalContent.ResolveBlockOrItem(Api.World);
                    MetalContent.Collectible.SetTemperature(Api.World, MetalContent, temperature, false);
                    MetalContent.StackSize = 1;
                    (MetalContent.Attributes["temperature"] as ITreeAttribute)?.SetFloat("cooldownSpeed", 300);
                }
                else
                {
                    MetalContent.Collectible.SetTemperature(Api.World, MetalContent, temperature, false);
                }

                int amountToFill = Math.Min(amount, requiredUnits - FillLevel);
                FillLevel += amountToFill;
                amount -= amountToFill;
                UpdateRenderer();
                return;
            }

        }

        public void OnPourOver()
        {
            MarkDirty(true);
        }

        public override void OnBlockRemoved()
        {
            base.OnBlockRemoved();

            renderer?.Dispose();
            renderer = null;
        }

        public override void OnBlockUnloaded()
        {
            base.OnBlockUnloaded();
            renderer?.Dispose();
            renderer = null;
        }

        public ItemStack[] GetStateAwareMold()
        {
            List<ItemStack> mold = new List<ItemStack>();

            if (!Shattered)
            {
                if (!(BreaksWhenFilled && FillLevel > 0))
                {
                    mold.Add(new ItemStack(Block));
                }
            }
            else if (Block.Attributes?["shatteredDrops"].AsObject<BlockDropItemStack[]>(null) is BlockDropItemStack[] shatteredDrops)
            {
                for (int i = 0; i < shatteredDrops.Length; i++)
                {
                    shatteredDrops[i].Resolve(Api.World, "shatteredDrops[" + i + "] for", Block.Code);
                    ItemStack stack = shatteredDrops[i].GetNextItemStack();
                    if (stack == null) continue;

                    mold.Add(stack);
                    if (shatteredDrops[i].LastDrop) break;
                }
            }

            return mold.ToArray();
        }

        /// <summary>
        /// Retrieves the molded stacks, will always return null for incomplete pours. Will return the shattered version if the mold is shattered
        /// </summary>
        /// <returns></returns>
        public ItemStack[] GetStateAwareMoldedStacks()
        {
            if (MetalContent?.Collectible != null && IsHardened)
            {
                if (Shattered)
                {
                    var shatteredStack = MetalContent.Collectible.Attributes?["shatteredStack"].AsObject<JsonItemStack>();
                    if (shatteredStack != null)
                    {
                        shatteredStack.Resolve(Api.World, "shatteredStack for" + MetalContent.Collectible.Code);
                        if (shatteredStack.ResolvedItemstack is ItemStack stack)
                        {
                            stack.StackSize = (int)(FillLevel / 5f);
                            return new ItemStack[] { stack };
                        }
                    }
                }

                if (IsFull) return GetMoldedStacks(MetalContent);
            }

            return null;
        }

        /// <summary>
        /// Retrieves the chiseled stack, for use when the player is removing hardened pours by chisel
        /// </summary>
        /// <returns></returns>
        public ItemStack GetChiseledStack()
        {
            if (MetalContent != null && FillLevel > 0 && !Shattered && IsHardened)
            {
                var chiseledStack = MetalContent.Collectible.Attributes?["shatteredStack"].AsObject<JsonItemStack>();
                chiseledStack?.Resolve(Api.World, "chiseledStack for" + MetalContent.Collectible.Code);

                if (chiseledStack?.ResolvedItemstack is ItemStack stack)
                {
                    stack.StackSize = (int)(FillLevel / 5f);
                    return stack;
                }
            }

            return null;
        }

        public ItemStack[] GetMoldedStacks(ItemStack fromMetal)
        {
            try
            {
                if (Block.Attributes["drop"].Exists)
                {
                    JsonItemStack jstack = Block.Attributes["drop"].AsObject<JsonItemStack>(null, Block.Code.Domain);
                    if (jstack == null) return null;

                    ItemStack stack = stackFromCode(jstack, fromMetal);
                    if (stack == null) return Array.Empty<ItemStack>();

                    if (MetalContent != null) stack.Collectible.SetTemperature(Api.World, stack, MetalContent.Collectible.GetTemperature(Api.World, MetalContent));

                    return new ItemStack[] { stack };
                }
                else
                {
                    JsonItemStack[] jstacks = Block.Attributes["drops"].AsObject<JsonItemStack[]>(null, Block.Code.Domain);
                    List<ItemStack> stacks = new List<ItemStack>();

                    foreach (var jstack in jstacks)
                    {
                        ItemStack stack = stackFromCode(jstack, fromMetal);

                        if (MetalContent != null) stack.Collectible.SetTemperature(Api.World, stack, MetalContent.Collectible.GetTemperature(Api.World, MetalContent));

                        if (stack != null)
                        {
                            stacks.Add(stack);
                        }
                    }

                    return stacks.ToArray();
                }
            } catch (JsonReaderException)
            {
                Api.World.Logger.Error("Failed getting molded stacks from tool mold of block {0}, probably unable to parse drop or drops attribute", Block.Code);
                throw;
            }
        }


        public ItemStack stackFromCode(JsonItemStack jstack, ItemStack fromMetal)
        {
            string metaltype = fromMetal.Collectible.LastCodePart();
            jstack.Code.Path = jstack.Code.Path.Replace("{metal}", metaltype);
            jstack.Resolve(Api.World, "tool mold drop for " + Block.Code);
            return jstack.ResolvedItemstack;
        }


        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolve)
        {
            base.FromTreeAttributes(tree, worldForResolve);

            MetalContent = tree.GetItemstack("contents");
            FillLevel = tree.GetInt("fillLevel");
            Shattered = tree.GetBool("shattered");
            if (worldForResolve != null && MetalContent != null) MetalContent.ResolveBlockOrItem(worldForResolve);

            hasMeshAngle = tree.HasAttribute("meshAngle");
            MeshAngle = tree.GetFloat("meshAngle");

            UpdateRenderer();

            if (Api?.Side == EnumAppSide.Client)
            {
                Api.World.BlockAccessor.MarkBlockDirty(Pos);
            }
        }


        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);

            tree.SetItemstack("contents", MetalContent);
            tree.SetInt("fillLevel", FillLevel);
            tree.SetBool("shattered", Shattered);
            tree.SetFloat("meshAngle", MeshAngle);
        }


        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            if (!Shattered)
            {
                string state = IsLiquid ? Lang.Get("liquid") : (IsHardened ? Lang.Get("hardened") : Lang.Get("soft"));

                string matkey = "material-" + MetalContent?.Collectible.Variant["metal"];
                string mat = Lang.HasTranslation(matkey) ? Lang.Get(matkey) : MetalContent?.GetName();
                string temp = Temperature < 21 ? Lang.Get("Cold") : Lang.Get("{0}°C", (int)Temperature);
                string contents = Lang.GetWithFallback("metalmold-blockinfo-unitsofmetal", "{0}/{4} units of {1} {2} ({3})", FillLevel, state, mat, temp, requiredUnits);
                dsc.AppendLine((MetalContent != null ? contents : Lang.GetWithFallback("metalmold-blockinfo-emptymold", "0/{0} units of metal", requiredUnits)) + "\n");
            }
            else if (GetStateAwareMoldedStacks()?[0] is ItemStack shatteredStack)
            {
                dsc.AppendLine(Lang.Get("metalmold-blockinfo-shatteredmetal", shatteredStack.StackSize, shatteredStack.GetName().ToLower()) + "\n");
            }
        }


        public override void OnStoreCollectibleMappings(Dictionary<int, AssetLocation> blockIdMapping, Dictionary<int, AssetLocation> itemIdMapping)
        {
            MetalContent?.Collectible.OnStoreCollectibleMappings(Api.World, new DummySlot(MetalContent), blockIdMapping, itemIdMapping);
        }

        public override void OnLoadCollectibleMappings(IWorldAccessor worldForResolve, Dictionary<int, AssetLocation> oldBlockIdMapping, Dictionary<int, AssetLocation> oldItemIdMapping, int schematicSeed, bool resolveImports)
        {
            if (MetalContent?.FixMapping(oldBlockIdMapping, oldItemIdMapping, worldForResolve) == null)
            {
                MetalContent = null;
            }
            // update the time for the temperature to the current ingame time if imported from another game
            if ((MetalContent?.Attributes["temperature"] as ITreeAttribute)?.HasAttribute("temperatureLastUpdate") == true)
            {
                ((ITreeAttribute)MetalContent.Attributes["temperature"]).SetDouble("temperatureLastUpdate", worldForResolve.Calendar.TotalHours);
            }
        }

        public void ShatterMold()
        {
            Api.World.PlaySoundAt(new AssetLocation("sounds/block/ceramicbreak"), Pos, -0.4);
            Shattered = true;
            this.Block.SpawnBlockBrokenParticles(Pos);
            this.Block.SpawnBlockBrokenParticles(Pos);
            MarkDirty(true);
        }

        public void CoolNow(float amountRel)
        {
            float breakchance = Math.Max(0, amountRel - 0.6f) * Math.Max(Temperature - 250f, 0) / 5000f;

            if (Api.World.Rand.NextDouble() < breakchance)
            {
                ShatterMold();
                MetalContent.Collectible.SetTemperature(Api.World, MetalContent, 20, false);
                FillLevel = (int)(FillLevel * (0.7f + Api.World.Rand.NextDouble() * 0.1f));
            } else
            {
                if (MetalContent != null)
                {
                    float temp = Temperature;
                    if (temp > 120)
                    {
                        Api.World.PlaySoundAt(new AssetLocation("sounds/effect/extinguish"), Pos, -0.5, null, false, 16);
                    }

                    MetalContent.Collectible.SetTemperature(Api.World, MetalContent, Math.Max(20, temp - amountRel * 20), false);
                    MarkDirty(true);
                }
            }
        }


        #region Mesh gen
        ITexPositionSource tmpTextureSource;
        AssetLocation metalTexLoc;
        public Size2i AtlasSize => capi.BlockTextureAtlas.Size;
        public TextureAtlasPosition this[string textureCode]
        {
            get
            {
                if (textureCode == "metal")
                {
                    return capi.BlockTextureAtlas[metalTexLoc];
                }

                return tmpTextureSource[textureCode];
            }
        }

        MeshData shatteredMesh;
        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tessThreadTesselator)
        {
            if (Shattered) EnsureShatteredMeshesLoaded();
            var tfMat = Mat4f.Create();
            Mat4f.Translate(tfMat, tfMat, 0.5f, 0f, 0.5f);
            Mat4f.RotateY(tfMat, tfMat, MeshAngle);
            Mat4f.Translate(tfMat, tfMat, -0.5f, -0f, -0.5f);
            mesher.AddMeshData(Shattered ? shatteredMesh : MoldMesh, tfMat);

            return true;
        }

        private void EnsureShatteredMeshesLoaded()
        {
            if (Shattered && shatteredMesh == null)
            {
                metalTexLoc = MetalContent == null ? new AssetLocation("block/transparent") : new AssetLocation("block/metal/ingot/" + MetalContent.Collectible.LastCodePart());
                capi.Tesselator.TesselateShape("shatteredmold", getShatteredShape(Block), out shatteredMesh, this);
            }
        }

        private Shape getShatteredShape(Block block)
        {
            tmpTextureSource = capi.Tesselator.GetTextureSource(block);
            var cshape = block.Attributes["shatteredShape"].AsObject<CompositeShape>();
            cshape.Base.WithPathAppendixOnce(".json").WithPathPrefixOnce("shapes/");
            return Shape.TryGet(Api, cshape.Base);
        }

        private void GenMeshes()
        {
            MoldMesh = ObjectCacheUtil.GetOrCreate(Api, Block.Code.ToString(), () =>
            {
                CompositeShape cShape = Block.Shape;
                ITexPositionSource tmpTextureSource = ((ICoreClientAPI)Api).Tesselator.GetTextureSource(Block);
                ITesselatorAPI mesher = ((ICoreClientAPI)Api).Tesselator;

                Shape shape = Shape.TryGet(Api, Block.Shape.Base.Clone().WithPathPrefixOnce("shapes/").WithPathAppendixOnce(".json"));
                mesher.TesselateShape(Block.Code.ToString(), shape, out MeshData mesh, tmpTextureSource, new Vec3f(cShape.rotateX, cShape.rotateY, cShape.rotateZ));

                return mesh;
            });
        }

        #endregion

        public void OnTransformed(IWorldAccessor worldAccessor, ITreeAttribute tree, int degreeRotation, Dictionary<int, AssetLocation> oldBlockIdMapping, Dictionary<int, AssetLocation> oldItemIdMapping, EnumAxis? flipAxis)
        {
            MeshAngle = tree.GetFloat("meshAngle");
            MeshAngle -= degreeRotation * GameMath.DEG2RAD;
            tree.SetFloat("meshAngle", MeshAngle);
        }
    }
}
