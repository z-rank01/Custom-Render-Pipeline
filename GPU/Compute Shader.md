## Shouldn't we use `DrawMeshInstancedIndirect`?

>The DrawMeshInstancedIndirect method is useful for when you do not know how many instances to draw on the CPU side and instead provide that information with a compute shader via a buffer.

### Corresponding pair:

Macro's `definition` <-> Macro's `declaration`

Kernel's `definition` <-> Kernel's `declaration`

## Macros: to invoke multiple functions (kind of like delegation)

##### Definition

	When we write KERNEL_FUNCTION the compiler will replace it with the code for the FunctionKernel function. To make it work for an arbitrary function we add a parameter to the macro. This works like the parameter list for a function, but without types and the opening bracket must be attached to the macro name. Give it a single function parameter and use that instead of the explicit invocation of Wave.

`KERNEL_FUNCTION`: macro function's name
`function`: parameters
`\`: the rest code should be on same line

```c#
#define KERNEL_FUNCTION(function) \
	[[numthreads](8, 8, 1)] \
	void function##Kernel (uint3 id: [SV_DispatchThreadID]{ \
		float2 uv = GetUV(id); \
		SetPosition(id, function(uv.x, uv.y, _Time)); \
	}
```

	These definitions normally only apply to whatever is written behind them on the same line, but we can extend it to multiple lines by adding a \ backslash at the end of every line except the last.


##### Declaration

`KERNEL_FUNCTION`: for using defined function with actual parameters (in this case the math function name)

```c#
KERNEL_FUNCTION(Wave)
KERNEL_FUNCTION(MultiWave)
KERNEL_FUNCTION(Ripple)
KERNEL_FUNCTION(Sphere)
KERNEL_FUNCTION(Torus)
```


## Kernel: to be invoked and rendered by c# file

##### Definition

	We also have to change the kernel function's name. We'll use the function parameter as a prefix, followed by Kernel. To combine both words connect them with the ## macro concatenation operator.

`##`: concatenation of variables and rest text
`[SV_DispatchThreadID]`: variable offered by pipeline in compute shader. use this to identify single thread. 

```c#
void function##Kernel (uint3 id: [SV_DispatchThreadID] {...}
```

##### Declaration

pragma for kernel function invoked by c# files (`var kernelIndex` to tell which kernel to use)

```c#
#pragma kernel WaveKernel
#pragma kernel MultiWaveKernel
#pragma kernel RippleKernel
#pragma kernel SphereKernel
#pragma kernel TorusKernel
```


## Compute Threads

	When a GPU is instructed to execute a compute shader function it partitions its work into groups and then schedules them to run independently and in parallel. 

	Each group in turn consists of a number of threads that perform the same calculations but with different input.

`[numthreads: [x, y, z]]`
`CSMain:` kernel name, should be the same with `#pragma kernel kernelName`
```c#
[numthreads(1, 1, 1)]
void CSMain() {...}
```

	GPU hardware contains compute units that always run a specific fixed amount of threads in lockstep. These are known as warps or wavefronts.

	If the amount of threads of a group is less than the warp size some threads will run idle, wasting time.

	If the amount of threads instead exceeds the size then the GPU will use more warps per group.

	In general 64 threads is a good default, as that matches the warp size of AMD GPUs while it's 32 for NVidia GPUs, so the latter will use two warps per group. In reality the hardware is more complex and can do more with thread groups.


## Use buffer data

example:

```c#
[RWStructuredBuffer]<float3> _Positions;

void SetPosition (uint3 id, float3 position) {
	_Positions[id.x + id.y * _Resolution] = position;
}
```

The result can be uesd in other shader like vertex and fragment shader to render