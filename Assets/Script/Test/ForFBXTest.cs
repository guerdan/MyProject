
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Script.Test
{
    public class ForFBXTest : MonoBehaviour
    {
        public InputField NumberInput;
        public Button Button;
        public GameObject Prefab;

        private List<GameObject> _gameObjects = new List<GameObject>();
        private void Awake()
        {
            Button.onClick.AddListener(OnButtonClick);
            Prefab.SetActive(false);
        }

        private void OnButtonClick()
        {
            var num = int.Parse(NumberInput.text);
            var need = num - _gameObjects.Count;
            if (need <= 0) return;

            StartCoroutine(CreateGameObject(need));
        }

        private IEnumerator CreateGameObject(int num)
        {
            for (int i = 0; i < num; i++)
            {
                var go = Instantiate(Prefab);
                _gameObjects.Add(go);
                go.SetActive(true);
            }
                yield return new WaitForSeconds(0.1f);
        }
    }
}