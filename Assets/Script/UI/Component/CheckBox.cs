
using UnityEngine;

namespace Script.UI.Component
{
    public class CheckBox : MonoBehaviour
    {
        [SerializeField] private GameObject[] TrueGos;
        [SerializeField] private GameObject[] FalseGos;

        private bool _value;
        public void SetData(bool value)
        {
            _value = value;

            foreach (var go in TrueGos)
            {
                if (go != null)
                    go.SetActive(value);
            }
            
            foreach (var go in FalseGos)
            {
                if (go != null)
                    go.SetActive(!value);
            }
        }
    }
}