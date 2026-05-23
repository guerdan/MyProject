
using System.IO;
using OfficeOpenXml;
using Script.Util;
using UnityEditor;
using System;
using System.Collections.Generic;
using Script.Framework.Else;
using Script.Model.Auto;
using Newtonsoft.Json;
using System.Linq;
using System.Drawing;
using OpenCvSharp;
using System.Drawing.Imaging;
using UnityEngine;

namespace Script.Editor
{

    /// <summary>
    /// 代表词条
    /// </summary>
    public class AffixEditorItem
    {
        public string Id;
        public string Str;
        public string Path;

        public Color32Image Img;

        public AffixEditorItem(string id, string str, string path)
        {
            Id = id;
            Str = str;
            Path = path;
        }
    }

    public class WordEditorItem
    {
        public int CombineCount = 1;
        public bool already_parse = false;

        public WordEditorItem()
        {
            CombineCount = 1;
            already_parse = false;
        }
    }



    public static class TextCfgGenrator
    {


        #region Gen Affix Img

        /// <summary>
        /// 词缀图有一行黑的，是有可能的。
        /// </summary>
        [MenuItem(Utils.CustomToolsPath + "Text/Affix Img")]
        public static void GenAffixImg()
        {
            // 遍历当前文件夹所有文件
            string[] files = Directory.GetFiles(@"D:\unityProject\MyProject_Resource\ItemAttr\样本\牌子_祭坛");
            foreach (string path in files)
            {

                List<Color32Image> list = null;
                using (Bitmap bitmap = new Bitmap(path))
                using (Bitmap bitmap1 = bitmap.Clone(new Rectangle(0, 0, bitmap.Width, bitmap.Height), PixelFormat.Format32bppArgb))
                {
                    IU.BitmapToMat(bitmap1, out Mat source, out Vec4b[] data);
                    var img = new Vec4bImage(data, bitmap1.Width, bitmap1.Height);
                    list = RecogUtil.ItemFloatParse(img);
                    source.Dispose();
                }

                string dir_path = @"D:\unityProject\MyProject_Resource\ItemAttr\template\牌子_祭坛";
                string file_name = Path.GetFileNameWithoutExtension(path);
                string[] sub_names = file_name.Split(",");

                for (var i = 0; i < list.Count; i++)
                {
                    if (i >= sub_names.Length) continue;
                    string sub = sub_names[i];
                    if (string.IsNullOrEmpty(sub)) continue;
                    RecogUtil.Save(list[i], $"{dir_path}/{sub}.png");
                }
            }

            DU.LogWarning("成功");
        }

        #endregion


        #region Text Json

