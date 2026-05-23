
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Security.Policy;
using System.Text;
using Newtonsoft.Json;

namespace Script.Util
{
    /// <summary>
    /// 全称 StringUtil
    /// 
    /// 应该弄一个I18N配置表，打包的时候自动拷贝一个excel表到包里，并且转为加密字符。
    /// </summary>
    public class SU
    {
        public static string Key = "rank_no1_bg01";
        public static long[] fibo;

        public static void InitFibo()
        {
            fibo = new long[68];
            fibo[0] = 1;
            fibo[1] = 1;
            for (int i = 2; i < 68; i++)
            {
                fibo[i] = fibo[i - 1] + fibo[i - 2];
            }
        }

        public static byte[] Encryption(byte[] data, string key)
        {
            if (fibo == null)
                InitFibo();

            int sum = 0;
            for (int i = 0; i < key.Length; i++)
            {
                sum += key[i];
            }

            int keyLen = key.Length;
            int N = Math.Max(sum % 64, 32);
            byte[] key2 = new byte[N];
            for (int i = 0; i < N; i++)
            {
                key2[i] = (byte)((key[(int)(fibo[i + 2] % keyLen)] + fibo[i + 4]) & 0xFF);
            }

            byte[] data_bytes = new byte[data.Length];
            for (int i = 0; i < data_bytes.Length; i++)
            {
                data_bytes[i] = (byte)(data[i] ^ key2[i % N]);
            }

            // 问题就是怎么存 bytes[],
            return data_bytes;
        }

        public static string GetString(string data)
        {
            byte[] bs = null;
            try
            {
                bs = Encryption(Convert.FromBase64String(data), Key);
            }
            catch (Exception e)
            {
                return "";
            }
            var str = Encoding.UTF8.GetString(bs);

            return str;
        }

        public static string SaveString(string text)
        {
            var bs = Encryption(Encoding.UTF8.GetBytes(text), Key);
            var str = Convert.ToBase64String(bs);

            return str;
        }

        public static string ErrorCode(int code)
        {
            return $"error code {code}: ";
        }


        /// <summary>
        /// 需做成菜单栏。
        /// 打印常量字符串的加密。
        /// </summary>
        public static void Print()
        {
            // return;

            HashSet<string> set = new HashSet<string>()
            {
            };

            Dictionary<string, string> dic = new Dictionary<string, string>();
            foreach (var item in set)
            {
                var save_str = SaveString(item);
                dic.Add(item, save_str);
            }

            string json = JsonConvert.SerializeObject(dic, Formatting.Indented);
            DU.Log(json);
        }



        public static string JieTu = "lfvRkQ8o";
        public static string MoBanPiPei = "ldvakx0eXXegMxbA";
        public static string ShuBiao = "ms/bkjQR";
        public static string JianPan = "mufVkw8O";
        public static string FuZhi = "m8bwkRQq";
        public static string TiaoJian = "le7akC8g";
        public static string TiaoJianPanDuan = "le7akC8gXXO9PAXg";
        public static string XunHuan = "ls3Rkxo5";
        public static string DengDai = "lN7ykSoT";
        public static string ChuFaShiJian = "m9TdkRsHXEGSPij7";
        public static string JianTingShiJian = "lOjqkQQ6XEGSPij7";
        public static string ZanTingJiaoBen = "len5kRUKUH+DPA/h";
        public static string DiTuShiBie = "lu/LkQ8oUFSfPxvm";
        public static string DiTuXunLu = "lu/LkQ8oXVSiMiTi";
        public static string WuPingGeShiBie = "lPrSkQcXXlulMjzLi8Eu";
        public static string QiaoJian = "lebJnQA4";
        public static string ZhiAnXia = "lvzRkhgfXEOS";
        public static string ZhiTaiQi = "lvzRkh46UE6u";
        public static string ZuoJian = "lsTdnQA4";
        public static string YouJian = "lvzInQA4";
        public static string YiDong = "lNTAkR4+";
        public static string Wu = "leTb";
        public static string TanSuoMiWu = "lf3ZkyA0UESuMwjz";
        public static string GenSuiMuBiao = "m8TknQ4ZX2C3PDPK";
        public static string QuWangMuDiDi = "lv3AkSoWX2C3PQnJi9U1";
        public static string SouXunMuBiao = "lePnkTstX2C3PDPK";
        public static string XiaoMieQuanBuMuBiao = "lcXzkxU7XX6xMxDlidIrMoEa";




        #region GameItemCfg
        public static string ShenShengShi = "lNblkQg1X2Sq";
        public static string HunDunShi1 = "lcTMkiYaX2Sq";
        public static string HunDunShi2 = "lcTMkiYaX2Sq6A==";
        public static string HunDunShi3 = "lcTMkiYaX2Sq6Q==";
        public static string CongGaoShi1 = "lsf8nT8OX2Sq";
        public static string CongGaoShi2 = "lsf8nT8OX2Sq6A==";
        public static string CongGaoShi3 = "lsf8nT8OX2Sq6Q==";
        public static string WuXiaoShi = "leTbkgEeX2Sq";
        public static string WaErShi = "lODdkSQCX2Sq";
        public static string FuHaoShi = "ltz3nCU8X2Sq";
        public static string ZengFuShi = "ltHlkS0TX2Sq";
        public static string TuiBianShi = "m+/ukRsOX2Sq";
        public static string DiTu14 = "lu/LkQ8oic8=";
        public static string DiTu15 = "lu/LkQ8oic4=";


        #endregion
        public static string JiaoBenMing = "m/fhkgg6XWuU";
        public static string ChuangJianJiaoBen = "lvvgkS8sUH+DPA/h";
        public static string ChongMinMingJiaoBen = "mvT2kQUrXWuUMhfXiNUp";


    }
}