#computeBuffer #buffer
### Buffer

example:

```c#
void OnEnable()
{
	// 3 * 4 = float3 * 4 bytes
	positionBuffer = new ComputeBuffer(maxResolution * maxResolution, 3 * 4);
}
    
    // Release buffer, best to manully release for avoiding clogging memory.
void OnDisable()
{
	positionBuffer.Release();
	positionBuffer = null;
}
```


### Property

example:

```c#
    static readonly int positionsId = Shader.PropertyToID("_Positions"),
                        resolutionXId = Shader.PropertyToID("_ResolutionX"),
                        resolutionZId = Shader.PropertyToID("_ResolutionZ"),
                        stepId = Shader.PropertyToID("_Step"),
                        timeId = Shader.PropertyToID("_Time"),
                        transitionProgressId = Shader.PropertyToID("_TransitionProgress");
```


### Set: bind buffers and properties to compute shader

example:

```c#
	float step = 2f / resolutionX;
	computeShader.SetInt(resolutionXId, resolutionX);
	computeShader.SetInt(resolutionZId, resolutionZ);
	computeShader.SetFloat(stepId, step);
	computeShader.SetFloat(timeId, Time.time);
	if (transitioning) 
	{
		computeShader.SetFloat(
			transitionProgressId,
			Mathf.SmoothStep(0f, 1f, duration / transitionDuration)
		);
	}
	// first parameter: int kernelIndex, in this case we have single kernel.
	// In order to select correct kernel functions,
	// current function index + current / next  function * 5
	var kernelIndex = (int)functions + (int)(transitioning ? fromFunction : toFunction) * FunctionCount;
	computeShader.SetBuffer(kernelIndex, positionsId, positionBuffer);
```


### Dispatch: run compute shader's kernel
#dispatch #computeShader

example:

```c#
	// groups
	int groupsX = Mathf.CeilToInt(resolutionX / 8f), groupsY = Mathf.CeilToInt(resolutionZ / 8f);
	// kernel indcies, already made above
	computeShader.Dispatch(kernelIndex, groupsX, groupsY, 1);
```

Compute Shader: [[Compute Shader]]

### Others: draw and material buffer

To use results of compute shader, we have to bind buffer's data (results) to the material for shader to use.

example:

```c#
	material.SetBuffer(positionsId, positionBuffer);
	material.SetFloat(stepId, step);

	var bounds = new Bounds(Vector3.zero, Vector3.one * (2f + 2f / resolutionX));
	// draw procedural params: mesh, sub-meshIndex, material, bounds, count.
	Graphics.DrawMeshInstancedProcedural(mesh, 0, material, bounds, resolutionX * resolutionZ);
```