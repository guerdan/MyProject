using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Root : MonoBehaviour
{
    public static Root inst;
    
    
    public Camera cam;

    // Start is called before the first frame update
    void Awake()
    {
        Root.inst = this;
    }

}
