
using System.Collections.Generic;
using UnityEngine;

namespace Script.UI.Components
{
    public class MPBStart : MonoBehaviour
    {
        public List<MeshRenderer> list;
        // Start is called before the first frame update
        void Start()
        {
            // for (int i = 0; i < list.Count; i++)
            // {
            //     if (i % 2 == 0)
            //     {
            //         list[i].material.color = Color.blue;
            //     }
            //     else
            //     {
            //         list[i].material.color = Color.red;
            //     }
            // }


            MaterialPropertyBlock materialPropertyBlock = new MaterialPropertyBlock();
            for (int i = 0; i < list.Count; i++)
            {
                if (i % 2 == 0)
                {
                    list[i].GetPropertyBlock(materialPropertyBlock);
                    materialPropertyBlock.SetColor("_Color", Color.white);
                    list[i].SetPropertyBlock(materialPropertyBlock);
                    materialPropertyBlock.Clear();
                    
                }
                else
                {
                    list[i].GetPropertyBlock(materialPropertyBlock);
                    materialPropertyBlock.SetColor("_Color", Color.red);
                    list[i].SetPropertyBlock(materialPropertyBlock);
                    materialPropertyBlock.Clear();
                }
            }

        }
    }
}