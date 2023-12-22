using System;
using System.Collections.Generic;
using System.Linq;

namespace Vintagestory.ServerMods
{
    /// <summary>
    /// This datastructure can provide a list of weighted indices (e.g. biome index, geologic province index) for a given x/y coordinate using bilinear interpolation for coordinates with fractional values.
    /// 
    /// This datastructure is an essential part for the generation of very large scale terrain generation maps. The challenge we are facing is that we require smooth transitions from one discrete state to another. How do we do it? We turn it into a list of states connected with a weighting factor. E.g. 90% mountain, 10% plains. 
    /// This requires us to:
    /// - Turn scalar values into lists of values + weight
    /// - Perform bilienar interpolation to create smooth transitions
    /// - (optional) Perform a "blur" to further smoothen transitions
    /// </summary>
    public class LerpedWeightedIndex2DMap
    {
        public int sizeX;

        public int topleftPadding = 0;
        public int botRightPadding = 0;

        // Non interpolated values
        WeightedIndex[][] groups;


        public LerpedWeightedIndex2DMap(int[] discreteValues2d, int sizeX)
        {
            this.sizeX = sizeX;
            groups = new WeightedIndex[discreteValues2d.Length][];

            for (int i = 0; i < discreteValues2d.Length; i++)
            {
                groups[i] = new WeightedIndex[] { new WeightedIndex() { Index = discreteValues2d[i], Weight = 1 } };
            }
        }

        public LerpedWeightedIndex2DMap(int[] rawScalarValues, int sizeX, int boxBlurRadius, int dataTopLeftPadding, int dataBotRightPadding)
        {
            this.sizeX = sizeX;
            this.topleftPadding = dataTopLeftPadding;
            this.botRightPadding = dataBotRightPadding;
            groups = new WeightedIndex[rawScalarValues.Length][];

            Dictionary<int, float> indices = new Dictionary<int, float>();

            for (int x = 0; x < sizeX; x++)
            {
                for (int z = 0; z < sizeX; z++)
                {
                    int minx = Math.Max(0, x - boxBlurRadius);
                    int minz = Math.Max(0, z - boxBlurRadius);

                    int maxx = Math.Min(sizeX - 1, x + boxBlurRadius);
                    int maxz = Math.Min(sizeX - 1, z + boxBlurRadius);

                    indices.Clear();


                    float weightFrac = 1f / ((maxx - minx + 1) * (maxz - minz + 1));

                    // Box Blur
                    for (int bx = minx; bx <= maxx; bx++)
                    {
                        for (int bz = minz; bz <= maxz; bz++)
                        {
                            int index = rawScalarValues[bz * sizeX + bx];

                            if (indices.TryGetValue(index, out float prevValue))
                            {
                                indices[index] = weightFrac + prevValue;
                            }
                            else
                            {
                                indices[index] = weightFrac;
                            }
                        }
                    }

                    // Write blurred entries for this coordinate
                    groups[z * sizeX + x] = new WeightedIndex[indices.Count];
                    int i = 0;
                    foreach (var val in indices)
                    {
                        groups[z * sizeX + x][i++] = new WeightedIndex() { Index = val.Key, Weight = val.Value };
                    }
                }
            }
        }



        // Interpolated values
        /// <summary>
        /// Does lerp calculations using a working array provided by the caller (thread-safe).  The working array should be twice as long as the output array
        /// </summary>
        public float[] WeightsAt(float x, float z, float[] output)
        {
            for (int i = 0; i < output.Length; i++)
            {
                output[i] = 0;
            }

            int posXLeft = (int)Math.Floor(x - 0.5f) + topleftPadding;
            int posXRight = posXLeft + 1;

            int posZLeft = (int)Math.Floor(z - 0.5f) + topleftPadding;
            int posZRight = posZLeft + 1;

            float fx = x - (posXLeft - topleftPadding + 0.5f);
            float fz = z - (posZLeft - topleftPadding + 0.5f);

            HalfBiLerp(   // Top
                groups[posZLeft * sizeX + posXLeft],
                groups[posZLeft * sizeX + posXRight],
                fx,
                output,
                (1 - fz)
            );

            HalfBiLerp(    // Bottom
                groups[posZRight * sizeX + posXLeft],
                groups[posZRight * sizeX + posXRight],
                fx,
                output,
                fz
            );

            return output;
        }

