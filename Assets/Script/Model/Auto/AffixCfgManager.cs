
using System.Collections.Generic;
using Script.Framework.Else;
using OfficeOpenXml;
using System;
using Newtonsoft.Json;
using System.IO;
using Script.Util;


namespace Script.Model.Auto
{
    /// <summary>
    /// 组合所有的文字，并排除变量。去字典里查字符串，得到词缀id。
    /// </summary>
    public class AffixCfgManager
    {
        private static AffixCfgManager _inst;
        public static AffixCfgManager Inst
        { get { if (_inst == null) _inst = new AffixCfgManager(); return _inst; } }

        // 用来匹配的
        public Dictionary<string, WordImgCfg> WordDic;

        // 用来匹配的
        public Dictionary<string, AffixCfg> AffixContentDic;
        // id查询
        public Dictionary<string, AffixCfg> AffixIdDic;


        private bool init = false;

        public void Init(bool force = false)
        {
            if (!force)
            {
                if (init)
                    return;
                init = true;
            }

            // Init TextCfgs
            WordDic = new Dictionary<string, WordImgCfg>();
            var dirPath = @"D:\unityProject\MyProject_Resource\ItemAttr";
            var wordPath = $"{dirPath}/字符.json";
            if (!File.Exists(wordPath))
                throw new Exception($"word文件不存在");

            string json = File.ReadAllText(wordPath);
            List<WordImgCfg> list = JsonConvert.DeserializeObject<List<WordImgCfg>>(json);


            foreach (var item in list)
            {
                WordDic.Add(item.Data, item);
            }


            var excelPath = $"{dirPath}/词.xlsx";
            if (!File.Exists(excelPath))
                throw new Exception("不存在excel文件");

            AffixIdDic = new Dictionary<string, AffixCfg>();
            AffixContentDic = new Dictionary<string, AffixCfg>();
            AffixCfg[] AffixList = null;

            // 因为是冷启动，所以第一次加载很慢。第一次额外增加400ms
            // DU.RunWithTimer(() =>
            // {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            using (var package = new ExcelPackage(new FileInfo(excelPath)))
            {
                // 获取第一个工作表
                var worksheet = package.Workbook.Worksheets[0];

                // 获取行数和列数
                int rowCount = worksheet.Dimension.Rows;
                int colCount = worksheet.Dimension.Columns;

                AffixList = new AffixCfg[rowCount - 1];
                for (int row = 2; row <= rowCount; row++)
                {
                    var id = worksheet.Cells[row, 1].Text;
                    var content = worksheet.Cells[row, 2].Text;
                    var cfg = new AffixCfg(id, content);
                    AffixList[row - 2] = cfg;

                }
            }
            // }, "ExcelPackage");

            MakeValidContent(AffixList);
            foreach (var affix in AffixList)
            {
                AffixIdDic[affix.Id] = affix;
                AffixContentDic[affix.Content] = affix;
            }

        }

        public WordImgCfg GetWordImgCfg(string data)
        {
            if (WordDic.TryGetValue(data, out var result))
            {
                return result;
            }
            return null;
        }
        public AffixCfg GetAffixCfgByContent(string data)
        {
            if (AffixContentDic.TryGetValue(data, out var result))
            {
                return result;
            }
            return null;
        }
        public AffixCfg GetAffixCfgById(string data)
        {
            if (AffixIdDic.TryGetValue(data, out var result))
            {
                return result;
            }
            return null;
        }

        public void MakeValidContent(AffixCfg[] AffixList)
        {
            var blackList = new bool[65536];
            char[] delChars = { '^', '#', '%' };
            foreach (var c in delChars)
                blackList[c] = true;

            foreach (var affix in AffixList)
            {
                var content = affix.Content;
                // Span<char> 是在栈上分配内存，不是堆！
                Span<char> span = stackalloc char[content.Length];

                int idx = 0;
                foreach (char c in content)
                {
                    if (!blackList[c])
                        span[idx++] = c;
                }

                affix.Content = new string(span.Slice(0, idx));
            }

        }
    }

    /// <summary>
    /// 要序列化的。
    /// 图像数据Data作为Id。char做不了id，因为"引"会被拆为两部分，这两部分也是用此结构
    /// </summary>
    [Serializable]
    public class WordImgCfg
    {
        [JsonProperty("data")]
        public string Data;
        [JsonProperty("char")]
        public char Char;                   // 图片所表示的文字

        [JsonProperty("c_c")]
        public int CombineCount;            // 组合数，> 1就代表要与后续字符组合。
                                            // "引"字分割成两个字符“弓”、“|”。它们都有一份TextImgCfg


        public override string ToString()
        {
            return $"TextImgCfg:{Char}";
        }
    }



    [Serializable]
    public struct TextImgCfgCombineResult
    {
        [JsonProperty("c")]
        public int CombineId;               // 与#组合
        [JsonProperty("r")]
        public int CombineResultId;         // 组合结果
    }


    public class AffixCfg
    {
        public string Id;
        public string Content;

        public AffixCfg(string id, string content)
        {
            Id = id;
            Content = content;
        }


    }

}