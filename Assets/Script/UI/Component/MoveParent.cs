using UnityEngine;
using UnityEngine.UI;

namespace Script.UI.Component
{
    public class MoveParent : MonoBehaviour
    {
        public Button btn;

       
        void Start()
        {
            btn.onClick.AddListener(onBtnClick);

        }

        void Update()
        {


        }

        void onBtnClick()
        {
            var child1 = transform.GetChild(0).gameObject;
            Destroy(child1);
        }
    }
}