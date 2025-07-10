using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.GameContent
{
    public interface ILiquidInterface
    {
        bool AllowHeldLiquidTransfer { get; }

        /// <summary>
        /// Liquid Capacity in litres
        /// </summary>
        float CapacityLitres { get; }

        /// <summary>
        /// Smalles amount of liquid that can be transfered with this container
        /// </summary>
        float TransferSizeLitres { get; }


        /// <summary>
        /// Current amount of liquid in this container in the inventory. From 0...Capacity
        /// </summary>
        /// <param name="world"></param>
        /// <param name="containerStack"></param>
        /// <returns></returns>
        float GetCurrentLitres(ItemStack containerStack);

        /// <summary>
        /// Current amount of liquid in this placed container. From 0...Capacity
        /// </summary>
        /// <param name="world"></param>
        /// <param name="pos"></param>
        /// <returns></returns>
        float GetCurrentLitres(BlockPos pos);

        bool IsFull(ItemStack containerStack);
        bool IsFull(BlockPos pos);


        /// <summary>
        /// Retrives the containable properties of the currently contained itemstack
        /// </summary>
        /// <param name="world"></param>
        /// <param name="containerStack"></param>
        /// <returns></returns>
        WaterTightContainableProps GetContentProps(ItemStack containerStack);

        /// <summary>
        /// Retrives the containable properties of the container block at given position
        /// </summary>
        /// <param name="world"></param>
        /// <param name="pos"></param>
        /// <returns></returns>
        WaterTightContainableProps GetContentProps(BlockPos pos);

        /// <summary>
        /// Returns the containing itemstack of the liquid source stack
        /// </summary>
        /// <param name="world"></param>
        /// <param name="containerStack"></param>
        /// <returns></returns>
        ItemStack GetContent(ItemStack containerStack);

        /// <summary>
        /// Returns the containing itemstack of a placed liquid source block
        /// </summary>
        /// <param name="world"></param>
        /// <param name="pos"></param>
        /// <returns></returns>
        ItemStack GetContent(BlockPos pos);
    }


    public interface ILiquidSource : ILiquidInterface
    {
        /// <summary>
        /// Tries to take out as much items/liquid as possible and returns it
        /// </summary>
        /// <param name="world"></param>
        /// <param name="containerStack"></param>
        /// <param name="quantity"></param>
        /// <returns></returns>
        ItemStack TryTakeContent(ItemStack containerStack, int quantity);


        /// <summary>
        /// Tries to take out as much items/liquid as possible from a placed container and returns it
        /// </summary>
        /// <param name="world"></param>
        /// <param name="pos"></param>
        /// <param name="quantity"></param>
        ItemStack TryTakeContent(BlockPos pos, int quantity);
    }


    public interface ILiquidSink : ILiquidInterface
    {
        /// <summary>
        /// Sets the liquid source contents to given stack
        /// </summary>
        /// <param name="containerStack"></param>
        /// <param name="content"></param>
        void SetContent(ItemStack containerStack, ItemStack content);

        /// <summary>
        /// Sets the containers contents to placed liquid source block
        /// </summary>
        /// <param name="world"></param>
        /// <param name="pos"></param>
        /// <param name="content"></param>
        void SetContent(BlockPos pos, ItemStack content);


        /// <summary>
        /// Tries to put as much items/liquid as possible into a placed container and returns it how much items it actually moved
        /// </summary>
        /// <param name="pos"></param>
        /// <param name="liquidStack"></param>
        /// <param name="desiredLitres"></param>
        /// <returns>Amount of items moved (stacksize, not litres)</returns>
        int TryPutLiquid(BlockPos pos, ItemStack liquidStack, float desiredLitres);



        /// <summary>
        /// Tries to place in items/liquid and returns actually inserted quantity
        /// </summary>
        /// <param name="containerStack"></param>
        /// <param name="liquidStack"></param>
        /// <param name="desiredLitres"></param>
        /// <returns></returns>
        int TryPutLiquid(ItemStack containerStack, ItemStack liquidStack, float desiredLitres);

    }
}
