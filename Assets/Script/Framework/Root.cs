using Script.Framework.AssetLoader;
using Script.Framework.UI;
using Script.Model.Auto;
using Script.UI.Panel.Auto;
using UnityEngine;

namespace Script.Framework
{
    [RequireComponent(typeof(GameTimer))]
    [RequireComponent(typeof(SceneTool))]
    public class Root : MonoBehaviour
    {
        protected static Root inst;
        public static Root Inst { get { return inst; } }
        
        [SerializeField] public Camera Camera;
        [SerializeField] public Canvas Canvas;

        protected void Awake()
        {
            inst = this;
            Camera = Camera.main;

            // 提前加载
            AssetManager.Inst.LoadAssetAsync<Shader>("Shader/OutlineEx", (s) => { }, this);
        }

        private float intervalFor1s = 1;   // 1s间隔实例 
        protected void Update()
        {
            float delta = Time.deltaTime;

            //1s的更新间隔
            intervalFor1s -= delta;
            if (intervalFor1s <= 0)
            {
                intervalFor1s = 1;
                (UIManager.Inst as UIManager).OnUpdate(1);
                ImageManager.Inst.OnUpdate(1);
            }

            AutoScriptManager.Inst.OnUpdate(delta);
            TemplateMatchDrawResultPanel.Inst?.OnUpdate();
        }


        void OnHeroBtnClick()
        {
            UIManager.Inst.ShowPanel(PanelEnum.HeroMainPanel, null);
        }
    }
}
