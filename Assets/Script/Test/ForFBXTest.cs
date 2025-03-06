
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Script.Test
{
    public class ForFBXTest : MonoBehaviour
    {
        public InputField NumberInput;
        public Button Button;
        public Button Button1;
        public GameObject Prefab;
        public GameObject Prefab1;

        private List<GameObject> _gameObjects = new List<GameObject>();
        private List<GameObject> _gameObjects1 = new List<GameObject>();
        private void Awake()
        {
            Button.onClick.AddListener(OnButtonClick);
            Button1.onClick.AddListener(OnButtonClick1);
            Prefab.SetActive(false);
            Prefab1.SetActive(false);

            Application.targetFrameRate=60;
        }

        private void OnButtonClick()
        {
            foreach (var go in _gameObjects1)
            {
                go.SetActive(false);
            }
            var num = int.Parse(NumberInput.text);
            var need = num - _gameObjects.Count;
            if (need <= 0) return;

            for (int i = _gameObjects.Count; i < num; i++)
            {
                var go = Instantiate(Prefab, Prefab.transform.parent);
                _gameObjects.Add(go);
            }
            foreach (var go in _gameObjects)
            {
                go.SetActive(true);
            }
        }
        private void OnButtonClick1()
        {
            foreach (var go in _gameObjects)
            {
                go.SetActive(false);
            }
            var num = int.Parse(NumberInput.text);
            var need = num - _gameObjects1.Count;
            if (need <= 0) return;

            for (int i = _gameObjects1.Count; i < num; i++)
            {
                var go = Instantiate(Prefab1, Prefab1.transform.parent, false);
                _gameObjects1.Add(go);
            }

             foreach (var go in _gameObjects1)
            {
                go.SetActive(true);
            }
        }


    }
}