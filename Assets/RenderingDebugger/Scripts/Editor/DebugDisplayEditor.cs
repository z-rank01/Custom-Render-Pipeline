using UnityEditor;
using UnityEngine;

namespace RenderingDebugger.Scripts.Editor
{
    public class DebugDisplayEditor : EditorWindow
    {
        private void OnGUI()
        {
            GUILayout.Label("Rendering Debugger Settings", EditorStyles.boldLabel);
            GUILayout.Space(10);

            if (EditorGUILayout.DropdownButton(new GUIContent("Enable Debugging"),  FocusType.Keyboard))
            {
                Debug.Log("Debugging Enabled");
                
                // 
            }

            GUILayout.Space(20);
            GUILayout.Label("Settings", EditorStyles.label);
            // Add more settings controls here as needed
        }
        
        private void SetupDisplayRect(float displayHeightRatio)
        {
            // This method can be used to set up the display rectangle for the debug display.
            // For example, you can adjust the size and position of the debug display based on the screen resolution.
            Rect displayRect = new Rect(0, 0, Screen.width, Screen.height * displayHeightRatio);
            GUILayout.BeginArea(displayRect);
            GUILayout.Label("Debug Display Area", EditorStyles.boldLabel);
            GUILayout.EndArea();
        }
    }
}