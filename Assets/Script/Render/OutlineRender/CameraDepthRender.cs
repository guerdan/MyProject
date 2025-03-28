
using System;
using UnityEngine;
using UnityEngine.Profiling;

namespace Script.Render.OutlineRender
{
    /// <summary>
    /// 摄像机绘制深度纹理
    /// </summary>
    public class CameraDepthRender : MonoBehaviour
    {
        //渲染纹理
        public Material mat;
        private Camera cam;
        public RenderTexture depthTexture = null;
        private void Start()
        {
            cam = gameObject.GetComponent<Camera>();
            cam.cullingMask = 1 << LayerMask.NameToLayer("Default");//防止被重置
            cam.depthTextureMode = DepthTextureMode.Depth;

            depthTexture = RenderTexture.GetTemporary(Screen.width, Screen.height, 0);
            cam.targetTexture = depthTexture;
        }
        private void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            Graphics.Blit(null, destination, mat);
        }

    }
}