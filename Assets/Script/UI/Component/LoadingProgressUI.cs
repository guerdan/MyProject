

using System.Collections;
using UnityEngine;

namespace Script.UI.Component
{
    public class LoadingProgressUI : MonoBehaviour
    {

        [SerializeField] public GameObject BlockNode;
        [SerializeField] public GameObject ImageNode;

        private Coroutine showImageCor;

        void Awake()
        {
            Hide();
        }

        public void Show()
        {
            BlockNode.SetActive(true);
            showImageCor = StartCoroutine(ShowImage());
        }
        public void Hide()
        {
            BlockNode.SetActive(false);
            ImageNode.SetActive(false);
            if (showImageCor != null)
            {
                StopCoroutine(showImageCor);
                showImageCor = null;
            }
        }


        private IEnumerator ShowImage()
        {
            yield return new WaitForSeconds(0.2f);
            ImageNode.SetActive(true);
        }
    }
}