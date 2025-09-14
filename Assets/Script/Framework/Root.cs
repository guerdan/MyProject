using Script.Framework;
using Script.Framework.AssetLoader;
using Script.Framework.UI;
using Script.Model.Auto;
using UnityEngine;
using UnityEngine.UI;

namespace Script.UI
{
    public class Root : MonoBehaviour
    {
        protected static Root inst;
        public static Root Inst { get { return inst; } }
        [HideInInspector] public Camera Camera;
        [SerializeField] public Canvas Canvas;

        protected void Awake()
        {
            inst = this;
            gameObject.AddComponent<GameTimer>();
            Camera = Camera.main;
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
        }


        void OnHeroBtnClick()
        {
            UIManager.Inst.ShowPanel(PanelEnum.HeroMainPanel, null);
        }
    }
}
