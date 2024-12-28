
using Script.Framework.AssetLoader;
using Script.Framework.UI;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

namespace Script.UI.Test
{
    public class UITest : MonoBehaviour
    {
        void Update()
        {
            if (Input.GetKeyDown(KeyCode.A))
            {
                OpenOnePanel();
            }


            if (Input.GetKeyDown(KeyCode.S))
            {
            }

            if (Input.GetKeyDown(KeyCode.D))
            {
            }

            if (Input.GetKeyDown(KeyCode.Q))
            {
            }

            if (Input.GetKeyDown(KeyCode.R))
            {
                AssetManager.Inst.ReleaseUnuseAsset();
            }
            if (Input.GetKeyDown(KeyCode.G))
            {
            }


            if (Input.GetKeyDown(KeyCode.L))
            {
            }
            if (Input.GetKeyDown(KeyCode.O))
            {
            }

            if (Input.GetKeyDown(KeyCode.K))
            {
            }
        }


        void OpenOnePanel()
        {
            UIManager.Inst.ShowPanel(PanelEnum.HeroDetailPanel, null);
        }

    }
}

