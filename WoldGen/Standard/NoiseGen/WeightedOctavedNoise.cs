using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API;
using Vintagestory.API.MathTools;

namespace Vintagestory.ServerMods
{
    class WeightedOctavedNoise : NormalizedSimplexNoise
    {
        private double[] offsets;

        public WeightedOctavedNoise(double[] offsets, double[] inputAmplitudes, double[] frequencies, long seed) : base(inputAmplitudes, frequencies, seed)
        {
            this.offsets = offsets;
        }


        public override double Noise(double x, double y)
        {
            double value = 1;
            double amp;
            for (int i = 0; i < inputAmplitudes.Length; i++)
            {
                amp = inputAmplitudes[i];
                value += Math.Min(amp, Math.Max(-amp, octaves[i].Evaluate(x * frequencies[i], y * frequencies[i]) * amp - offsets[i]));
            }

            return value / 2;
        }

        public override double Noise(double x, double y, double z)
        {
            double value = 1;
            double amp;

            for (int i = 0; i < inputAmplitudes.Length; i++)
            {
                amp = inputAmplitudes[i];
                value += Math.Min(amp, Math.Max(-amp, octaves[i].Evaluate(x * frequencies[i], y * frequencies[i], z * frequencies[i]) * amp));
            }

            return value / 2;
        }



    }
}
