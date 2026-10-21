
## 项目文件结构介绍

本项目下有一个`Tags`文件夹，包含了有关测点的配置、业务逻辑、依赖注入等信息：
```
Tags/
  _Extensions.cs            # DI 注册入口
  Logicets/                 # 业务逻辑写到这里
    HeartBeatLogicet.cs     # 示例的心跳逻辑，可以用于观测心跳和实际的扫描周期
  index.xml                 # 测点配置：通道+测点+逻辑插件
  JsonTags.cs               # 示例的自定义类型，会被当作`JsonTag`，用于演示 NativeAOT
```

通常，调用者需要先拿到一个`ITagsProjectCtrl`来启停一个测点项目：
```c#
var _ctrl = sp.GetRequiredService<ITagsProjectCtrl>();

// 或者你想拿到命名的 ITagsProjectCtrl
var _ctrl = sp.GetRequiredKeyedService<ITagsProjectCtrl>("your-name");
```
有了`ITagsProjectCtrl`，调用者就可以用它启动、或者停止测点项目。

调用者可以通过`ctrl.StartPollAsync(dir, root, hook)`方法启动项目。注意，此方法"几乎"不会结束——异常会自动捕获，并按用户注入的策略进行重试(详见[文档](https://tags.doc.stdunit.com/chapter_1%E2%80%94%E2%80%94%E8%B5%B7%E6%AD%A5#%E9%A1%B9%E7%9B%AE%E8%BF%90%E8%A1%8C)):
```c#
await _ctrl.StartPollAsync(dir, root: null, (proj, sp, ct) =>
{
    proj.RunnerStarted += (grp, ch) =>{
        _logger.LogInformation("Tags处理开始,grp={grpName}", grp.TagName());
        return Task.CompletedTask;
    };
    proj.RunnerCrashed += (grp, ch, ex) =>{
        _logger.LogError(ex, "Tags处理异常,grp={grpName}", grp.TagName());
        return Task.CompletedTask;
    };


    var loggerFactory = sp.GetRequiredService<ILoggerFactory>();

    // 手动注册业务逻辑
    proj.Logicets.Add(new HeartBeatLogicet(proj.Channels, proj.Tags, loggerFactory.CreateLogger<HeartBeatLogicet>()));
    // ...其它也逻辑

    return Task.CompletedTask;
});
```

调用者终止项目运行的代码是：
```c#
await ctrl.StopAsync();
```

`ITagsProjectCtrl`还提供了`StartedOrStopped`事件，以让外部人员观测由它所控制的项目启停事件。喜欢`Rx/R3`的开发者，可以直接使用`ctrl.ObserveStartedOrStopped()`扩展方法。

## `StdUnit.Tags`介绍

[`StdUnit.Tags`](https://github.com/StdUnit/StdUnit.Tags)是一套面向**串行轮询读写**场景的 .NET 类库：它把一组只能周期性读写的资源表达成一棵**测点树**，再为每个入口提供一套严格串行的交互机制(多个入口并行执行)。我们有意地模仿了 PLC 的扫描机制:

0. 执行外部意图
1. 读入数据 
2. 处理逻辑
3. 刷写底层

从某种意义上说，它是一套测点通信抽象，外加一套以**确定性**为核心目标的 `mini-runtime`。

### 文档

`StdUnit.Tags`的相关文档和教程部署在[https://tags.doc.stdunit.com](https://tags.doc.stdunit.com)，主要涉及基本概念、常用写法、设计思路和驱动专题，其中也穿插了不少常见故障与排错手法。

AI 可以直接参考 [StdUnit.Tags —— 给 AI 的参考手册](https://tags.doc.stdunit.com/assets/999.AI.markdown)（机器可读的原始 Markdown），也可以先访问 <https://tags.doc.stdunit.com/llms.txt>，那里是文档站给 AI 准备的索引。

### 关于动态插件的限制:

以编程的方式，手动在代码中注册`ILogiect`，不管在哪种宿主中，都是可以正常使用的。但是想要在`XML`中通过`<Logicet/>`动态配置插件，则有两个平台限制。

| 宿主形态 | XML动态插件(`<Logicet/>`)  | 编程注册(`TryAddLogicet<TLogicet>`) |
|---|---|---|
| `net8.0` + CoreCLR（JIT / 仅裁剪 / 单文件） | ✅ 隔离 + 卸载 | ✅ |
| `net8.0` + **NativeAOT** | ❌ 不可用 | ✅ |
| `net472` | ✅ 降级可用 | ✅ |

#### netfx 的插件化限制

`net472` 没有 `AssemblyLoadContext`，无法做到「按目录隔离 + 可卸载」的插件加载，因此 `<Logicet>` 插件在该框架下退化为 `Assembly.LoadFrom`：

- **仍然可用**：插件可以独立编译、单独发布，再挂载到宿主——「宿主开发与插件开发分离」的能力保留；
- **无依赖隔离**：插件依赖与宿主同名程序集冲突时以先加载者为准，不会为插件目录单独建立加载上下文；
- **无法卸载**：`project` 停止不会释放插件程序集，插件 `dll` 在宿主进程退出前一直被锁定；
  同一路径的插件重新编译后，若不重启宿主，可能仍运行旧代码。

加载时会输出 `WARNING` 日志提示上述限制。若需要完整能力（隔离 + 卸载 + 热更新），请使用 `net8.0`。

#### AOT 的插件化限制

由于 **.NET NativeAOT 的运行时完全不提供「动态加载程序集」的能力**，所以在`XML`中，不支持通过配置`<Logicet/>`元素的方式为 AOT 宿主引入逻辑插件——**在 AOT 下只能使用代码注册的方式添加逻辑组件**。


> 配了 `<Logicet/>` 的 AOT 宿主会在**加载期**抛 `TagsProjectNotSupportedException`。
