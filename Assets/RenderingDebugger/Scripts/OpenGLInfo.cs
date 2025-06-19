using UnityEngine;

namespace RenderingDebugger.Scripts
{
    public class OpenGLInfo : MonoBehaviour
    {
        void Start()
        {
            string deviceVersion = SystemInfo.graphicsDeviceVersion;
            Debug.Log("Graphics Device Version: " + deviceVersion);

            //Example: If the device supports OpenGL ES 3.0, the string might contain "OpenGL ES 3.0"
            if (deviceVersion.Contains("OpenGL ES 3.0"))
            {
                Debug.Log("OpenGL ES 3.0 is supported.");
            }
            else if (deviceVersion.Contains("OpenGL ES 2.0"))
            {
                Debug.Log("OpenGL ES 2.0 is supported.");
            }
        }
    }
}