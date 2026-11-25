using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

#nullable disable

namespace Vintagestory.GameContent.Mechanics
{
    /// <summary>
    /// Add dummy block for Pulverizer upper part, etc
    /// </summary>
    public class BEPulverizer : BlockEntityDisplay, IRotatable
    {
        // The fitted parts are kept in hidden slots after the two input slots.
        const int leftCapSlot = 2;
        const int rightCapSlot = 3;
        const int toggleSlot = 4;
        const int leftPounderSlot = 5;
        const int rightPounderSlot = 6;
        const int slotCount = rightPounderSlot + 1;
        static readonly AssetLocation legacyToggleCode = new("pulverizertoggle-oak");
        static readonly AssetLocation legacyPounderCode = new("pounder-oak");

        public BlockFacing Facing { get; protected set; } = BlockFacing.NORTH;

        public Vec4f lightRbs = new Vec4f();
        public virtual Vec4f LightRgba { get { return lightRbs; } }

        public override InventoryBase Inventory => inv;
        public override string InventoryClassName => "pulverizer";

        float rotateY = 0f;
        InventoryPulverizer inv;

        internal Matrixf mat = new Matrixf();

        BEBehaviorMPPulverizer pvBh;
        bool legacyPartsMigrated;

        public ItemStack ToggleStack => inv[toggleSlot].Itemstack;
        public ItemStack LeftPounderStack => inv[leftPounderSlot].Itemstack;
        public ItemStack RightPounderStack => inv[rightPounderSlot].Itemstack;
        public ItemStack LeftCapStack => inv[leftCapSlot].Itemstack;
        public ItemStack RightCapStack => inv[rightCapSlot].Itemstack;

        public int CapMetalTierL;
        public int CapMetalTierR;

        public int CapMetalIndexL;
        public int CapMetalIndexR;

        public bool IsComplete => ToggleStack != null && LeftPounderStack != null && RightPounderStack != null && LeftCapStack != null && RightCapStack != null;

        public bool CanCrush(bool right) => ToggleStack != null && (right ? RightPounderStack != null && RightCapStack != null : LeftPounderStack != null && LeftCapStack != null);

        public override int DisplayedItems => 2;

        public BEPulverizer()
        {
            inv = new InventoryPulverizer(this, slotCount);
            inv.SlotModified += Inv_SlotModified;
        }

        private void Inv_SlotModified(int t1)
        {
            UpdateCaps();   // called on both server-side and client-side

            if (Api is ICoreClientAPI)
            {
                MarkMeshesDirty();
                Api.World.BlockAccessor.MarkBlockDirty(Pos);   // always redraw on client after updating meshes
            }
        }


        public override void Initialize(ICoreAPI api)
        {
            Facing = BlockFacing.FromCode(Block.Variant["side"]);
            if (Facing == null) Facing = BlockFacing.NORTH;

            switch (Facing.Index)
            {
                case 0:
                    rotateY = 180;
                    break;
                case 1:
                    rotateY = 90;
                    break;
                case 3:
                    rotateY = 270;
                    break;
                default:
                    break;
            }

            mat.Translate(0.5f, 0.5f, 0.5f);
            mat.RotateYDeg(rotateY);
            mat.Translate(-0.5f, -0.5f, -0.5f);

            base.Initialize(api);

            inv.LateInitialize(InventoryClassName + "-" + Pos, api);

            if (api.World.Side == EnumAppSide.Server)
            {
                RegisterGameTickListener(OnServerTick, 200);
            }

            pvBh = GetBehavior<BEBehaviorMPPulverizer>();
            UpdateCaps();
        }

        float accumLeft;
        float accumRight;

        private void OnServerTick(float dt)
        {
            if (ToggleStack == null) return;

            float nwspeed = Math.Abs(pvBh.CurrentSpeed) * 3f;

            // Slot 0 is crushed by the right pounder and slot 1 by the left one.
            if (CanCrush(true) && !inv[0].Empty)
            {
                accumLeft += dt * nwspeed;

                if (accumLeft > 5)
                {
                    accumLeft = 0;
                    Crush(0, CapMetalTierR, -4 / 16d);
                }
            }

            if (CanCrush(false) && !inv[1].Empty)
            {
                accumRight += dt * nwspeed;

                if (accumRight > 5)
                {
                    accumRight = 0;
                    Crush(1, CapMetalTierL, 4 / 16d);
                }
            }
        }

