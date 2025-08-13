using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
	public static GameManager Inst;
    void Awake()
    {
        Inst = this;
    }
    void Update()
	{
		if (Input.GetKeyDown(KeyCode.Escape))
		{
			Quit();
		}
	}

	public void Quit()
	{
		Application.Quit();
	}
	public void Minimize()
	{
		TransparentWindow.Main.MinimizeWindow();
	}
	public void Show()
	{
		TransparentWindow.Main.ShowWindow();
	}
}
