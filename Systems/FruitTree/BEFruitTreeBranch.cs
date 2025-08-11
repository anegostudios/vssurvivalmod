using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

#nullable disable

namespace Vintagestory.GameContent
{
    public class BlockEntityFruitTreeBranch : BlockEntityFruitTreePart
    {
        public int SideGrowth = 0;
        public Vec3i ParentOff;
        
        public int GrowTries;
        public double lastGrowthAttemptTotalDays;

        MeshData branchMesh;
        Cuboidf[] colSelBoxes;

        /// <summary>
        /// A value of 0..1. Zero being just a single leaves block, 1 being fully grown tree
        /// </summary>
        public float? FastForwardGrowth;


        bool initialized;

        public override void Initialize(ICoreAPI api)
        {
            this.Api = api;
            if (Block?.Attributes?["foliageBlock"].Exists != true) return;

            blockFoliage = api.World.GetBlock(AssetLocation.Create(Block.Attributes["foliageBlock"].AsString(), Block.Code.Domain)) as BlockFruitTreeFoliage;
            blockBranch = Block as BlockFruitTreeBranch;

            initCustomBehaviors(null, false);

            base.Initialize(api);

            if (FastForwardGrowth != null && Api.Side == EnumAppSide.Server)
            {
                lastGrowthAttemptTotalDays = Api.World.Calendar.TotalDays - 20 - ((float)FastForwardGrowth * 600);
                InitTreeRoot(TreeType, true);
                FastForwardGrowth = null;
            }

            updateProperties();
        }

        public Cuboidf[] GetColSelBox()
        {
            if (GrowthDir.Axis == EnumAxis.Y) return Block.CollisionBoxes;
            return colSelBoxes;
        }

        public override void OnBlockPlaced(ItemStack byItemStack = null)
        {
            base.OnBlockPlaced(byItemStack);
            InitTreeRoot(byItemStack?.Attributes?.GetString("type", null), byItemStack != null, byItemStack);
        }

        internal void InteractDebug()
        {
            if (RootOff.Y != 0) return;

            var rootBe = (Api.World.BlockAccessor.GetBlockEntity(Pos.AddCopy(RootOff)) as BlockEntityFruitTreeBranch)?.GetBehavior<FruitTreeRootBH>();
            if (rootBe != null)
            {
                foreach (var val in rootBe.propsByType)
                {
                    val.Value.State = (EnumFruitTreeState)(((int)val.Value.State + 1) % ((int)EnumFruitTreeState.Dead));
                }
            }

            MarkDirty(true);
        }

        public void InitTreeRoot(string treeType, bool callInitialize, ItemStack parentPlantStack = null)
        {
            if (initialized) return;
            initialized = true;
            GrowthDir = BlockFacing.UP;
            PartType = parentPlantStack?.Collectible.Variant["type"] == "cutting" ? EnumTreePartType.Cutting : EnumTreePartType.Branch;
            RootOff = new Vec3i();
            TreeType ??= treeType;

            if (PartType == EnumTreePartType.Cutting && parentPlantStack != null)
            {
                var belowBe = Api.World.BlockAccessor.GetBlockEntity(Pos.DownCopy()) as BlockEntityFruitTreeBranch;
                bool soilBelow = Api.World.BlockAccessor.GetBlock(Pos.DownCopy()).Fertility > 0;
                bool verticalGrowDir = soilBelow || belowBe != null;

                if (!verticalGrowDir)
                {
                    foreach (var facing in BlockFacing.HORIZONTALS)
                    {
                        var nbe = Api.World.BlockAccessor.GetBlockEntity(Pos.AddCopy(facing)) as BlockEntityFruitTreeBranch;
                        if (nbe != null)
                        {
                            GrowthDir = facing.Opposite;
                            RootOff = nbe.RootOff.AddCopy(facing);
                            var rootBh = Api.World.BlockAccessor.GetBlockEntity(Pos.AddCopy(RootOff)).GetBehavior<FruitTreeRootBH>();
                            rootBh.RegisterTreeType(treeType);
                            rootBh.propsByType[TreeType].OnFruitingStateChange += RootBh_OnFruitingStateChange;
                            GenMesh();
                        }
                    }
                }
            }
            
            
            updateProperties();

            initCustomBehaviors(parentPlantStack, callInitialize);

            var bh = GetBehavior<FruitTreeGrowingBranchBH>();
            if (bh != null)
            {
                bh.VDrive = 3 + (float)Api.World.Rand.NextDouble();
                bh.HDrive = 1;

                if (treeType != null && RootOff?.IsZero == true)
                {
                    var props = GetBehavior<FruitTreeRootBH>().propsByType[TreeType];
                    bh.HDrive *= props.RootSizeMul;
                    bh.VDrive *= props.RootSizeMul;
                }
            }
        }

