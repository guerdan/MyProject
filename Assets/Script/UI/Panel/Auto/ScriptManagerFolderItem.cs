
using System;
using Script.UI.Component;
using UnityEngine;
using UnityEngine.UI;

namespace Script.UI.Panel.Auto
{
    public class ScriptManagerFolderItem : MonoBehaviour
    {
        [SerializeField] private Text FloderName;
        [SerializeField] private CheckBox Btn;

        public void SetData(string text, Action onClick)
        {
           FloderName.text = text;
           Btn.SetData(false, (v) => onClick?.Invoke());
        }
    }
}