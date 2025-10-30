// SimpleTest.cs
// Just add this to ANY GameObject in your scene
// If you see red text, Unity IMGUI is working

using UnityEngine;

public class SimpleTest : MonoBehaviour
{
    void OnGUI()
    {
        GUI.color = Color.red;
        GUI.Label(new Rect(200, 200, 400, 100), "IF YOU CAN READ THIS, IMGUI WORKS!");
    }
}