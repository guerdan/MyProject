
using System.Drawing;
using System.Drawing.Imaging;
using OpenCvSharp;
using UnityEngine;




namespace Script.UI.Panel.Auto
{
    public class OCVExample
    {
        public static OCVExample _inst;
        public static OCVExample Inst
        {
            get
            {
                if (_inst == null)
                    _inst = new OCVExample();
                return _inst;
            }
        }

        public void Init()
        {
            return;


            string path = Application.streamingAssetsPath + "/pic_chuan.png";
            Mat image = Cv2.ImRead(path);
            // 置灰
            // Mat gray = new Mat();
            // Cv2.CvtColor(image, gray, ColorConversionCodes.BGR2GRAY);
            // Cv2.ImShow("gray", gray);

            // 模糊
            Mat blur = new Mat();
            // 若size有值则会内部推导sigma不读传参，若size为0则从sigma参数中推导size。
            Cv2.GaussianBlur(image, blur, new OpenCvSharp.Size(), 15);

            Cv2.ImShow("高斯模糊", blur);
            Cv2.ImShow("image", image);
            Cv2.WaitKey(0);
            Cv2.DestroyAllWindows();
            image.Dispose();
        }




        public static void ShowMat(Mat mat, string windowName = "Image")
        {
            Cv2.ImShow(windowName, mat);
            // Cv2.WaitKey(0); // 等待按键
            // Cv2.DestroyAllWindows();
        }


        // Bitmap 转 Texture2D
        public static Texture2D BitmapToTexture2D(Bitmap bitmap)
        {
            // 创建相同尺寸的Texture2D
            Texture2D texture = new Texture2D(bitmap.Width, bitmap.Height, TextureFormat.RGBA32, false);

            // 锁定位图数据
            BitmapData bitmapData = bitmap.LockBits(
                new Rectangle(0, 0, bitmap.Width, bitmap.Height),
                ImageLockMode.ReadOnly,
                System.Drawing.Imaging.PixelFormat.Format32bppArgb);

            try
            {
                // 复制像素数据到Texture2D
                byte[] pixelData = new byte[bitmapData.Stride * bitmapData.Height];
                System.Runtime.InteropServices.Marshal.Copy(bitmapData.Scan0, pixelData, 0, pixelData.Length);
                texture.LoadRawTextureData(pixelData);
                texture.Apply();
            }
            finally
            {
                // 解锁Bitmap
                bitmap.UnlockBits(bitmapData);
            }

            return texture;
        }

        // Texture2D 转 Mat
        public static Mat Texture2DToMat(Texture2D texture)
        {
            // 创建与Texture2D匹配的Mat
            Mat mat = new Mat(texture.height, texture.width, MatType.CV_8UC4);

            // 获取Texture2D的像素数据
            Color32[] colors = texture.GetPixels32();

            // 转换为OpenCV的BGR格式
            Vec4b[] bytes = new Vec4b[colors.Length];
            for (int i = 0; i < colors.Length; i++)
            {
                bytes[i] = new Vec4b(
                    colors[i].b, // Blue
                    colors[i].g, // Green
                    colors[i].r, // Red
                    colors[i].a  // Alpha
                );
            }

            // 设置Mat数据
            mat.SetArray(bytes);

            return mat;
        }

        // 组合方法：Bitmap 直接转 Mat
        public static Mat BitmapToMat(Bitmap bitmap)
        {
            Texture2D texture = BitmapToTexture2D(bitmap);
            Mat mat = Texture2DToMat(texture);

            // 释放临时Texture2D
            UnityEngine.Object.Destroy(texture);

            return mat;
        }
    }
}