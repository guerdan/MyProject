
using System;
using UnityEngine;
using UnityEngine.Profiling;

namespace Script.Render.OutlineRender
{
    /// <summary>
    /// 后处理（深度纹理）实现描边。需要主摄像机、outline摄像机。
    /// </summary>
    public class OutLineRender : MonoBehaviour
    {

        public bool EnableOutline = true;

        /// 主相机  
        public Camera mainCamera;
        /// 辅助摄像机  
        public Camera outlineCamera;
        // 纯色shader
        public Shader purecolorShader;
        //描边处理的shader
        public Material material;
        private void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            if (!EnableOutline)
            {
                Graphics.Blit(source, destination);
                return;
            }
            material.SetTexture("_SrcTex", outlineCamera.targetTexture);
            Graphics.Blit(source, destination, material);
        }


        void Update()
        {

        }


    }
}