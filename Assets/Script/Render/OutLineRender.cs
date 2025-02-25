
using System;
using UnityEngine;
using UnityEngine.Profiling;

namespace Script.Render
{
    /// <summary>
    /// 后处理实现描边。需要主摄像机、outline摄像机。
    /// </summary>
    public class OutLineRender : PostEffectsBase
    {
        //渲染纹理
        private RenderTexture renderTexture = null;
        private Material _material = null;

        /// 主相机  
        public Camera mainCamera;
        /// 辅助摄像机  
        public Camera outlineCamera;
        // 纯色shader
        public Shader purecolorShader;
        //描边处理的shader
        public Shader shader;
        //迭代次数
        [Range(0, 4)]
        public int iterations = 3;
        //模糊扩散范围
        [Range(0.2f, 3.0f)]
        public float blurSpread = 0.6f;
        private int downSample = 1;
        public Color outlineColor = new Color(1, 1, 1, 1);

        public Material outlineMaterial
        {
            get
            {
                _material = CheckShaderAndCreateMaterial(shader, _material);
                return _material;
            }
        }

        void Awake()
        {
            CreatePureColorRenderTexture();
        }

        void Start()
        {
            SetOutlineCamera();
        }

        private void CreatePureColorRenderTexture()
        {
            outlineCamera.cullingMask = 1 << LayerMask.NameToLayer("Player");
            int width = outlineCamera.pixelWidth;
            int height = outlineCamera.pixelHeight;
            renderTexture = RenderTexture.GetTemporary(width, height, 0);
        }


        //渲染之前调用
        private void SetOutlineCamera()
        {
            if (!outlineCamera.enabled) return;
            outlineCamera.targetTexture = renderTexture;
            //掩码枚举
            outlineCamera.cullingMask = 1 << LayerMask.NameToLayer("Default");

        }
        //解释outlineCamera.RenderWithShader
        //假设脚本中调用 GetComponent<Camera>().RenderWithShader(Shader.Find("shaderX"), "")，则此摄像机本次渲染的所有物体都会使用shaderX进行渲染。
        //*假设脚中中调用 GetComponent<Camera>().RenderWithShader(Shader.Find("shaderX"), "myReplacementTag")，则对于本次要渲染的每个物体object(i)，假设object(i)本身的shader是shader(i)，如果shader(i)的所有subShader都不带"myReplacementTag"标签，则object(i)不渲染；如果shader(i)中的subShader(j)带有"myReplacementTag"标签，设此标签为"myReplacementTag"="A"，则unity会去shaderX中找"myReplacementTag"="A"的subShader，如果找到了，则用shaderX的此subShader替换object(i)的原有shader；否则object(i)不渲染。
        //需要指出的是，"myReplacementTag"应该总是用"RenderType",原因是unity内置的所有shader都带有RenderType标签。




        private void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            int rtW = source.width;
            int rtH = source.height;
            // var temp1 = RenderTexture.GetTemporary(rtW, rtH, 0);

            outlineMaterial.SetColor("_OutlineColor", outlineColor);
            outlineMaterial.SetFloat("_OutlineWidth", 0.002f);
            outlineMaterial.SetTexture("_SrcTex", renderTexture);
            Graphics.Blit(source, destination, outlineMaterial);
        }


        void Update()
        {
          
        }

       
    }
}