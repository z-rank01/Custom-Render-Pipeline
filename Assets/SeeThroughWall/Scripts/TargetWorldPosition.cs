using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TargetWorldPosition : MonoBehaviour
{
    public GameObject receiveObject;
    
    // Update is called once per frame
    void Update()
    {
        var meshRenderer = receiveObject.GetComponent<MeshRenderer>();
        if (!meshRenderer) return;
        meshRenderer.material.SetVector("_TargetWorldPosition", transform.position);
    }
}
