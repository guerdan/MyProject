
using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Script.Util
{
    /// <summary>
    /// Debug-Util，本项目简称DU
    /// </summary>
    public static class DU
    {
        public static void Log(object msg)
        {
            Debug.Log(msg.ToString());
        }
        public static void LogWarning(object msg)
        {
            Debug.LogWarning(msg.ToString());
        }
        public static void LogError(object msg)
        {
            Debug.LogError(msg.ToString());
        }

        /// <summary>
        /// 系统内置消息弹窗
        /// </summary>
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern int MessageBox(IntPtr hWnd, string text, string caption, uint type);
        public static void MessageBox(object msg)
        {
            MessageBox(IntPtr.Zero, msg.ToString(), "提示", 0);
        }

        /// <summary>
        /// 保留小数点后num位并向下取
        /// </summary>
        public static string FloatFormat(float v, int num = 2)
        {
            var power = Mathf.Pow(10, num);
            v = Mathf.Floor(v * power) / power;

            string format = "F" + num;
            return v.ToString(format);
        }

        private static System.Diagnostics.Stopwatch stopwatch;
        public static void StartTimer()
        {
            if (stopwatch == null)
            {
                stopwatch = new System.Diagnostics.Stopwatch();
            }

            stopwatch.Reset();
            stopwatch.Start();
        }

        public static string StopTimer(string message = "")
        {
            if (stopwatch == null)
            {
                return "Stopwatch not initialized.";
            }

            stopwatch.Stop();
            return $"{message} 耗时: {stopwatch.ElapsedMilliseconds} ms";
        }
    }
}