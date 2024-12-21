using DG.Tweening;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.UI;

namespace Script.Tweener
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