using System;
using System.Collections.Generic;
using System.IO;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public interface IFirePit
    {
        bool IsBurning { get; }
    }

    public enum EnumFirepitModel
    {
        Normal = 0,
        Spit = 1,
        Wide = 2
    }

    public interface IInFirepitMeshSupplier
    {
        /// <summary>
        /// Return the mesh you want to be rendered in the firepit. You can return null to signify that you do not wish to use a custom mesh.
        /// </summary>
        /// <param name="stack"></param>
        /// <param name="world"></param>
        /// <param name="pos"></param>
        /// <param name="firepitModel"></param>
        /// <returns></returns>
        MeshData GetMeshWhenInFirepit(ItemStack stack, IWorldAccessor world, BlockPos pos, ref EnumFirepitModel firepitModel);
    }

    public class InFirePitProps
    {
        public ModelTransform Transform;
        public EnumFirepitModel UseFirepitModel;
    }

    public interface IInFirepitRenderer : IRenderer
    {
        /// <summary>
        /// Called every 100ms in case you want to do custom stuff, such as playing a sound after a certain temperature
        /// </summary>
        /// <param name="temperature"></param>
        void OnUpdate(float temperature);

        /// <summary>
        /// Called when the itemstack has been moved to the output slot
        /// </summary>
        void OnCookingComplete();
    }

    public interface IInFirepitRendererSupplier
    {
        /// <summary>
        /// Return the renderer that perfroms the rendering of your block/item in the firepit. You can return null to signify that you do not wish to use a custom renderer
        /// </summary>
        /// <param name="stack"></param>
        /// <param name="world"></param>
        /// <param name="pos"></param>
        /// <returns></returns>
        IInFirepitRenderer GetRendererWhenInFirepit(ItemStack stack, BlockEntityFirepit firepit, bool forOutputSlot);

        /// <summary>
        /// The model type the firepit should be using while you render your custom item
        /// </summary>
        /// <param name="stack"></param>
        /// <param name="world"></param>
        /// <param name="pos"></param>
        /// <returns></returns>
        EnumFirepitModel GetDesiredFirepitModel(ItemStack stack, BlockEntityFirepit firepit, bool forOutputSlot);
    }

}