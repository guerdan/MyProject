using Script.Framework;
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
        [HideInInspector] public Camera cam;

        protected void Awake()
        {
            inst = this;
            gameObject.AddComponent<GameTimer>();
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
            }

            ProcessNodeManager.Inst.OnUpdate(delta);
        }


        void OnHeroBtnClick()
        {
            UIManager.Inst.ShowPanel(PanelEnum.HeroMainPanel, null);
        }
    }
}
