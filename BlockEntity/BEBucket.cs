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

namespace Vintagestory.GameContent
{
    public class BlockEntityBucket : BlockEntityLiquidContainer
    {
        public override string InventoryClassName => "bucket";

        MeshData currentMesh;
        BlockBucket ownBlock;

        public float MeshAngle;

        public BlockEntityBucket()
        {
            inventory = new InventoryGeneric(1, null, null);
        }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            ownBlock = Block as BlockBucket;
            if (Api.Side == EnumAppSide.Client)
            {
                currentMesh = GenMesh();
                MarkDirty(true);
            }
        }


        public override void OnBlockBroken(IPlayer byPlayer = null)
        {
            // Don't drop inventory contents
        }


        public override void OnBlockPlaced(ItemStack byItemStack = null)
        {
            base.OnBlockPlaced(byItemStack);

            if (Api.Side == EnumAppSide.Client)
            {
                currentMesh = GenMesh();
                MarkDirty(true);
            }
        }

        


        internal MeshData GenMesh()
        {
            if (ownBlock == null) return null;
            
            MeshData mesh = ownBlock.GenMesh(Api as ICoreClientAPI, GetContent(), Pos);

            if (mesh.CustomInts != null)
            {
                for (int i = 0; i < mesh.CustomInts.Count; i++)
                {
                    mesh.CustomInts.Values[i] |= 1 << 27; // Disable water wavy
                    mesh.CustomInts.Values[i] |= 1 << 26; // Enabled weak foam
                }
            }

            return mesh;
        }

        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tesselator)
        {
            mesher.AddMeshData(currentMesh.Clone().Rotate(new Vec3f(0.5f, 0.5f, 0.5f), 0, MeshAngle, 0));
            return true;
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);

            MeshAngle = tree.GetFloat("meshAngle", MeshAngle);

            if (Api?.Side == EnumAppSide.Client)
            {
                currentMesh = GenMesh();
                MarkDirty(true);
            }
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);

            tree.SetFloat("meshAngle", MeshAngle);
        }


        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            ItemSlot slot = inventory[0];

            if (slot.Empty)
            {
                dsc.AppendLine(Lang.Get("Empty"));
            } else 
            {
                dsc.AppendLine(Lang.Get("Contents: {0}x{1}", slot.Itemstack.StackSize, slot.Itemstack.GetName()));
            }
        }

    }
}