        private void Crush(int slot, int capTier, double xOffset)
        {
            var rand = Api.World.Rand;
            ItemStack inputStack = inv[slot].TakeOut(1);
            var props = inputStack.Collectible.GetCrushingProperties(Api.World, inputStack);
            ItemStack outputStack = null;

            bool canCrush = false;
            if (props != null)
            {
                canCrush = props.HardnessTier <= capTier;
                outputStack = props.CrushedStack?.ResolvedItemstack?.Clone();
                outputStack?.StackSize = GameMath.RoundRandom(rand, props.Quantity.nextFloat(outputStack.StackSize, rand));
            }

            // Make sure to always return the input if crushing isn't possible
            if (!canCrush || outputStack?.StackSize > 0)
            {
                Vec3d position = mat.TransformVector(new Vec4d(xOffset * 0.999, 0.1, 0.8, 0)).XYZ.Add(Pos).Add(0.5, 0, 0.5);
                double lengthways = rand.NextDouble() * 0.07 - 0.035;
                double sideways = rand.NextDouble() * 0.03 - 0.005;
                Vec3d velocity = new Vec3d(Facing.Axis == EnumAxis.Z ? sideways : lengthways, rand.NextDouble() * 0.02 - 0.01, Facing.Axis == EnumAxis.Z ? lengthways : sideways);

                Api.World.SpawnItemEntity(canCrush ? outputStack : inputStack, position, velocity);
            }


            MarkDirty(true);
        }


        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tesselator)
        {
            // Adds the display items
            base.OnTesselation(mesher, tesselator);

            ICoreClientAPI capi = Api as ICoreClientAPI;
            BlockPulverizer pulverizerBlock = (BlockPulverizer)Block;
            ItemStack frameStack = pvBh.GetPlacedStack();
            string materialKey = pulverizerBlock.GetMaterialKey(frameStack);

            MeshData meshTop = ObjectCacheUtil.GetOrCreate(capi, $"pulverizertopmesh-{rotateY}-{materialKey}", () =>
            {
                Shape shapeTop = API.Common.Shape.TryGet(capi, "shapes/block/wood/mechanics/pulverizer-top.json");
                capi.Tesselator.TesselateShape(Block.Code + " top", shapeTop, out MeshData mesh, pulverizerBlock.GetMaterialTextureSource(capi, shapeTop, Block.Code + " top", frameStack), new Vec3f(0, rotateY, 0));

                return mesh;
            });

            MeshData meshBase = ObjectCacheUtil.GetOrCreate(capi, $"pulverizerbasemesh-{rotateY}-{materialKey}", () =>
            {
                Shape shapeBase = API.Common.Shape.TryGet(capi, "shapes/block/wood/mechanics/pulverizer-base.json");
                capi.Tesselator.TesselateShape(Block.Code + " base", shapeBase, out MeshData mesh, pulverizerBlock.GetMaterialTextureSource(capi, shapeBase, Block.Code + " base", frameStack), new Vec3f(0, rotateY, 0));

                return mesh;
            });

            mesher.AddMeshData(meshTop);
            mesher.AddMeshData(meshBase);

            for (int i = 0; i < Behaviors.Count; i++)
            {
                Behaviors[i].OnTesselation(mesher, tesselator);
            }

            return true;
        }

        [Obsolete("Fitted parts now drop from Inventory. Use Block.GetDrops() for the frame.")]
        public ItemStack[] getDrops(IWorldAccessor world, ItemStack pulvFrame) => [pulvFrame];

        public bool OnInteract(IPlayer byPlayer, BlockSelection blockSel)
        {
            ItemSlot handslot = byPlayer.InventoryManager.ActiveHotbarSlot;

            Vec4d vec = new Vec4d(blockSel.HitPosition.X, blockSel.HitPosition.Y, blockSel.HitPosition.Z, 1);
            Vec4d tvec = mat.TransformVector(vec);
            int a = Facing.Axis == EnumAxis.Z ? 1 : 0;
            ItemSlot targetSlot = tvec.X < 0.5 ? inv[a] : inv[1-a];

            if (handslot.Empty)
            {
                TryTake(targetSlot, byPlayer);
            } else
            {
                if (TryAddPart(handslot, targetSlot, byPlayer))
                {
                    Api.World.PlaySoundAt(Block.Sounds.Place, Pos, -0.25, byPlayer);
                    (Api as ICoreClientAPI)?.World.Player.TriggerFpAnimation(EnumHandInteract.HeldItemInteract);
                    return true;
                }

                var crushingProps = handslot.Itemstack.Collectible.GetCrushingProperties(Api.World, handslot.Itemstack);
                if (crushingProps != null)
                {
                    TryPut(handslot, targetSlot);
                }
            }

            return true;
        }