        bool beingBrokenLoopPrevention = false;
        public override void OnBlockBroken(IPlayer byPlayer)
        {
            if (beingBrokenLoopPrevention)
            {
                Api.Logger.Error(new System.Exception("Fruit tree branch would endlessly loop here")); return;
            }
            beingBrokenLoopPrevention = true;
            base.OnBlockBroken(byPlayer);

            foreach (var facing in BlockFacing.ALLFACES)
            {
                var npos = Pos.AddCopy(facing);
                Block nblock = Api.World.BlockAccessor.GetBlock(npos);
                if (nblock == blockFoliage)
                {
                    bool isSupported = false;
                    foreach (var nfacing in BlockFacing.HORIZONTALS)
                    {
                        var nnpos = npos.AddCopy(nfacing);
                        if (nnpos == Pos) continue;

                        Block nnblock = Api.World.BlockAccessor.GetBlock(nnpos);
                        if (nnblock == blockBranch) { isSupported = true; break; }
                    }

                    if (!isSupported) Api.World.BlockAccessor.BreakBlock(npos, byPlayer);
                }

                if (nblock is BlockFruitTreeBranch && facing.IsHorizontal)
                {
                    var be = Api.World.BlockAccessor.GetBlockEntity(npos);
                    if (be == null) continue;

                    var bh = be.GetBehavior<FruitTreeGrowingBranchBH>();
                    if (bh == null)
                    {
                        bh = new FruitTreeGrowingBranchBH(be);
                        bh.Initialize(Api, null);
                        be.Behaviors.Add(bh);
                    }

                    bh.OnNeighbourBranchRemoved(facing.Opposite);
                }
            }
        }

        public override void OnBlockRemoved()
        {
            base.OnBlockRemoved();

            var rootBe = (Api?.World.BlockAccessor.GetBlockEntity(Pos.AddCopy(RootOff)) as BlockEntityFruitTreeBranch)?.GetBehavior<FruitTreeRootBH>();
            if (rootBe != null)
            {
                rootBe.BlocksRemoved++;
            }
        }

        public override void OnBlockUnloaded()
        {
            base.OnBlockUnloaded();
        }


        static Dictionary<string, int[]> facingRemapByShape = new Dictionary<string, int[]>()
        {
            {  "stem", new int[] { 0, 1, 2, 3, 4, 5 } },
            {  "branch-ud", new int[] { 0, 1, 2, 3, 4, 5 } },
            {  "branch-n", new int[] { 4, 3, 5, 1, 0, 2 } },
            {  "branch-s", new int[] { 4, 3, 5, 1, 0, 2 } },
            {  "branch-w", new int[] { 0, 5, 2, 4, 3, 1 } },
            {  "branch-e", new int[] { 0, 5, 2, 4, 3, 1 } },

            {  "branch-ud-end", new int[] { 0, 1, 2, 3, 4, 5 } },
            {  "branch-n-end", new int[] { 4, 3, 5, 1, 0, 2 } },
            {  "branch-s-end", new int[] { 4, 3, 5, 1, 0, 2 } },
            {  "branch-w-end", new int[] { 0, 5, 2, 4, 3, 1 } },
            {  "branch-e-end", new int[] { 0, 5, 2, 4, 3, 1 } }
        };
        

