
using System.IO;

namespace Script.Util
{
    public static class PathUtil
    {
        public static string SquareFrameUIPath = "Common/Prefabs/Component/SquareFrameUI";



        /// <summary>
        /// 拷贝整个文件夹及其内容
        /// </summary>
        /// <param name="sourceDir">源文件夹路径</param>
        /// <param name="destinationDir">目标文件夹路径</param>
        /// <param name="overwrite">是否覆盖已存在的文件</param>
        public static void CopyDirectory(string sourceDir, string destinationDir, bool overwrite = true)
        {
            // 检查源文件夹是否存在
            if (!Directory.Exists(sourceDir))
            {
                throw new DirectoryNotFoundException($"源文件夹不存在: {sourceDir}");
            }

            // 如果目标文件夹不存在，则创建
            if (!Directory.Exists(destinationDir))
            {
                Directory.CreateDirectory(destinationDir);
            }

            // 拷贝文件
            foreach (var file in Directory.GetFiles(sourceDir))
            {
                string destFile = Path.Combine(destinationDir, Path.GetFileName(file));
                File.Copy(file, destFile, overwrite);
            }

            // 递归拷贝子文件夹
            foreach (var subDir in Directory.GetDirectories(sourceDir))
            {
                string destSubDir = Path.Combine(destinationDir, Path.GetFileName(subDir));
                CopyDirectory(subDir, destSubDir, overwrite);
            }
        }


        public static void DeleteDirectory(string dir)
        {
            if (Directory.Exists(dir))
            {
                // 删除文件夹及其所有内容
                Directory.Delete(dir, true);
            }
        }

    }
}