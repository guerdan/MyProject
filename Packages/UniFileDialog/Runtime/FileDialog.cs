using System;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using static zFramework.IO.WinAPI;
using System.Linq;

namespace zFramework.IO
{
    public static class FileDialog
    {
        static string Filter(params string[] filters)
        {
            return string.Join("\0", filters) + "\0";
        }
        /// <summary>
        /// 打开文件选择窗口
        /// </summary>
        /// <param name="title">指定窗口名称</param>
        /// <param name="extensions">指定文件选择类型，使用 | 分隔</param>
        /// <returns>选中的文件路径的列表</returns>
        /// https://learn.microsoft.com/zh-cn/windows/win32/api/commdlg/ns-commdlg-openfilenamea?redirectedfrom=MSDN
        /// https://www.cnblogs.com/zhaotianff/p/15720189.html

        public static List<string> SelectFile(string title, string initialPath, params string[] extensions)
        {
            initialPath = initialPath.Replace('/', '\\');

            var filters = new List<string>();
            foreach (var ext in extensions)
            {
                if (ext.Contains("|"))
                {
                    var name = ext.Split('|')[0];
                    var exts = ext.Split('|')[1];
                    filters.Add(name);
                    filters.Add("*" + exts);
                }
            }
            var filter = Filter(filters.ToArray());

            var chars = new char[1024];
            // var it = Path.GetFileName(path).GetEnumerator();
            // for (int i = 0; i < chars.Length && it.MoveNext(); ++i)
            // {
            //     chars[i] = it.Current;
            // }
            var file = new string(chars);
            var filePtr = Marshal.StringToHGlobalAuto(file);

            // int size = 1024;
            // List<string> list = new List<string>();
            // //多选文件是传出一个指针，这里需要提前分配空间
            // //如果是单选文件，使用已经分配大小的StringBuilder或string
            // IntPtr filePtr = Marshal.AllocHGlobal(size);

            // //清空分配的内存区域
            // for (int i = 0; i < size; i++)
            // {
            //     Marshal.WriteByte(filePtr, i, 0);
            // }

            OpenFileName ofn = new OpenFileName();
            ofn.lStructSize = Marshal.SizeOf(ofn);
            ofn.lpstrFilter = filter;
            ofn.nFilterIndex = 2;
            ofn.filePtr = filePtr;          // 同时选中多个文件，的缓冲区
            ofn.nMaxFile = 1024;            // 同时选中多个文件，这些文件名总的字符大小
            ofn.nMaxFileTitle = 256;        // 选中的单个文件的字符大小
            ofn.lpstrFileTitle = new string(new char[256]);  // 选中的单个文件的缓冲区

            // ofn.nMaxFile = size;
            // ofn.nMaxFileTitle = 256;
            ofn.lpstrInitialDir = initialPath + "\\";
            ofn.lpstrDefExt = "*.*";
            ofn.Flags = OFN_EXPLORER | OFN_FILEMUSTEXIST | OFN_PATHMUSTEXIST | OFN_ALLOWMULTISELECT | OFN_NOCHANGEDIR;
            ofn.hwndOwner = UnityHWnd; //这一步将文件选择窗口置顶。

            // if (GetOpenFileName(ofn))
            // {
            //     var file = Marshal.PtrToStringAuto(ofn.filePtr);
            //     while (!string.IsNullOrEmpty(file))
            //     {
            //         list.Add(file);
            //         //转换为地址
            //         long filePointer = (long)ofn.filePtr;
            //         //偏移
            //         filePointer += file.Length * Marshal.SystemDefaultCharSize + Marshal.SystemDefaultCharSize;
            //         ofn.filePtr = (IntPtr)filePointer;
            //         file = Marshal.PtrToStringAuto(ofn.filePtr);
            //     }
            // }

            if (!GetOpenFileName(ofn))
            {
                return null;
            }


            var saveto = Marshal.PtrToStringUni(ofn.filePtr);
            Marshal.FreeHGlobal(filePtr);
            return new List<string> { saveto };

            //第一条字符串为文件夹路径，需要再拼成完整的文件路径
            // if (list.Count > 1)
            // {
            //     for (int i = 1; i < list.Count; i++)
            //     {
            //         list[i] = System.IO.Path.Combine(list[0], list[i]);
            //     }

            //     list = list.Skip(1).ToList();
            // }

            // Marshal.FreeHGlobal(filePtr);
            // return list;
        }

        /// <summary>
        /// 保存文件选择窗口
        /// </summary>
        /// <param name="title">指定窗口名称</param>
        /// <param name="extensions">预设文件存储位置及文件名</param>
        /// <returns>文件路径</returns>
        public static string SaveDialog(string title, string path, string extension)
        {
            path = path.Replace('/', '\\');
            OpenFileName ofn = new OpenFileName();
            ofn.lStructSize = Marshal.SizeOf(ofn);

            var filters = new List<string>();
            if (extension.Contains("|"))
            {
                var name = extension.Split('|')[0];
                var exts = extension.Split('|')[1];
                filters.Add(name);
                filters.Add("*" + exts);
            }
            var filter = Filter(filters.ToArray());

            ofn.lpstrFilter = filter;
            var chars = new char[256];
            var it = Path.GetFileName(path).GetEnumerator();
            for (int i = 0; i < chars.Length && it.MoveNext(); ++i)
            {
                chars[i] = it.Current;
            }
            var file = new string(chars);
            var filePtr = Marshal.StringToHGlobalAuto(file);
            ofn.filePtr = filePtr;
            ofn.nMaxFile = file.Length;

            ofn.lpstrFileTitle = new string(new char[256]);
            ofn.nMaxFileTitle = 256;
            ofn.lpstrInitialDir = Path.GetDirectoryName(path) + "\\";
            // ofn.lpstrFileTitle = title;
            ofn.lpstrDefExt = "*.*";
            ofn.Flags = OFN_OVERWRITEPROMPT | OFN_HIDEREADONLY | OFN_NOCHANGEDIR;
            ofn.hwndOwner = UnityHWnd; //这一步将文件选择窗口置顶。

            if (!GetSaveFileName(ofn))
            {
                return null;
            }
            var saveto = Marshal.PtrToStringUni(ofn.filePtr);
            Marshal.FreeHGlobal(filePtr);
            return saveto;
        }
    }
}