using System;
using Vintagestory.API;


namespace Vintagestory.ServerMods
{
    class NoiseWind : NoiseBase
    {
        static int minStrength = 1;
        static int maxStrength = 20;


        public NoiseWind(long seed) : base(seed)
        {

        }

        public PolarVector getWindAt(double xpos, double zpos)
        {
            int xPosInt = (int) xpos;
            int zPosInt = (int) zpos;

            InitPositionSeed(xPosInt, zPosInt);
            PolarVector leftTop = new PolarVector((float)(NextInt(360) / 180.0 * Math.PI), NextInt(maxStrength - minStrength) + minStrength);

            InitPositionSeed(xPosInt - 1, zPosInt);
            PolarVector rightTop = new PolarVector((float)(NextInt(360) / 180.0 * Math.PI), NextInt(maxStrength - minStrength) + minStrength);

            InitPositionSeed(xPosInt, zPosInt - 1);
            PolarVector leftBottom = new PolarVector((float)(NextInt(360) / 180.0 * Math.PI), NextInt(maxStrength - minStrength) + minStrength);

            InitPositionSeed(xPosInt - 1, zPosInt - 1);
            PolarVector rightBottom = new PolarVector((float)(NextInt(360) / 180.0 * Math.PI), NextInt(maxStrength - minStrength) + minStrength);

            return BiLerp((float)(xpos - xPosInt), (float)(zpos - zPosInt), leftTop, rightTop, leftBottom, rightBottom);
        }


        static PolarVector BiLerp(float lx, float ly, PolarVector leftTop, PolarVector rightTop, PolarVector leftBottom, PolarVector rightBottom)
        {
            PolarVector top = new PolarVector(
                lx * leftTop.angle + (1-lx) * rightTop.angle,
                lx * leftTop.length + (1 - lx) * rightTop.length
            );

            PolarVector bottom = new PolarVector(
                lx * leftBottom.angle + (1 - lx) * rightBottom.angle,
                lx * leftBottom.length + (1 - lx) * rightBottom.length
            );

            return new PolarVector(
                ly * top.angle + (1 - ly) * bottom.angle,
                ly * top.length + (1 - ly) * bottom.length
            );
        }


        static PolarVector SmoothLerp(float lx, float ly, PolarVector w1, PolarVector w2, PolarVector w3, PolarVector w4)
        {
            float lxSmooth = SmoothStep(lx);
            float lySmooth = SmoothStep(ly);

            float lxISmooth = SmoothStep(1 - lx);
            float lyISmooth = SmoothStep(1 - ly);

            PolarVector r1 = new PolarVector(
                lxSmooth * w1.angle + lxISmooth * w2.angle,
                lxSmooth * w1.length + lxISmooth * w2.length
            );

            PolarVector r2 = new PolarVector(
                lxSmooth * w3.angle + lxISmooth * w4.angle,
                lxSmooth * w3.length + lxISmooth * w4.length
            );

            return new PolarVector(
                lySmooth * r1.angle + lyISmooth * r2.angle,
                lySmooth * r1.length + lyISmooth * r2.length
            );
        }


        static float SmoothStep(float x) { return x * x * (3.0f - 2.0f * x); }


    }

    public class PolarVector
    {
        public float angle;
        public float length;

        public PolarVector(float angle, float length)
        {
            this.angle = angle;
            this.length = length;
        }

        public override string ToString()
        {
            return "angle " + angle + ", length " + length;
        }
    }

}