        public void updateProperties()
        {
            if (GrowthDir.Axis != EnumAxis.Y)
            {
                float rotX = GrowthDir.Axis == EnumAxis.Z ? 90 : 0;
                float rotZ = GrowthDir.Axis == EnumAxis.X ? 90 : 0;
                if (Block == null || Block.CollisionBoxes == null)
                {
                    Api?.World.Logger.Warning("BEFruitTreeBranch:updatedProperties() Block {0} or its collision box is null? Block might have incorrect hitboxes now.", Block?.Code);
                }
                else
                {
                    colSelBoxes = new Cuboidf[] { Block.CollisionBoxes[0].Clone().RotatedCopy(rotX, 0, rotZ, new Vec3d(0.5, 0.5, 0.5)) };
                }
            }

            GenMesh();
        }

        public override void GenMesh()
        {
            branchMesh = GenMeshes();
        }

        public MeshData GenMeshes()
        {
            if (capi == null) return null;
            if (Api.Side != EnumAppSide.Client || TreeType == null || TreeType == "") return null;

            string cacheKey = "fruitTreeMeshes" + Block.Code.ToShortString();
            Dictionary<int, MeshData> meshes = ObjectCacheUtil.GetOrCreate(Api, cacheKey, () => new Dictionary<int, MeshData>());

            leavesMesh = null;
            if (PartType == EnumTreePartType.Branch && Height > 0)
            {
                GenFoliageMesh(false, out leavesMesh, out _);
            }

            string shapekey = "stem";
            switch (PartType)
            {
                case EnumTreePartType.Cutting:
                    if (GrowthDir.Axis == EnumAxis.Y) shapekey = "cutting-ud";
                    if (GrowthDir.Axis == EnumAxis.X) shapekey = "cutting-we";
                    if (GrowthDir.Axis == EnumAxis.Z) shapekey = "cutting-ns";
                    break;
                case EnumTreePartType.Branch:
                    if (GrowthDir.Axis == EnumAxis.Y) shapekey = "branch-ud";
                    else shapekey = "branch-" + GrowthDir.Code[0];

                    if (!(Api.World.BlockAccessor.GetBlock(Pos.AddCopy(GrowthDir)) is BlockFruitTreeBranch))
                    {
                        shapekey += "-end";
                    }

                    break;
                case EnumTreePartType.Leaves:
                    shapekey = "leaves";
                    break;
            }


            int meshkey = getHashCode(shapekey);
            if (meshes.TryGetValue(meshkey, out MeshData mesh))
            {
                return mesh;
            }


            var cshape = Block?.Attributes?["shapes"][shapekey].AsObject<CompositeShape>(null, Block.Code.Domain);
            if (cshape == null) return null;

            nowTesselatingShape = Shape.TryGet(Api, cshape.Base.WithPathAppendixOnce(".json").WithPathPrefixOnce("shapes/"));
            if (nowTesselatingShape == null) return null;

            List<string> selectiveElements = null;

            if (PartType != EnumTreePartType.Cutting)
            {
                selectiveElements = new List<string>(new string[] { "stem", "root/branch" });
                if (PartType != EnumTreePartType.Leaves)
                {
                    var remap = facingRemapByShape[shapekey];
                    for (int i = 0; i < 8; i++)
                    {
                        if ((SideGrowth & (1 << i)) > 0)
                        {
                            char f = BlockFacing.ALLFACES[remap[i]].Code[0];
                            selectiveElements.Add("branch-" + f);
                            selectiveElements.Add("root/branch-" + f);
                        }
                    }
                }
            }

            

            capi.Tesselator.TesselateShape("fruittreebranch", nowTesselatingShape, out mesh, this, new Vec3f(cshape.rotateX, cshape.rotateY, cshape.rotateZ), 0, 0, 0, null, selectiveElements?.ToArray());

            mesh.ClimateColorMapIds.Fill((byte)0);
            mesh.SeasonColorMapIds.Fill((byte)0);


            return meshes[meshkey] = mesh;
        }