        public WeightedIndex[] this[float x, float z]
        {
            get
            {
                int posXLeft = (int)Math.Floor(x - 0.5f);
                int posXRight = posXLeft + 1;

                int posZLeft = (int)Math.Floor(z - 0.5f);
                int posZRight = posZLeft + 1;

                float fx = x - (posXLeft + 0.5f);
                float fz = z - (posZLeft + 0.5f);

                WeightedIndex[] weightedIndicesTop = Lerp(
                    groups[(posZLeft + topleftPadding) * sizeX + posXLeft + topleftPadding],
                    groups[(posZLeft + topleftPadding) * sizeX + posXRight + topleftPadding],
                    fx
                );

                WeightedIndex[] weightedIndicesBottom = Lerp(
                    groups[(posZRight + topleftPadding) * sizeX + posXLeft + topleftPadding],
                    groups[(posZRight + topleftPadding) * sizeX + posXRight + topleftPadding ],
                    fx
                );

                return LerpSorted(weightedIndicesTop, weightedIndicesBottom, fz);
            }
        }


        WeightedIndex[] Lerp(WeightedIndex[] left, WeightedIndex[] right, float lerp)
        {
            Dictionary<int, WeightedIndex> indices = new Dictionary<int, WeightedIndex>();

            for (int i = 0; i < left.Length; i++)
            {
                int index = left[i].Index;
                WeightedIndex windex;
                indices.TryGetValue(index, out windex);

                indices[index] = new WeightedIndex(index, windex.Weight + (1 - lerp) * left[i].Weight);
            }

            for (int i = 0; i < right.Length; i++)
            {
                int index = right[i].Index;
                WeightedIndex windex;
                indices.TryGetValue(index, out windex);

                indices[index] = new WeightedIndex(index, windex.Weight + lerp * right[i].Weight);
            }

            return indices.Values.ToArray();
        }


        // Exactly same method as above but using a SortedDictionary
        WeightedIndex[] LerpSorted(WeightedIndex[] left, WeightedIndex[] right, float lerp)
        {
            SortedDictionary<int, WeightedIndex> indices = new SortedDictionary<int, WeightedIndex>();

            for (int i = 0; i < left.Length; i++)
            {
                int index = left[i].Index;
                WeightedIndex windex;
                indices.TryGetValue(index, out windex);

                indices[index] = new WeightedIndex() { Index = index, Weight = windex.Weight + (1 - lerp) * left[i].Weight };
            }

            for (int i = 0; i < right.Length; i++)
            {
                int index = right[i].Index;
                WeightedIndex windex;
                indices.TryGetValue(index, out windex);

                indices[index] = new WeightedIndex() { Index = index, Weight = windex.Weight + lerp * right[i].Weight };
            }

            return indices.Values.ToArray();
        }


        public void Split(WeightedIndex[] weightedIndices, out int[] indices, out float[] weights)
        {
            indices = new int[weightedIndices.Length];
            weights = new float[weightedIndices.Length];

            for (int i = 0; i < weightedIndices.Length; i++)
            {
                indices[i] = weightedIndices[i].Index;
                weights[i] = weightedIndices[i].Weight;
            }
        }

        private void HalfBiLerp(WeightedIndex[] left, WeightedIndex[] right, float lerp, float[] output, float overallweight)
        {
            for (int i = 0; i < left.Length; i++)
            {
                int index = left[i].Index;
                output[index] += ((1 - lerp) * left[i].Weight) * overallweight;
            }

            for (int i = 0; i < right.Length; i++)
            {
                int index = right[i].Index;
                output[index] += (lerp * right[i].Weight) * overallweight;
            }
        }
    }
}
