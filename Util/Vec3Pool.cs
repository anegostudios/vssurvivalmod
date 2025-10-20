using System.Collections.Generic;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public static class Vec3Pool
    {
        private static readonly Stack<Vec3d> vec3dPool = new Stack<Vec3d>(64);
        private static readonly Stack<Vec3f> vec3fPool = new Stack<Vec3f>(64);
        private static readonly object lockObj = new object();

        public static Vec3d GetVec3d()
        {
            lock (lockObj)
            {
                return vec3dPool.Count > 0 ? vec3dPool.Pop() : new Vec3d();
            }
        }

        public static void ReturnVec3d(Vec3d vec)
        {
            if (vec == null) return;
            lock (lockObj)
            {
                if (vec3dPool.Count < 128)
                {
                    vec.Set(0, 0, 0);
                    vec3dPool.Push(vec);
                }
            }
        }

        public static Vec3f GetVec3f()
        {
            lock (lockObj)
            {
                return vec3fPool.Count > 0 ? vec3fPool.Pop() : new Vec3f();
            }
        }

        public static void ReturnVec3f(Vec3f vec)
        {
            if (vec == null) return;
            lock (lockObj)
            {
                if (vec3fPool.Count < 128)
                {
                    vec.Set(0, 0, 0);
                    vec3fPool.Push(vec);
                }
            }
        }
    }
}