        private bool TryAddPart(ItemSlot slot, ItemSlot targetSlot, IPlayer toPlayer)
        {
            BlockPulverizer pulverizerBlock = (BlockPulverizer)Block;
            TagSet tags = slot.Itemstack.Collectible.GetTags(slot.Itemstack);

            if (ToggleStack == null && tags.Overlaps(pulverizerBlock.ToggleTag))
            {
                insertPart(slot, toggleSlot, toPlayer);
                pvBh.OnToggleInserted();
                return true;
            }

            if ((LeftPounderStack == null || RightPounderStack == null) && tags.Overlaps(pulverizerBlock.PounderTag))
            {
                insertPart(slot, LeftPounderStack != null ? rightPounderSlot : leftPounderSlot, toPlayer);
                return true;
            }

            if (tags.Overlaps(pulverizerBlock.PounderCapTag))
            {
                bool left = targetSlot == inv[1];
                if (left ? LeftPounderStack != null : RightPounderStack != null)
                {
                    // Each pounder takes its own cap, so the clicked side is fitted or has its cap swapped.
                    int capSlot = left ? leftCapSlot : rightCapSlot;
                    ItemStack fittedCap = inv[capSlot].Itemstack;
                    if (fittedCap != null && !toPlayer.InventoryManager.TryGiveItemstack(fittedCap, true))
                    {
                        Api.World.SpawnItemEntity(fittedCap, Pos);
                    }

                    insertPart(slot, capSlot, toPlayer);
                }
                else
                {
                    (Api as ICoreClientAPI)?.TriggerIngameError(this, "requirepounder", Lang.Get("Please add a pounder on this side first!"));
                }
                return true;
            }

            return false;
        }

        void insertPart(ItemSlot fromSlot, int partSlotId, IPlayer byPlayer)
        {
            ItemStack partStack = fromSlot.Itemstack.Clone();
            partStack.StackSize = 1;
            if (byPlayer.WorldData.CurrentGameMode != EnumGameMode.Creative)
            {
                fromSlot.TakeOut(1);
                fromSlot.MarkDirty();
            }

            inv[partSlotId].Itemstack = partStack;
            inv[partSlotId].MarkDirty();
            MarkDirty(true);
        }

        private void TryPut(ItemSlot fromSlot, ItemSlot intoSlot)
        {
            if (fromSlot.TryPutInto(Api.World, intoSlot, 1) > 0)
            {
                fromSlot.MarkDirty();
                MarkDirty(true);
            }
        }

        private void TryTake(ItemSlot fromSlot, IPlayer toPlayer)
        {
            ItemStack stack = fromSlot.TakeOut(1);
            if (!toPlayer.InventoryManager.TryGiveItemstack(stack))
            {
                Api.World.SpawnItemEntity(stack, Pos.ToVec3d().Add(0.5, 0.1, 0.5));
            }

            MarkDirty(true);
        }


        public void UpdateCaps()
        {
            string metalL = getCapMetal(inv[leftCapSlot]);
            string metalR = getCapMetal(inv[rightCapSlot]);

            CapMetalTierL = getMetalTier(metalL);
            CapMetalTierR = getMetalTier(metalR);

            CapMetalIndexL = Math.Max(0, PulverizerRenderer.metals.IndexOf(metalL));
            CapMetalIndexR = Math.Max(0, PulverizerRenderer.metals.IndexOf(metalR));
        }

        static string getCapMetal(ItemSlot capSlot)
        {
            return capSlot.Itemstack?.Collectible?.Variant["metal"] ?? "nometal";
        }

        int getMetalTier(string metal)
        {
            MetalPropertyVariant metalvar = null;
            if (metal != null) Api.ModLoader.GetModSystem<SurvivalCoreSystem>().metalsByCode.TryGetValue(metal, out metalvar);
            return Math.Max(metalvar?.Tier ?? 0, 0);
        }


        protected override float[][] genTransformationMatrices()
        {
            float[][] tfMatrices = new float[2][];

            for (int index = 0; index < 2; index++)
            {
                float x = (index % 2 == 0) ? 11.5f / 16f : 4.5f / 16f;

                Matrixf mat = new Matrixf().Set(this.mat.Values);

                mat.Translate(x - 0.5f, 4 / 16f, -4.5f / 16f);
                mat.Translate(0.5f, 0, 0.5f);
                mat.Scale(0.6f, 0.6f, 0.6f);
                mat.Translate(-0.5f, 0, -0.5f);

                tfMatrices[index] = mat.Values;
            }



            return tfMatrices;
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);

            legacyPartsMigrated = tree.HasAttribute("hasAxle");
            if (legacyPartsMigrated) migrateLegacyParts(tree, worldAccessForResolve);

            // A synced inventory raises no slot modified event, so the caps are refreshed here.
            if (Api != null) UpdateCaps();

