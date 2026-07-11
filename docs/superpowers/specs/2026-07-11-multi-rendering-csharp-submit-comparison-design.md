# MultiRenderingBenchmark C# 提交方式对比设计

## 目标

将 `MultiRenderingBenchmark` 调整为只比较每实例 8 字节位置数据的路径，并明确加入以下两种 C# 发起的 RenderingDevice 提交方式：

1. C# 直接调用 `RenderingDevice.TextureUpdate`。
2. C# 通过函数指针调用 GDExtension，由 C++ 调用 `RenderingDevice::texture_update`。

保留现有 RenderingServer 位置纹理路径作为第三个参照项，删除综合基准中的 RenderingServer C++ MultiMesh 32MB 路径。为相关 C#、C++ 和 shader 代码增加详细中文注释。

## 基准结构

场景中使用三个彼此独立的测试节点，依次运行：

1. `RenderingDeviceCSharp`：`TestNodeRD.SubmitMode.CSharpOriginal`。
2. `RenderingDeviceGDExtension`：`TestNodeRD.SubmitMode.GDExtensionSingleBuffer`，带独立 `RDSubmitBridge` 子节点。
3. `RenderingServerTexture`：现有紧凑位置纹理提交路径。

两个 RenderingDevice 节点分别完成初始化，避免运行中切换模式造成纹理、桥接对象或计时状态残留。控制器继续通过启用当前节点、禁用其他节点的方式顺序采样。

## 数据口径

- RenderingDevice 两种模式都上传 `R32G32_SFLOAT` 位置纹理，每实例为 X/Y 两个 32 位浮点数，共 8 字节。
- 默认 1,000,000 个实例的有效位置数据约为 8,000,000 字节。
- 删除综合基准中的 C++ MultiMesh 32MB 测试项及其场景节点和专用网格资源。
- `TestNodeRD` 为实际绘制保留 Godot MultiMesh 所需的实例状态；这部分不作为综合基准名称中的动态提交字节数，也不在每帧重复调用 `MultimeshSetBuffer`。
- 三项继续使用相同 `InstanceCount`、预热帧数、采样帧数和采样轮数。

## 统计与输出

每项统计并展示：

- 逻辑耗时：更新实例位置。
- 填充耗时：从实例结构提取 X/Y 到连续上传区。
- 提交耗时：调用 Godot C# binding 或 GDExtension 原生入口的 CPU 时间。
- CPU 渲染准备：填充加提交。
- GPU 时间：RenderingDevice timestamp 可用时显示。
- CPU 总计：逻辑、填充和提交之和。

名称中明确标注两条 RD 路径均为 8MB 位置数据，避免把静态资源占用误认为每帧动态上传量。

## 中文注释范围

- `demo/benchmark/MultiRenderingBenchmark.cs`：阶段数组、节点与模式映射、采样累计、阶段切换、指标含义。
- `demo/c_sharp/TestNodeRD.cs`：实例结构、托管数组固定、8 字节位置布局、纹理创建、C# binding 路径、函数指针路径、单缓冲行为、GPU timestamp 和资源释放。
- `src/rd_submit_bridge.h`、`src/rd_submit_bridge.cpp`：桥接对象职责、ABI 参数、步长读取、复用 `PackedByteArray`、填充与提交计时边界。
- `demo/c_sharp/rd_instance.gdshader`：`INSTANCE_ID` 到纹理坐标的映射及顶点偏移。
- `demo/benchmark/multi_rendering_benchmark.tscn`：场景本身不支持代码注释；通过清晰节点名表达用途。

注释解释设计原因、数据所有权和性能边界，不逐行复述语法。

## 错误处理与资源生命周期

- GDExtension 模式缺少 `RDSubmitBridge` 时沿用现有回退行为并打印错误。
- 每个 RD 节点拥有自己的纹理 RID、wrapper、固定数组和桥接上下文。
- 节点退出时解除 wrapper 的 RID 引用，并在渲染线程释放纹理。
- 阶段切换只改变可见性和 `ProcessMode`，不在采样期间重建资源。

## 验证

1. 先增加自动源码/场景结构测试，验证三个阶段名称与模式、两个独立 RD 节点、32MB MultiMesh 项已移除，并观察测试在实现前失败。
2. 修改实现后再次运行结构测试，确认通过。
3. 使用 Release 配置执行 `dotnet build demo/GDEXTest.csproj -c Release`。
4. 若本机 Godot 可执行文件可用，以 benchmark 场景执行一次启动检查；否则明确报告仅完成编译和静态结构验证。

## 非目标

- 不修改 RenderingDevice 或 RenderingServer 的 Godot 引擎实现。
- 不新增三缓冲对比项；本次只比较 C# 直接单缓冲与 C# 经 GDExtension 单缓冲。
- 不重构其他独立 benchmark 场景。
- 不删除 `GDExample` 中仍被其他场景使用的 C++ MultiMesh 功能。
