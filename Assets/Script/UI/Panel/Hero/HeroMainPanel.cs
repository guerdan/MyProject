
using Script.Framework.UI;
using Unity.VisualScripting;
using UnityEngine.UI;

namespace Script.UI.Panel.Hero
{
    public class HeroMainPanel : BasePanel
    {
        public Text Title;
        public Button DetailBtn;

        void OnEnable()
        {
            DetailBtn.onClick.AddListener(OnDetailBtnClick);
        }
        void OnDisable()
        {
            DetailBtn.onClick.RemoveAllListeners();
        }

        public override void SetData(object data)
        {
            Title.text = "英雄主界面";
        }

        void OnDetailBtnClick()
        {
            UIManager.Inst.ShowPanel(PanelEnum.HeroDetailPanel, null);
        }
    }
}