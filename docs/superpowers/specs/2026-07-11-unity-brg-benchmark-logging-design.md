# Unity BatchRendererGroup 基准日志设计

## 目标

为外部 Unity 项目中的 `Assets/EntitiesGraphicsTutorials/Lesson3/Scripts/BatchRendererGroupSetup.cs` 增加与 Godot `RenderingServer3DBRG` 风格一致的 CPU 性能日志，不改变原有 BatchRendererGroup 数据布局、Burst Job 算法、剔除和渲染结果。

目标文件：

`E:\UnityProject\RoadToDotsTutorials-main\Assets\EntitiesGraphicsTutorials\Lesson3\Scripts\BatchRendererGroupSetup.cs`

## 计时口径

Unity 当前的 `UpdateInstanceDataJob` 同时计算波浪位置、矩阵和颜色，并直接写入 `sysmemBuffer`。为避免新增中间数组和改变性能路径，统计边界定义为：

- `逻辑`：`UpdatePositionsAndColorsWithJob(Vector3.zero)` 创建并调度所有 batch Job 的主线程时间。
- `填充RGBA32F`：从 Job 调度完成到 `JobHandle.Complete()` 返回的时间，包含 Burst Job 执行、写满 `sysmemBuffer` 以及主线程等待。
- `提交`：`gpuPersistentInstanceData.SetData(sysmemBuffer)` 的 CPU 调用时间。
- `CPU总计`：上述三项之和。

这些指标用于观察 Unity 侧 CPU 准备路径；`SetData()` 返回不代表 GPU 已经执行完上传，因此日志不称其为 GPU 时间。

## 采样行为

- 固定预热 `60` 帧，不计入累计值。
- 随后累计 `60` 帧。
- 每完成一轮 60 帧，输出各阶段平均毫秒数，并清零累计值和采样帧数。
- 后续继续每 60 帧输出一轮，方便观察稳定性。
- 初始化尚未完成、buffer 无效或对象销毁后不执行统计。

## 计时实现

- 使用 `System.Diagnostics.Stopwatch.GetTimestamp()` 获取单调高精度时间戳。
- 使用 `Stopwatch.Frequency` 将累计 tick 转为毫秒。
- 使用 `double` 累计 tick，避免长时间运行时整数求平均的截断。
- 不在每帧创建字符串；只在一轮采样完成时调用 `Debug.Log`。

## 输出格式

```text
[Unity BRG] 实例=40000 帧=60 逻辑=0.1897ms 填充RGBA32F=0.1876ms 提交=0.2284ms CPU总计=0.6058ms
```

- 实例数来自运行时 `instanceCount`。
- 帧数来自本轮实际 `sampleFrameCount`。
- 所有毫秒值使用 `F4` 格式。

## 代码改动

只修改 `BatchRendererGroupSetup.cs`：

- 引入 `System.Diagnostics` 的别名或使用完整类型名，避免 `Debug` 与 `UnityEngine.Debug` 命名冲突。
- 新增预热、采样和各阶段累计字段。
- 在 `Update()` 中加入四个时间点，并保持原 Job、`Complete()`、`SetData()` 的调用顺序不变。
- 新增一个只在采样结束时计算平均值和输出日志的私有方法。

## 验证

1. 先添加外部脚本结构测试，要求 Stopwatch、60 帧预热/采样、三个计时边界和目标日志文本存在，并确认修改前失败。
2. 修改脚本后运行结构测试转绿。
3. 检查脚本尾随空白和 C# 语法结构。
4. 若 Unity 工程可通过命令行定位对应 Editor，则执行脚本编译；否则明确报告未运行 Unity 编译。

## 非目标

- 不拆分 Burst Job。
- 不增加 GPU fence 或 GPU timestamp。
- 不统计 `OnPerformCulling`，因为它由渲染管线回调，不在 `Update()` 的实例数据准备链路中。
- 不更改实例数量、网格、材质、颜色公式、剔除或运动矢量行为。
