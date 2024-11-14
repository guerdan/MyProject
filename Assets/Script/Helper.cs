using UnityEngine;

namespace Script
{
    public class Helper
    {
        public static void Log(string str)
        {
            var frame = Time.frameCount;
            Debug.Log($"{frame} {str}");
        }
        
        
        
    }

    
}