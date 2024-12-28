using Script.Framework;
using Script.Framework.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Script.UI
{
    public class Root : MonoBehaviour
    {
        public static Root inst;
        public Camera cam;

        public Button HeroBtn;

        void Awake()
        {
            Root.inst = this;
            gameObject.AddComponent<GameTimer>();
        }

        void OnEnable()
        {
            HeroBtn.onClick.AddListener(OnHeroBtnClick);
        }
        void OnDisable()
        {
            HeroBtn.onClick.RemoveAllListeners();
        }

        private float intervalFor1s = 1;   // 1s间隔实例 
        void Update()
        {
            float delta = Time.deltaTime;

            //1s的更新间隔
            intervalFor1s -= delta;
            if (intervalFor1s <= 0)
            {
                intervalFor1s = 1;
                (UIManager.Inst as UIManager).OnUpdate(1);
            }
        }


        void OnHeroBtnClick()
        {
            UIManager.Inst.ShowPanel(PanelEnum.HeroMainPanel, null);
        }
    }
}
