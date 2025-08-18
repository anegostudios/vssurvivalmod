using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

#nullable disable

namespace Vintagestory.GameContent
{
    public class OvenItemData
    {
        /// <summary>
        /// The temperature needed to start baking (default 160 degrees C, a typical Maillard browning temperature)
        /// </summary>
        public float BrowningPoint;
        /// <summary>
        /// The amount of time these items need for a perfect bake (taken from item properties)
        /// </summary>
        public float TimeToBake;
        /// <summary>
        /// How close is the item to fully baked (fully done == 1.0, greater values mean burnt)
        /// </summary>
        public float BakedLevel;
        /// <summary>
        /// How close is the item to fully risen (fully done == 1.0, greater values ignored)
        /// </summary>
        public float CurHeightMul;

        /// <summary>
        /// The current temperature of this item (may be less than the oven temperature if it was recently placed)
        /// </summary>
        public float temp;

        /// <summary>
        /// How close is the item to fully risen (fully done == 1.0, greater values ignored)
        /// </summary>
        public float CurZMul;

        /// <summary>
        /// How close is the item to fully risen (fully done == 1.0, greater values ignored)
        /// </summary>
        public float CurXMul;

        public OvenItemData()
        {

        }

        public OvenItemData(float browning, float time, float baked = 0f, float risen = 0f, float tCurrent = 20f, float risenx = 0f, float risenz = 0f)
        {
            this.BrowningPoint = browning;
            this.TimeToBake = time;
            this.BakedLevel = baked;
            this.CurXMul = risenx;
            this.CurHeightMul = risen;
            this.CurZMul = risenz;
            this.temp = tCurrent;
            
        }

        public OvenItemData(ItemStack stack)
        {
            BakingProperties bakeprops = BakingProperties.ReadFrom(stack);
            this.BrowningPoint = bakeprops?.Temp ?? 160;
            this.TimeToBake = stack.Collectible.CombustibleProps?.MeltingDuration * 10f ?? 150f;
            this.BakedLevel = bakeprops?.LevelFrom ?? 1f;
            this.CurXMul = bakeprops?.StartScaleX ?? 1f;
            this.CurHeightMul = bakeprops?.StartScaleY ?? 1f;
            this.CurZMul = bakeprops?.StartScaleZ ?? 1f;
            this.temp = 20f;
        }

        public static OvenItemData ReadFromTree(ITreeAttribute tree, int i)
        {
            return new OvenItemData(
                tree.GetFloat("brown" + i),
                tree.GetFloat("tbake" + i),
                tree.GetFloat("baked" + i),
                tree.GetFloat("heightmul" + i),
                tree.GetFloat("temp" + i),
                tree.GetFloat("xmul" + i),
                tree.GetFloat("zmul" + i)
            );
        }

        public void WriteToTree(ITreeAttribute tree, int i)
        {
            tree.SetFloat("brown" + i, this.BrowningPoint);
            tree.SetFloat("tbake" + i, this.TimeToBake);
            tree.SetFloat("baked" + i, this.BakedLevel);
            tree.SetFloat("heightmul" + i, this.CurHeightMul);
            tree.SetFloat("temp" + i, this.temp);
            tree.SetFloat("xmul" + i, this.CurXMul);
            tree.SetFloat("zmul" + i, this.CurZMul);
        }
    }


}
