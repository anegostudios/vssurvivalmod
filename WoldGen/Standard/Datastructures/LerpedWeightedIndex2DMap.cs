using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vintagestory.ServerMods
{
    /// <summary>
    /// This datastructure can provide a list of weighted indices (e.g. biome index, geologic province index) for a given x/y coordinate using bilinear interpolation for coordinates with fractional values.
    /// 
    /// This datastructure is an essential part for the generation of very large scale terrain generation maps. The challenge we are facing is that we require smooth transitions from one dicrete state to another. How do we do it? We turn it into a list of states connected with a weighting factor. E.g. 90% mountain, 10% plains. 
    /// This requires us to:
    /// - Turn scalar values into lists of values + weight
    /// - Perform bilienar interpolation to create smooth transitions
    /// - (optional) Perform a "blur" to further smoothen transitions
    /// </summary>
    public class LerpedWeightedIndex2DMap
    {
        public int sizeX;

        // Non interpolated values
        WeightedIndex[][] groups;


        public LerpedWeightedIndex2DMap(int[] discreteValues2d, int sizeX)
        {
            this.sizeX = sizeX;
            groups = new WeightedIndex[discreteValues2d.Length][];

            for (int i = 0; i < discreteValues2d.Length; i++)
            {
                groups[i] = new WeightedIndex[] { new WeightedIndex() { index = discreteValues2d[i], weight = 1 } };
            }
        }

        public LerpedWeightedIndex2DMap(int[] rawScalarValues, int size, int boxBlurRadius)
        {
            this.sizeX = size;
            groups = new WeightedIndex[rawScalarValues.Length][];

            
            
            Dictionary<int, float> indices = new Dictionary<int, float>();

            for (int x = 0; x < size; x++)
            {
                // TODO: Use fast blur for better performance (see Cairo::SurfaceTransformBlur or http://blog.ivank.net/fastest-gaussian-blur.html)
                for (int z = 0; z < size; z++)
                {
                    int minx = Math.Max(0, x - boxBlurRadius);
                    int minz = Math.Max(0, z - boxBlurRadius);

                    int maxx = Math.Min(size - 1, x + boxBlurRadius);
                    int maxz = Math.Min(size - 1, z + boxBlurRadius);

                    indices.Clear();


                    float weightFrac = 1f / ((maxx - minx + 1) * (maxz - minz + 1));

                    // Box Blur
                    for (int bx = minx; bx <= maxx; bx++)
                    {
                        for (int bz = minz; bz <= maxz; bz++)
                        {
                            int index = rawScalarValues[bz * size + bx];

                            if (indices.ContainsKey(index))
                            {
                                indices[index] += weightFrac;
                            }
                            else
                            {
                                indices[index] = weightFrac;
                            }
                        }
                    }

                    // Write blurred entries for this coordinate
                    groups[z * size + x] = new WeightedIndex[indices.Count];
                    int i = 0;
                    foreach (var val in indices)
                    {
                        groups[z * size + x][i++] = new WeightedIndex() { index = val.Key, weight = val.Value };
                    }
                }
            }
        }


        
        // Interpolated values
        public WeightedIndex[] this[float x, float z]
        {
            get
            {
                WeightedIndex[] weightedIndicesTop = Lerp(
                    groups[(int)z * sizeX + (int)x],
                    groups[(int)z * sizeX + (int)x + 1],
                    x - (int)x
                );

                WeightedIndex[] weightedIndicesBottom = Lerp(
                    groups[(int)(z+1) * sizeX + (int)x],
                    groups[(int)(z+1) * sizeX + (int)x + 1],
                    x - (int)x
                );
                
                return LerpSorted(weightedIndicesTop, weightedIndicesBottom, z - (int)z);
            }
        }


        WeightedIndex[] Lerp(WeightedIndex[] left, WeightedIndex[] right, float lerp)
        {
            Dictionary<int, WeightedIndex> indices = new Dictionary<int, WeightedIndex>();

            for (int i = 0; i < left.Length; i++)
            {
                int index = left[i].index;
                if (indices.ContainsKey(index))
                {
                    indices[index].weight += (1 - lerp) * left[i].weight;
                } else
                {
                    indices[index] = new WeightedIndex() { index = index, weight = (1 - lerp) * left[i].weight };
                }
            }

            for (int i = 0; i < right.Length; i++)
            {
                int index = right[i].index;
                if (indices.ContainsKey(index))
                {
                    indices[index].weight += lerp * right[i].weight;
                }
                else
                {
                    indices[index] = new WeightedIndex() { index = index, weight = lerp * right[i].weight };
                }
            }

            return indices.Values.ToArray();
        }


        // Exactly same method as above but using a SortedDictionary
        WeightedIndex[] LerpSorted(WeightedIndex[] left, WeightedIndex[] right, float lerp)
        {
            SortedDictionary<int, WeightedIndex> indices = new SortedDictionary<int, WeightedIndex>();

            for (int i = 0; i < left.Length; i++)
            {
                int index = left[i].index;
                if (indices.ContainsKey(index))
                {
                    indices[index].weight += (1 - lerp) * left[i].weight;
                }
                else
                {
                    indices[index] = new WeightedIndex() { index = index, weight = (1 - lerp) * left[i].weight };
                }
            }

            for (int i = 0; i < right.Length; i++)
            {
                int index = right[i].index;
                if (indices.ContainsKey(index))
                {
                    indices[index].weight += lerp * right[i].weight;
                }
                else
                {
                    indices[index] = new WeightedIndex() { index = index, weight = lerp * right[i].weight };
                }
            }

            return indices.Values.ToArray();
        }


        public void Split(WeightedIndex []weightedIndices, out int[] indices, out float[] weights)
        {
            indices = new int[weightedIndices.Length];
            weights = new float[weightedIndices.Length];

            for (int i = 0; i < weightedIndices.Length; i++)
            {
                indices[i] = weightedIndices[i].index;
                weights[i] = weightedIndices[i].weight;
            }
        }
    }
}
