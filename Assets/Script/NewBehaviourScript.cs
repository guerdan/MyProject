using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NewBehaviourScript : MonoBehaviour
{
    public GameObject cube;
    void Start()
    {
        
    }

    void Update()
    {
        
        if (Time.frameCount == 2)
        {
            Destroy(cube);
        }
        
        Debug.LogError(cube);
    }
}
