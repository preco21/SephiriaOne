using System;
using System.Collections.Generic;

namespace SephiriaOne
{
    internal static class HotkeyInputOwnership
    {
        internal static bool IsBlocked<T>(IList<T> stack, Predicate<T> owned) where T : class
        {
            return stack != null && stack.Count > 0 && stack[stack.Count - 1] != null && !owned(stack[stack.Count - 1]);
        }
    }
}
