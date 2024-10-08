using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Sprites;
using UnityEngine.UI;

public class AStarPanel : MonoBehaviour, IPointerClickHandler
{
    public Button setPlayerBtn;
    public Button setDestinBtn;
    public Button setBarrierBtn;
    public Button clearAllBtn;
    public Button startBtn;


    private int[,] map;

    private List<GameObject> nodes = new List<GameObject>(); 
    // Start is called before the first frame update
    void Start()
    {
        setPlayerBtn.onClick.AddListener(onSetPlayerBtnClick);
        setDestinBtn.onClick.AddListener(onSetDestinBtnClick);
        setBarrierBtn.onClick.AddListener(onSetDestinBtnClick);
        clearAllBtn.onClick.AddListener(onClearAllBtnnClick);
        startBtn.onClick.AddListener(onStartBtnClick);

        for (int i = 0; i < transform.childCount; i++)
        {
            this.nodes.Add(transform.GetChild(i).gameObject);
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
    
    void onStartBtnClick()
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