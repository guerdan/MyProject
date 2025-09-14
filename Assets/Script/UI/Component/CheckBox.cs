
using System;
using UnityEngine;
using UnityEngine.UI;

namespace Script.UI.Component
{
    public class CheckBox : MonoBehaviour
    {
        [SerializeField] private GameObject[] TrueGos;
        [SerializeField] private GameObject[] FalseGos;

        private bool _value;
        private Action<bool> _clickFunc;
        private Button _button;

        void Awake()
        {
            _button = GetComponent<Button>();

            if (_button) _button.onClick.AddListener(OnClick);
        }

        public void SetData(bool value, Action<bool> clickFunc = null)
        {
            _value = value;
            _clickFunc = clickFunc;

            Refresh();
        }

        void Refresh()
        {
            foreach (var go in TrueGos)
            {
                if (go != null)
                    go.SetActive(_value);
            }

            foreach (var go in FalseGos)
            {
                if (go != null)
                    go.SetActive(!_value);
            }
        }

        void OnClick()
        {
            _value = !_value;
            Refresh();
            _clickFunc?.Invoke(_value);
        }
    }
}