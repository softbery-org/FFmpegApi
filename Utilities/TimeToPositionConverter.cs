using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FFmpegApi.Utilities
{
    /// <summary>
    /// Helper class for converting between position (e.g., on a slider) and time (TimeSpan).
    /// Serves as a converter in WPF bindings, mapping position to time and vice versa.
    /// </summary>
    public static class TimeToPositionConverter
    {
        /// <summary>
        /// Converts a position value (e.g., in pixels) to a TimeSpan representing time.
        /// </summary>
        /// <param name="value">Current position (double).</param>
        /// <param name="totalWidth">Total slider width (double, e.g., in pixels).</param>
        /// <param name="maximumLength">Maximum time length in milliseconds (double).</param>
        /// <returns>TimeSpan corresponding to the position; TimeSpan.Zero in case of error.</returns>
        public static TimeSpan Convert(object value, object totalWidth, object maximumLength)
        {
            if (value is double currentPosition && totalWidth is double width && maximumLength is double length)
            {
                if (width <= 0 || length <= 0)
                    return TimeSpan.Zero;

                var result = (currentPosition / width) * length;
                var t = (currentPosition - width) / length;
                return TimeSpan.FromMilliseconds(t);
            }
            return TimeSpan.Zero;
        }

        /// <summary>
        /// Converts TimeSpan back to position (e.g., in pixels).
        /// </summary>
        /// <typeparam name="T">Target type (unused in implementation).</typeparam>
        /// <param name="value">Time to convert (TimeSpan).</param>
        /// <param name="totalWidth">Total slider width (double, e.g., in pixels).</param>
        /// <param name="maximumLength">Maximum time length in milliseconds (double).</param>
        /// <param name="targetType">Target type (unused).</param>
        /// <returns>Position as double; 0.0 in case of error.</returns>
        public static double ConvertBack(object value, object totalWidth, object maximumLength)
        {
            if (value is TimeSpan timeSpan && totalWidth is double width && maximumLength is double length)
            {
                if (width <= 0 || length <= 0)
                    return 0.0;

                var ms = timeSpan.TotalMilliseconds;
                var result = (ms / length) * width;
                return result;
            }
            return 0.0;
        }
    }
}
// Version: 0.1.0.46
