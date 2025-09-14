
using System.Collections.Generic;
using Script.Framework.UI;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

namespace Script.UI.Panel.Auto
{
    public class LogPrintItem : MonoBehaviour
    {
        [SerializeField] private Button btn;
        [SerializeField] private Text textComp;
        void Awake()
        {
        }

        void OnEnable()
        {
            btn.onClick.AddListener(OnClick);
        }

        void OnDisable()
        {
            btn.onClick.RemoveAllListeners();
        }


        public void SetData(string text, float height)
        {
            var ui = GetComponent<RectTransform>();
            ui.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
            var textComp = GetComponentInChildren<Text>();
            textComp.text = text;
        }

        void OnClick()
        {   
            // 原图 是1
            // var data = new List<string>(){"MatchInput/folder.png","MatchTemplate/folder.png"};
            // 抠图 未用掩码是0.97   用掩码到1
        
        }
    }


}