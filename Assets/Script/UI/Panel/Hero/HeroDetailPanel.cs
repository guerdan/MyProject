
using Script.Framework.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Script.UI.Panel.Hero
{
    public class HeroDetailPanel : BasePanel
    {
        public Text Title;
        public Image image;

        public override void SetData(object data){
            Title.text = "英雄详情";
        }
    
    }
}