        /// <summary>
        /// 文字图像放入到 15X14 的图片里。如果过小，就向左下对齐。
        /// 
        /// 性能：图像数据为二进制数据，最好用Base64编码成字符串，会增加33%的体积
        /// Base64	+33%	✅ 最小、最快、标准
        /// 十六进制 (hex)	+100%	体积 x2，太大
        /// 数字数组	+300%~500%	超大，巨浪费
        /// Unicode 字符串	不稳定	会出 \u0000 膨胀
        /// </summary>
        [MenuItem(Utils.CustomToolsPath + "Text/Text Json")]
        public static void GenAllTextJson()
        {
            // Save TextCfgs

            // Setting
            //
            var dirPath = @"D:\unityProject\MyProject_Resource\ItemAttr";
            var templateDirPath = @$"{dirPath}\template";
            var excelPath = $"{dirPath}/词.xlsx";

            // Variable
            //
            var charDic = new WordEditorItem[65536];
            var textDic = new Dictionary<string, WordImgCfg>();


            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            if (!File.Exists(excelPath))
            {
                throw new Exception("不存在excel文件");
            }


            AffixEditorItem[] sources;
            using (var package = new ExcelPackage(new FileInfo(excelPath)))
            {
                // 获取第一个工作表
                var worksheet = package.Workbook.Worksheets[0];

                // 获取行数和列数
                int rowCount = worksheet.Dimension.Rows;
                int colCount = worksheet.Dimension.Columns;

                sources = new AffixEditorItem[rowCount - 1];
                for (int row = 2; row <= rowCount; row++)
                {
                    var id = worksheet.Cells[row, 1].Text;
                    var str = worksheet.Cells[row, 2].Text;
                    var path = worksheet.Cells[row, 3].Text;
                    sources[row - 2] = new AffixEditorItem(id, str, path);
                }
            }

            // 要先解析 0,1,2,3,4,5,6,7,8,9，在后文"#"会排除掉数字。
            var numberDirPath = @$"{dirPath}\template\数字";
            for (int i = 0; i <= 10; i++)
            {
                var path = $"{numberDirPath}/{i}.png";
                if (!File.Exists(path))
                    continue;

                char c = default;
                if (i <= 9)
                    c = (char)('0' + i);
                else if (i == 10)
                    c = '.';

                var img = RecogUtil.GetImg(path);
                var data = RecogUtil.ImgToString(img);

                var cfg = new WordImgCfg
                {
                    Char = c,
                    Data = data,
                    CombineCount = 1
                };
                textDic.Add(data, cfg);

                charDic[c] = new WordEditorItem();
                charDic[c].CombineCount = 1;
                charDic[c].already_parse = true;
            }




            // 无论生成配置，还是解析，"引"都得当成2个字符看
            foreach (var item in sources)
            {
                var str = item.Str;
                for (int i = 0; i < str.Length; i++)
                {
                    var c = str[i];
                    byte combine_count = 1;

                    if (i < str.Length - 1 && str[i + 1] == '^')
                        combine_count = 2;

                    if (charDic[c] == null)
                    {
                        charDic[c] = new WordEditorItem();
                    }

                    charDic[c].CombineCount = Math.Max(charDic[c].CombineCount, combine_count);
                }
            }

            List<string> error_list = new List<string>();
            foreach (var item in sources)
            {
                var path = $"{templateDirPath}/{item.Path}.png";
                if (!File.Exists(path))
                {
                    continue;
                }
                var img = RecogUtil.GetImg(path);
                item.Img = img;
                var str = item.Str;
                List<string> dataList = null;
                try
                {
                    dataList = RecogUtil.SplitTextImg(img, "");
                }
                catch (Exception e)
                {
                    DU.LogError($"id={item.Id} SplitTextImg失败: {e}");
                    continue;
                }

                int cursor = 0;
                for (int i = 0; i < str.Length; i++)
                {
                    // 输入
                    char c = str[i];
                    if (c == '^' || c == '%' || c == '#')
                        continue;

                    // 输出
                    try
                    {
                        var char_parse = charDic[c];
                        int combine_count = char_parse.CombineCount;
                        string img_data = dataList[cursor];

                        if (!char_parse.already_parse)
                        {
                            char_parse.already_parse = true;

                            if (combine_count == 1)
                            {
                                var cfg = new WordImgCfg
                                {
                                    Char = c,
                                    Data = img_data,
                                    CombineCount = 1
                                };
                                textDic.Add(img_data, cfg);
                            }
                            else
                            {
                                // 开头的字符。 识别流程：先识别开头的字符，拿到CombineCount = 2后，
                                // 拼接前后两个字符的图像Data，得到Data1。用Data1去字典里查，得到最终字符。
                                var c0 = new WordImgCfg
                                {
                                    Char = default,
                                    Data = img_data,
                                    CombineCount = 2
                                };
                                textDic.Add(img_data, c0);

                                // 最终字符
                                var full_data = img_data + dataList[cursor + 1];
                                var c1 = new WordImgCfg
                                {
                                    Char = c,
                                    Data = full_data,
                                    CombineCount = 2
                                };
                                textDic.Add(full_data, c1);
                            }

                        }
                        cursor += combine_count;

                    }
                    catch (Exception e)
                    {
                        DU.LogError($"id={item.Id} char={c} 解析失败: {e}");
                    }
                }

                if (cursor != dataList.Count)
                {
                    error_list.Add(item.Id);
                }
            }


            // 保存"字符.json"
            var list = textDic.Values.ToList();
            list.Sort((a, b) => a.Char.CompareTo(b.Char));
            // foreach (var item in list)
            // {
            //     item.Serialize();
            // }
            var json = JsonConvert.SerializeObject(list);
            File.WriteAllText($"{dirPath}/字符.json", json);

            if (error_list.Count > 0)
            {
                DU.LogError($"以下id执行失败：{DU.GetListString(error_list)}");
                return;
            }

            AffixCfgManager.Inst.Init(true);
            var status1 = CheckAllAffix(sources);
            if (!status1)
                return;

            DU.LogWarning("执行完成");
        }

        public static bool CheckAllAffix(AffixEditorItem[] sources)
        {
            foreach (var item in sources)
            {
                if (item.Img.W == 0) continue;
                try
                {
                    var img = RecogUtil.GetImg(@$"D:\unityProject\MyProject_Resource\ItemAttr\template\{item.Path}.png");
                    var recog_id = RecogUtil.RecogAffix(img);
                    // var recog_id = RecogUtil.RecogAffix(item.Img);
                    if (recog_id != item.Id)
                    {
                        DU.LogError($"id={item.Id} 结果错误，错误={recog_id}");
                        return false;
                    }
                }
                catch (Exception e)
                {
                    DU.LogError($"id={item.Id} 报错: {e}");
                    return false;
                }
            }
            return true;
        }
        #endregion


        #region Gen Game Item

        /// <summary>
        /// 自动排除空东西
        /// </summary>
        [MenuItem(Utils.CustomToolsPath + "GameItem/Gen Img")]
        public static void GenGameItemImg()
        {
            var dirPath = @"D:\unityProject\MyProject_Resource\物品格";
            var templateDirPath = @$"{dirPath}\item";


            var source_path = @$"{dirPath}\物品id_背包.png";
            var img = RecogUtil.GetImg(source_path);

            var regions = RecogUtil.GetRegions(ItemGridPosType.Bag, new Vector2(1, 1));

            for (var i = 0; i < regions.Length; i++)
            {
                var region = regions[i];
                int w = (int)region.z, h = (int)region.w;
                int xs = (int)region.x, ys = (int)region.y;

                Color32[] colors = new Color32[w * h];
                Color32Image item_img = new Color32Image(colors, w, h);
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        colors[y * w + x] = img.Colors[(y + ys) * img.W + x + xs];
                    }

                var aver = RecogUtil.GetAverageColor(item_img, new CVRect(15, 15, 10, 10));
                if (aver.r <= 10 && aver.g <= 10 && aver.b <= 10)
                    continue;

                var path = $"{templateDirPath}/{i}.png";
                RecogUtil.Save(item_img, path);
            }

            DU.LogWarning("执行完成");
        }

        #endregion
    }
}