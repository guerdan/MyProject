
using Script.Framework.UI;
using Script.Model.Auto;
using Script.UI.Component;
using UnityEngine;
using UnityEngine.UI;

namespace Script.UI.Panel.Auto
{
    public class ScriptManagerScriptItem : MonoBehaviour
    {

        [SerializeField] private Button Btn;
        [SerializeField] private Text ScriptName;
        [SerializeField] private CheckBox RunBtn;
        [SerializeField] private CheckBox CollectBtn;
        string _id;
        AutoScriptData _scriptData;
        ScriptManagerPanel _panel;
        bool _is_collection = false;

        void Awake()
        {
            Btn.onClick.AddListener(OnBtnClick);
        }
        void OnEnable()
        {
            ScriptManagerPanel.OnRefresh += Refresh;
        }
        void OnDisable()
        {
            ScriptManagerPanel.OnRefresh -= Refresh;
        }

        public void SetData(string id, ScriptManagerPanel panel, bool is_collection = false)
        {
            _id = id;
            _scriptData = AutoScriptManager.Inst.GetScriptData(id);
            _panel = panel;
            _is_collection = is_collection;

            // 收藏列表里就不显示收藏按钮了
            //
            if (_is_collection)
            {
                CollectBtn.gameObject.SetActive(false);
                RunBtn.GetComponent<RectTransform>().anchoredPosition
                    = CollectBtn.GetComponent<RectTransform>().anchoredPosition;
            }

            Refresh(_id);
        }

        void Refresh(string id)
        {
            if (id != _id)
                return;

            ScriptName.text = _scriptData.Config.Name;
            RunBtn.SetData(_scriptData.Running, OnRunBtnClick);

            bool contain = AutoScriptManager.Inst.Settings.Collections.Contains(_id);
            CollectBtn.SetData(contain, OnCollectBtnClick);


        }

        void OnRunBtnClick(bool value)
        {
            if (value)
                AutoScriptManager.Inst.StopScript(_id);
            else
                AutoScriptManager.Inst.StartScript(_id);

            ScriptManagerPanel.OnRefresh?.Invoke(_id);
        }

        void OnCollectBtnClick(bool value)
        {
            if (value)
                AutoScriptManager.Inst.Settings.Collections.Insert(0, _id);
            else
                AutoScriptManager.Inst.Settings.Collections.Remove(_id);

            ScriptManagerPanel.OnRefresh?.Invoke(_id);
            _panel.RefreshCollectionList();
        }

        void OnBtnClick()
        {
            UIManager.Inst.ShowPanel(PanelEnum.DrawProcessPanel, _id);
        }
    }
}