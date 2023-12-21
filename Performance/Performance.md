### Batch

		SRP batching: Default in Universal Pipeline, https://docs.unity3d.com/cn/2023.2/Manual/SRPBatcher.html
		static batching: Useful for static objects, https://docs.unity3d.com/cn/2023.2/Manual/static-batching.html
		Dynamic batching: Useful in built-in Pipeline, https://docs.unity3d.com/cn/2023.2/Manual/dynamic-batching.html

Preference setting:
[[URPBatching_PreferenceSetting.png]]

Batching configuration:
[[BatchingConfiguration.png]]

SRP Batching:
[[SRPBatching.png]]

Dynamic Batching (seems no effect in URP pipeline):
[[DynamicBatching.png]]

Without Batching:
[[NoBatching.png]]

### GPU Instancing

		提高渲染性能的另一种方法是启用 GPU 实例。这使得可以使用单个绘制命令来告诉 GPU 使用相同材质绘制一个网格的多个实例，从而提供一组变换矩阵和可选的其他实例数据。在这种情况下，我们必须为每种材质启用它。我们有一个"启用 GPU 实例化"开关。

[[GPUInstancing.png]]


### Frame Debugger for GPU

		We can check actual draw call in the last frame

SRP Batching:
[[[FrameDebugger_SRPBatching.png]]]

Dynamic Batching:
[[FrameDebugger_DynamicBatching.png]]

GPU Instancing:
[[FrameDebugger_GPUInstancing.png]]


### Profiler for CPU
#profiler 

		CPU Usage
		Timeline: single frame(ms) 
			Main Thread: PlayerLoop -> EditorLoop -> PlayerLoop(calling GPU) -> EditorLoop
			Render Thread: wait -> RenderSingleCamera (After second PlayerLoop) -> wait (if render is too long, could be extended to next frame)

![[Profiler.png]]

		Profile build: % percentage 
			actual running consumption. And we can switch to Hierarchy mode to see proportion instead of timeline.

![[Profiler2.png]]




### UI for FPS
#UI #ui #canvas #panel

		-Canvas
			-panel
				-Text (script.cs)
		-EventSystem



### Built-in RP and Universal RP
#BRP #brp #URP #urp #built-in #universal

		URP does twice CPU->GPU: shadow and opaque object
		BRP does third times CPU->GPU: depth-only, shadow and opagque object(for each different light source) 

