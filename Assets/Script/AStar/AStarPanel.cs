using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Sprites;
using UnityEngine.UI;

public class AStarPanel : MonoBehaviour, IPointerClickHandler
{
    public Button startBtn;
    public Button setPlayerBtn;
    public Button setDestinBtn;
    public Button setBarrierBtn;
    public Button clearAllBtn;

    public GameObject UIPrefab;
    public GameObject UIParent;
    private List<GameObject> nodes = new List<GameObject>(); 
    // Start is called before the first frame update
    void Start()
    {
        setPlayerBtn.onClick.AddListener(onSetPlayerBtnClick);
        setDestinBtn.onClick.AddListener(onSetDestinBtnClick);
        setBarrierBtn.onClick.AddListener(onSetDestinBtnClick);
        clearAllBtn.onClick.AddListener(onClearAllBtnnClick);
        startBtn.onClick.AddListener(onStartBtnClick);

       
    }

    void onStartBtnClick()
    {
        foreach (var go in nodes)
        {
            Destroy(go);
        }
        nodes.Clear();
        int row = 12;
        int column = 17;
        int count = row * column;
        for (int i = 0; i < count; i++)
        {
            GameObject node = Instantiate(UIPrefab);
            node.transform.SetParent(UIParent.transform,false);
            nodes.Add(node);
        }
    }

    void onSetPlayerBtnClick()
    {
        
    }
    
    void onSetDestinBtnClick()
    {
        
    }
    
    void onSetBarrierBtnnClick()
    {
        
    }
    
    void onClearAllBtnnClick()
    {
        
    }
    
    

    public void OnPointerClick(PointerEventData eventData)
    {
        var pos = eventData.position;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            GetComponent<RectTransform>(),
            pos,
            Root.inst.cam,
            out Vector2 localPoint);
        
        Debug.LogWarning(localPoint);
    }
}