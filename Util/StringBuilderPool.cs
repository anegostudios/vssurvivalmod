using System.Text;

namespace Vintagestory.GameContent
{
    public static class StringBuilderPool
    {
        [ThreadStatic]
        private static StringBuilder cached;

        public static StringBuilder Get()
        {
            var sb = cached;
            if (sb != null)
            {
                cached = null;
                sb.Clear();
                return sb;
            }
            return new StringBuilder();
        }

        public static void Return(StringBuilder sb)
        {
            if (sb.Capacity <= 1024)
            {
                cached = sb;
            }
        }

        public static string GetStringAndReturn(StringBuilder sb)
        {
            var result = sb.ToString();
            Return(sb);
            return result;
        }
    }
}