        int getHashCode(string shapekey)
        {
            return (SideGrowth + "-" + PartType + "-" + FoliageState + "-" + GrowthDir.Index + "-" + shapekey + "-" + TreeType).GetHashCode();
        }

        


        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            base.GetBlockInfo(forPlayer, dsc);

            if (Api.World.EntityDebugMode)
            {
                dsc.AppendLine("LeavesState: " + FoliageState);
                dsc.AppendLine("TreeType: " + TreeType);

                dsc.Append("SideGrowth: ");
                foreach (var facing in BlockFacing.ALLFACES)
                {
                    if ((SideGrowth & (1 << facing.Index)) > 0) dsc.Append(facing.Code[0]);
                }
                dsc.AppendLine();

                var rootBe = (Api.World.BlockAccessor.GetBlockEntity(Pos.AddCopy(RootOff)) as BlockEntityFruitTreeBranch)?.GetBehavior<FruitTreeRootBH>();
                if (rootBe != null)
                {
                    foreach (var val in rootBe.propsByType)
                    {
                        dsc.AppendLine(val.Key + " " + val.Value.State);
                    }
                }
            }

        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            GrowTries = tree.GetInt("growTries");
            if (tree.HasAttribute("rootOffX"))
            {
                RootOff = new Vec3i(tree.GetInt("rootOffX"), tree.GetInt("rootOffY"), tree.GetInt("rootOffZ"));
            }

            initCustomBehaviors(null, false);

            base.FromTreeAttributes(tree, worldForResolving);

            SideGrowth = tree.GetInt("sideGrowth", 0);
            if (tree.HasAttribute("parentX"))
            {
                ParentOff = new Vec3i(tree.GetInt("parentX"), tree.GetInt("parentY"), tree.GetInt("parentZ"));
            }

            FastForwardGrowth = null;
            if (tree.HasAttribute("fastForwardGrowth"))
            {
                FastForwardGrowth = tree.GetFloat("fastForwardGrowth");
            }

            lastGrowthAttemptTotalDays = tree.GetDouble("lastGrowthAttemptTotalDays");

            if (Api != null)
            {
                updateProperties();
            }
        }

        void initCustomBehaviors(ItemStack parentPlantStack, bool callInitialize)
        {
            if (RootOff?.IsZero == true && GetBehavior<FruitTreeRootBH>() == null)
            {
                var bh = new FruitTreeRootBH(this, parentPlantStack);
                if (callInitialize) bh.Initialize(Api, null);
                Behaviors.Add(bh);
            }
            if (GrowTries < 60 && GetBehavior<FruitTreeGrowingBranchBH>() == null)
            {
                var bh = new FruitTreeGrowingBranchBH(this);
                if (callInitialize) bh.Initialize(Api, null);
                Behaviors.Add(bh);
            }
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);

            tree.SetInt("sideGrowth", SideGrowth);
            if (ParentOff != null)
            {
                tree.SetInt("parentX", ParentOff.X);
                tree.SetInt("parentY", ParentOff.Y);
                tree.SetInt("parentZ", ParentOff.Z);
            }

            tree.SetInt("growTries", GrowTries);

            if (FastForwardGrowth != null) {
                tree.SetFloat("fastForwardGrowth", (float)FastForwardGrowth);
            }

            tree.SetDouble("lastGrowthAttemptTotalDays", lastGrowthAttemptTotalDays);
        }


        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tessThreadTesselator)
        {
            mesher.AddMeshData(branchMesh);

            if (leavesMesh != null)
            {
                mesher.AddMeshData(leavesMesh);
            }

            return true;
        }

        
    }
}
