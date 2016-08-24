using System;

namespace Psns.Common.InterProcess
{
    internal static class FunctionalExtensions
    {
        public static T tee<T>(this T @this, Action<T> action)
        {
            action(@this);
            return @this;
        }
    }
}