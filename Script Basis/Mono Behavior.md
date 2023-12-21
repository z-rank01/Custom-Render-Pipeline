### Mono-behavior 
#Mono #MonoVM #\.NET
1. reason: 

		unity implement cross-platform project "Mono" to extend .NET Framework. 
	[[Unity_IL2CPP.jpg]]

2. full inheritance chain

		MonoBehavior <- Behavior <- Component <- Object

3. Method

		Note: There is a checkbox for enabling or disabling MonoBehaviour in the Unity Editor. It disables functions when unticked. If none of these functions are present in the script, the Unity Editor does not display the checkbox. The functions are:  
[Start](https://docs.unity3d.com/ScriptReference/MonoBehaviour.Start.html)()  
[Update](https://docs.unity3d.com/ScriptReference/MonoBehaviour.Update.html)()  
[FixedUpdate](https://docs.unity3d.com/ScriptReference/MonoBehaviour.FixedUpdate.html)()  
[LateUpdate](https://docs.unity3d.com/ScriptReference/MonoBehaviour.LateUpdate.html)()  
[OnGUI](https://docs.unity3d.com/ScriptReference/MonoBehaviour.OnGUI.html)()  
[OnDisable](https://docs.unity3d.com/ScriptReference/MonoBehaviour.OnDisable.html)()  
[OnEnable](https://docs.unity3d.com/ScriptReference/MonoBehaviour.OnEnable.html)()