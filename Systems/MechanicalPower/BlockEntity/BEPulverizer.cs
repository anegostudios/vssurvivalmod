using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent.Mechanics
{
    /// <summary>
    /// Add dummy block for Pulverizer upper part, etc
    /// </summary>
    public class BEPulverizer : BlockEntityDisplay
    {
        public BlockFacing Facing { get; protected set; } = BlockFacing.NORTH;
        readonly AssetLocation pounderName = new AssetLocation("pounder-oak");
        readonly AssetLocation toggleName = new AssetLocation("pulverizertoggle-oak");

        public Vec4f lightRbs = new Vec4f();
        public virtual Vec4f LightRgba { get { return lightRbs; } }

        public override InventoryBase Inventory => inv;
        public override string InventoryClassName => "pulverizer";

        float rotateY = 0f;
        InventoryGeneric inv;

        internal Matrixf mat = new Matrixf();

        BEBehaviorMPPulverizer pvBh;

        public bool hasAxle;
        public bool hasLPounder;
        public bool hasRPounder;
        public bool hasPounderCaps => !inv[2].Empty;

        public int CapMetalIndexL;
        public int CapMetalIndexR;

        public bool IsComplete => hasAxle && hasLPounder && hasRPounder && hasPounderCaps;

        public BEPulverizer()
        {
            inv = new InventoryGeneric(3, null, null);
            inv.SlotModified += Inv_SlotModified;
            meshes = new MeshData[2];
        }

        private void Inv_SlotModified(int t1)
        {
            updateMeshes();
        }

        protected override void updateMeshes()
        {
            string metal = "nometal";
            if (!inv[2].Empty) metal = inv[2].Itemstack.Collectible.Variant["metal"];
            CapMetalIndexL = CapMetalIndexR = PulverizerRenderer.metals.IndexOf(metal);

            base.updateMeshes();
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
            inv[0].MaxSlotStackSize = 1;
            inv[1].MaxSlotStackSize = 1;
            inv[2].MaxSlotStackSize = 1;

            if (api.World.Side == EnumAppSide.Server)
            {
                RegisterGameTickListener(OnServerTick, 200);
            }

            pvBh = GetBehavior<BEBehaviorMPPulverizer>();
        }

        float accumLeft;
        float accumRight;

        private void OnServerTick(float dt)
        {
            if (!IsComplete) return;

            float nwspeed = pvBh.Network?.Speed ?? 0;
            nwspeed = Math.Abs(nwspeed * 3f) * pvBh.GearedRatio;

            if (!inv[0].Empty)
            {
                accumLeft += dt * nwspeed;

                //TODO: instead tie this in with actual strikes from the beBehavior
                if (accumLeft > 3)
                {
                    accumLeft = 0;
                    Crush(0, CapMetalIndexL, -3.5 / 16d);
                }
            }

            if (!inv[1].Empty)
            {
                accumRight += dt * nwspeed;

                if (accumRight > 3)
                {
                    accumRight = 0;
                    Crush(1, CapMetalIndexR, 3.5 / 16d);
                }
            }

        }

        private void Crush(int slot, int capTier, double xOffset)
        {
            ItemStack input = inv[slot].TakeOut(1);
            JsonItemStack jstack = input.ItemAttributes?["crushedOutput"].AsObject<JsonItemStack>(null, Block.Code.Domain);
            jstack.Resolve(Api.World, "crushed output");
            ItemStack output = jstack.ResolvedItemstack;
            bool tierPassed = output != null && TierPasses(output.Attributes?.GetString("crushingtier"), capTier);
            if (output != null) output.Attributes.RemoveAttribute("crushingtier");

            Vec3d position = mat.TransformVector(new Vec4d(xOffset, 0.1, 0.7, 0)).XYZ.Add(Pos).Add(0.5, 0, 0.5);
            Vec3d velocity = new Vec3d((float)Api.World.Rand.NextDouble() * 0.05f - 0.025f, (float)Api.World.Rand.NextDouble() * 0.05f - 0.025f, (float)Api.World.Rand.NextDouble() * 0.05f - 0.025f);
            Api.World.SpawnItemEntity(tierPassed ? output : input, position, velocity);
            //Api.World.SpawnParticles(1, ColorUtil.WhiteArgb, position, position, new Vec3f(), new Vec3f(), 2, 0, 0.3f);
            MarkDirty(true);
        }

        private bool TierPasses(string requiredMetal, int capTier)
        {
            int requiredTier = 0;
            String[] names = PulverizerRenderer.metals;
            for (int i = 0; i < names.Length; i++)
            {
                if (names[i] == requiredMetal)
                {
                    requiredTier = i;
                    break;
                }
            }
            return requiredTier <= capTier;
        }

        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tesselator)
        {
            // Adds the display items
            base.OnTesselation(mesher, tesselator);

            lightRbs = Api.World.BlockAccessor.GetLightRGBs(Pos);
            ICoreClientAPI capi = Api as ICoreClientAPI;

            
            MeshData meshTop = ObjectCacheUtil.GetOrCreate(capi, "pulverizertopmesh-"+rotateY, () =>
            {
                MeshData mesh;
                Shape shapeTop = capi.Assets.TryGet("shapes/block/wood/mechanics/pulverizer-top.json").ToObject<Shape>();
                capi.Tesselator.TesselateShape(Block, shapeTop, out mesh, new Vec3f(0, rotateY, 0));

                return mesh;
            });

            MeshData meshBase = ObjectCacheUtil.GetOrCreate(capi, "pulverizerbasemesh-" + rotateY, () =>
            {
                MeshData mesh;
                Shape shapeBase = capi.Assets.TryGet("shapes/block/wood/mechanics/pulverizer-base.json").ToObject<Shape>();
                capi.Tesselator.TesselateShape(Block, shapeBase, out mesh, new Vec3f(0, rotateY, 0));

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

        /// <summary>
        /// Add the specific parts of this Pulverizer, to the block drops  (the caps will drop anyhow as they are in inventory)
        /// </summary>
        public ItemStack[] getDrops(IWorldAccessor world, ItemStack pulvFrame)
        {
            int pounders = 0;
            if (hasLPounder) pounders++;
            if (hasRPounder) pounders++;
            ItemStack[] result = new ItemStack[pounders + (hasAxle ? 2 : 1)];
            int index = 0;
            result[index++] = pulvFrame;
            for (int i = 0; i < pounders; i++)
                result[index++] = new ItemStack(world.GetItem(pounderName));
            if (hasAxle) result[index] = new ItemStack(world.GetItem(toggleName));
            return result;
        }

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
                if (TryAddPart(handslot, byPlayer)) return true;

                if (handslot.Itemstack.ItemAttributes?["crushedOutput"].Exists == true)
                {
                    TryPut(handslot, targetSlot);
                }
            }

            return true;
        }


        private bool TryAddPart(ItemSlot slot, IPlayer toPlayer)
        {
            if (!hasAxle && slot.Itemstack.Collectible.Code.Path == "pulverizertoggle-oak")
            {
                slot.TakeOut(1);
                slot.MarkDirty();
                hasAxle = true;
                MarkDirty(true);
                return true;
            }

            if ((!hasLPounder || !hasRPounder) && slot.Itemstack.Collectible.Code.Path == "pounder-oak")
            {
                slot.TakeOut(1);
                slot.MarkDirty();
                if (hasLPounder) hasRPounder = true;
                hasLPounder = true;
                MarkDirty(true);
                return true;
            }

            if (slot.Itemstack.Collectible.FirstCodePart() == "poundercap")
            {
                if (hasLPounder && hasRPounder)
                {
                    if (slot.Itemstack.StackSize < 2)
                    {
                        (Api as ICoreClientAPI)?.TriggerIngameError(this, "require2caps", Lang.Get("Please add 2 caps at the same time!"));
                        return true;
                    }

                    ItemStack stack = slot.TakeOut(2);
                    if (!inv[2].Empty)
                    {
                        if (!toPlayer.InventoryManager.TryGiveItemstack(inv[2].Itemstack, true))
                        {
                            Api.World.SpawnItemEntity(inv[2].Itemstack, Pos.ToVec3d().Add(0.5, 0.5, 0.5));
                        }
                    }

                    inv[2].Itemstack = stack;

                    slot.MarkDirty();
                    MarkDirty(true);
                }
                else
                {
                    (Api as ICoreClientAPI)?.TriggerIngameError(this, "require2pounders", Lang.Get("Please add pounders before adding caps!"));
                }
                return true;
            }

            return false;
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

        protected override MeshData genMesh(ItemStack stack, int index)
        {
            MeshData mesh = base.genMesh(stack, index);

            float x = (index % 2 == 0) ? 11.5f / 16f : 4.5f / 16f;

            Vec4f offset = mat.TransformVector(new Vec4f(x - 0.5f, 4/16f, -4.5f/16f, 0f));
            mesh.Scale(new Vec3f(0.5f, 0f, 0.5f), 0.5f, 0.5f, 0.5f);
            mesh.Translate(offset.XYZ);

            return mesh;
        }

        public override void OnBlockRemoved()
        {
            base.OnBlockRemoved();
        }

        public override void OnBlockUnloaded()
        {
            base.OnBlockUnloaded();
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);

            hasLPounder = tree.GetBool("hasLPounder");
            hasRPounder = tree.GetBool("hasRPounder");
            hasAxle = tree.GetBool("hasAxle");
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);

            tree.SetBool("hasLPounder", hasLPounder);
            tree.SetBool("hasRPounder", hasRPounder);
            tree.SetBool("hasAxle", hasAxle);
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
            if (empty) sb.AppendLine("  " + Lang.Get("nothing"));
        }

        public void onNeighbourChange(BlockPos neibpos)
        {
        }
    }
}
