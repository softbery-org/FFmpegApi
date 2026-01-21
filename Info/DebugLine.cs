// Version: 0.0.0.1
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FFmpegApi.Info
{
    /// <summary>
    /// Delegate for getting class name
    /// </summary>
    /// <returns>BaseString of Class</returns>
    public delegate string GetClassNameDelegate(object sender);

    /// <summary>
    /// Static class for extending Console.WriteLine functionality
    /// </summary>
    public static class Debuger
    {
        /// <summary>
        /// Get method base name by type name
        /// </summary>
        /// <returns>class name</returns>
        private static string GetName(object sender)
        {
            if (sender == null)
                return "Unknown";
            return sender.GetType().FullName;
        }

        /// <summary>
        /// Write line using Debug.WriteLine() with exception
        /// </summary>
        /// <example>
        /// Debug.WriteLine($"[{datetime}][{class_name}][{ex.HResult}]: {ex.Message}");
        /// </example>
        /// <param name="sender">Sender object</param>
        /// <param name="ex">Exception to use</param>
        public static void WriteLine(this object sender, Exception ex)
        {
            string data = DateTime.Now.ToString("dd-MM-yyyy H:mm:ss");
            GetClassNameDelegate delegat = GetName;
            var class_name = delegat(sender);

            if (ex != null)
                Debug.WriteLine($"[{data}][{class_name}][{ex.HResult}]: {ex.Message}");
            else
                Debug.WriteLine($"[{data}][{class_name}]: Exception is null");
        }

        /// <summary>
        /// Write line using Debug.WriteLine() with string value
        /// </summary>
        /// <example>
        /// Debug.WriteLine($"[{datetime}][{class_name}]: {msg}");
        /// </example>
        /// <param name="sender">Sender object</param>
        /// <param name="msg">string message value</param>
        public static void WriteLine(this object sender, object msg)
        {
            string data = DateTime.Now.ToString("dd-MM-yyyy H:mm:ss");
            GetClassNameDelegate delegat = GetName;
            var class_name = delegat(sender);

            if (!string.IsNullOrEmpty(msg.ToString()))
                Debug.WriteLine($"[{data}][{class_name}]: {msg}");
            else
                Debug.WriteLine($"[{data}][{class_name}]: Message object is null");
        }
    }
}