            // Do this last!!!
            RedrawAfterReceivingTreeAttributes(worldAccessForResolve);     // Redraw on client after we have completed receiving the update from server
        }

        // Older saves stored the toggle and pounders as flags and both caps as one stack.
        void migrateLegacyParts(ITreeAttribute tree, IWorldAccessor world)
        {
            migrateLegacyPart(tree.GetBool("hasAxle"), toggleSlot, legacyToggleCode, world);
            migrateLegacyPart(tree.GetBool("hasLPounder"), leftPounderSlot, legacyPounderCode, world);
            migrateLegacyPart(tree.GetBool("hasRPounder"), rightPounderSlot, legacyPounderCode, world);

            ItemStack caps = inv[leftCapSlot].Itemstack;
            if (caps?.StackSize > 1 && inv[rightCapSlot].Empty)
            {
                ItemStack cap = caps.Clone();
                cap.StackSize = 1;
                inv[leftCapSlot].Itemstack = cap;
                inv[rightCapSlot].Itemstack = cap.Clone();
            }
        }

        void migrateLegacyPart(bool fitted, int partSlotId, AssetLocation itemCode, IWorldAccessor world)
        {
            if (!fitted || !inv[partSlotId].Empty) return;

            Item item = world.GetItem(itemCode);
            if (item != null) inv[partSlotId].Itemstack = new ItemStack(item);
        }

        public override void OnLoadCollectibleMappings(IWorldAccessor worldForResolve, Dictionary<int, AssetLocation> oldBlockIdMapping, Dictionary<int, AssetLocation> oldItemIdMapping, int schematicSeed, bool resolveImports)
        {
            ItemStack[] legacyParts = null;
            if (legacyPartsMigrated)
            {
                // Reconstructed parts already use this world's IDs, not the schematic's IDs.
                legacyParts = new ItemStack[slotCount - toggleSlot];
                for (int i = toggleSlot; i < slotCount; i++)
                {
                    legacyParts[i - toggleSlot] = inv[i].Itemstack;
                    inv[i].Itemstack = null;
                }
            }

            base.OnLoadCollectibleMappings(worldForResolve, oldBlockIdMapping, oldItemIdMapping, schematicSeed, resolveImports);

            if (legacyParts != null)
            {
                for (int i = toggleSlot; i < slotCount; i++) inv[i].Itemstack = legacyParts[i - toggleSlot];
            }
            legacyPartsMigrated = false;

            if (Api != null) UpdateCaps();
        }

        public void OnTransformed(IWorldAccessor worldAccessor, ITreeAttribute tree, int degreeRotation, Dictionary<int, AssetLocation> oldBlockIdMapping, Dictionary<int, AssetLocation> oldItemIdMapping, EnumAxis? flipAxis)
        {
            if (flipAxis != EnumAxis.X && flipAxis != EnumAxis.Z) return;

            bool legacyParts = tree.HasAttribute("hasAxle");
            if (legacyParts) swapAttributes(tree, "hasLPounder", "hasRPounder");

            ITreeAttribute slots = tree.GetTreeAttribute("inventory")?.GetTreeAttribute("slots");
            if (slots == null) return;

            swapAttributes(slots, "0", "1");
            if (!legacyParts)
            {
                swapAttributes(slots, leftCapSlot.ToString(), rightCapSlot.ToString());
                swapAttributes(slots, leftPounderSlot.ToString(), rightPounderSlot.ToString());
            }
        }

        static void swapAttributes(ITreeAttribute tree, string firstKey, string secondKey)
        {
            IAttribute first = tree[firstKey];
            IAttribute second = tree[secondKey];
            tree.RemoveAttribute(firstKey);
            tree.RemoveAttribute(secondKey);
            if (first != null) tree[secondKey] = first;
            if (second != null) tree[firstKey] = second;
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder sb)
        {
            sb.AppendLine(Lang.Get("Pulverizing:"));
            bool empty = true;
            for (int i = 0; i < 2; i++)
            {
                if (inv[i].Empty) continue;
                empty = false;
                sb.AppendLine("  " + inv[i].StackSize + " x " + inv[i].GetStackName());
            }

            if (empty) sb.AppendLine("  " + Lang.Get("Nothing"));

            pvBh.GetBlockInfo(forPlayer, sb);
        }
    }

    public class InventoryPulverizer : InventoryDisplayed
    {
        const int inputSlotCount = 2;

        public InventoryPulverizer(BlockEntity be, int size) : base(be, size, "pulverizer-0", null)
        {
            slots = GenEmptySlots(size);
            for (int i = 0; i < size; i++) slots[i].MaxSlotStackSize = 1;
        }

        public override float GetSuitability(ItemSlot sourceSlot, ItemSlot targetSlot, bool isMerge)
        {
            if (GetSlotId(targetSlot) >= inputSlotCount) return 0;  // Hoppers and chutes may only fill the input slots
            return base.GetSuitability(sourceSlot, targetSlot, isMerge);
        }

        public override ItemSlot GetAutoPushIntoSlot(BlockFacing atBlockFace, ItemSlot fromSlot)
        {
            ItemSlot slot = base.GetAutoPushIntoSlot(atBlockFace, fromSlot);
            int slotId = GetSlotId(slot);
            return slotId >= 0 && slotId < inputSlotCount ? slot : null;
        }
    }

}
