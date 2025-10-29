
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

namespace Script.Util
{
    /// <summary>
    /// Debug-Util，本项目简称DU
    /// </summary>
    public static class DU
    {
        // 统计执行的平均时长。Dic<输出名, (次数, 总时长)>
        static Dictionary<string, (int, double)> _averageTimeDic = new Dictionary<string, (int, double)>();

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

        public static string StopTimer(string message = "", int logType = 0)
        {
            if (stopwatch == null)
            {
                return "Stopwatch not initialized.";
            }

            stopwatch.Stop();
            string str = $"{message} 耗时: {stopwatch.ElapsedMilliseconds} ms";
            if (logType == 1)
                Log(str);
            else if (logType == 2)
                LogWarning(str);
            else if (logType == 3)
                LogError(str);

            return str;
        }

        public static string RunWithTimer(Action action, string message = "", int logType = 1)
        {

            var pre = DateTime.Now;

            action.Invoke();

            var back = DateTime.Now;
            double ms = (back - pre).TotalMilliseconds;

            if (!_averageTimeDic.TryGetValue(message, out var tuple))
            {
                tuple = (0, 0);
            }
            tuple.Item1 += 1;
            tuple.Item2 += ms;
            _averageTimeDic[message] = tuple;
            double avg = tuple.Item2 / tuple.Item1;

            string str = $"{message} 耗时: {ms} ms，平均：{avg} ms";

            if (logType == 1)
                Log(str);
            else if (logType == 2)
                LogWarning(str);
            else if (logType == 3)
                LogError(str);
            return str;
        }

        public static double RunWithTimer(Action action)
        {
            var pre = DateTime.Now;

            action.Invoke();

            var back = DateTime.Now;
            double ms = (back - pre).TotalMilliseconds;
            return ms;
        }

        public static string GetListString(List<string> list)
        {
            var result = new StringBuilder();
            result.Append("[");
            for (int i = 0; i < list.Count; i++)
            {
                result.Append(list[i]);
                if (i < list.Count - 1)
                    result.Append(", ");
            }
            result.Append("]");
            return result.ToString();
        }
    }
}