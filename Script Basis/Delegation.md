### Delegation
#delegation #delegate
1. Technique

		委托（delegate）是一种类型安全的[函数指针]，用于[通用语言运行库]（CLI）。在 C# 中，delegate是一种class，包装了一个或多个函数指针及绑定的类实例。Delegate 用来实现函数回调与事件接收（event listener）。Delegate 对象可以作为参数传递给其他函数，以引用（referenced）封装在 delegate 对象中的函数，而无需在编译时刻就绑定被调用函数。

		一旦为委托分配了函数方法，委托将与该函数方法具有完全相同的行为。与委托的类型特征（由返回类型和参数组成）匹配的任何方法都可以分配给该委托。

		"委托"作为类，继承自 System.MulticastDelegate（抽象类）。可以认为包含：一个类对象实例的地址（Target 属性），该类的一个方法的地址（Method 属性），以及另一个"委托"实例的引用（reference）。因此引用一个"委托"对象，可能实际上引用了多个"委托"的实例。"委托"对象被调用时，依次调用里面的多个“委托”的实例。这对于事件驱动的程序比较有用。

		如果"委托"封装了一个静态函数，则其内部的绑定的类对象地址为null。

		可以通过 Delegate 类的 GetInvocationList() 取出这些委托，并查看其 Target 和 Method 属性，获取所引用的方法名等信息。


2. Usage

```c#
using UnityEngine
using System
...
// initialization
public delegate float Delegator(float parameter){...}
...
// instantiation, function's parameter should be matched.
Delegator functions = {function1, function2, function3, ...};
...
// use
float variable = functions[function1](para);
...
```