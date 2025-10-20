using System;

namespace Vintagestory.GameContent
{
    /// <summary>
    /// Helper for using stackalloc for small temporary buffers
    /// Reduces GC pressure by allocating on stack instead of heap
    /// </summary>
    public static class StackAllocHelper
    {
        public const int MaxStackAllocSize = 1024;

        public static bool CanStackAlloc<T>(int count) where T : unmanaged
        {
            return count * sizeof(T) <= MaxStackAllocSize;
        }
    }
}
