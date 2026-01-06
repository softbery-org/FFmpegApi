// Version: 0.0.0.1
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public static class Math
{
    /// <summary>
    /// Clamps the specified value to be within the inclusive range defined by the minimum and maximum values.
    /// </summary>
    /// <typeparam name="T">The type of the value, which must implement <see cref="IComparable{T}"/>.</typeparam>
    /// <param name="value">The value to clamp.</param>
    /// <param name="min">The lower bound of the range.</param>
    /// <param name="max">The upper bound of the range.</param>
    /// <returns>The clamped value, which is the original value if it falls within the range, otherwise the nearest bound.</returns>
    public static T Clamp<T>(T value, T min, T max) where T : IComparable<T>
    {
        if (value.CompareTo(min) < 0) return min;
        if (value.CompareTo(max) > 0) return max;
        return value;
    }

    /// <summary>
    /// Clamps the specified double value to be within the inclusive range defined by the minimum and maximum values.
    /// </summary>
    /// <param name="value">The value to clamp.</param>
    /// <param name="min">The lower bound of the range.</param>
    /// <param name="max">The upper bound of the range.</param>
    /// <returns>The clamped value, which is the original value if it falls within the range, otherwise the nearest bound.</returns>
    public static double Clamp(double value, double min, double max)
    {
        return System.Math.Max(min, System.Math.Min(max, value));
    }
}
