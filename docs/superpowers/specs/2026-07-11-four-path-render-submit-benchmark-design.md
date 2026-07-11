# 五路径渲染提交基准设计

## 目标

在 `MultiRenderingBenchmark` 中使用相同的百万实例位置数据和相同绘制负载，对比 C#、GDExtension、纯 C++ 与 RenderingDevice、RenderingServer 形成的五条提交路径。

## 测试矩阵

综合基准依次运行以下五项：

1. C# → `RenderingDevice.TextureUpdate`。
2. C# → `Image.SetData` → `ImageTexture.Update`（RenderingServer 管理的非 RD 路径）。
3. C# 函数指针 → GDExtension → `RenderingDevice::texture_update`。
4. C# 函数指针 → GDExtension → `Image::set_data` → `ImageTexture::update`（RenderingServer 管理的非 RD 路径）。
5. 纯 C++ 逻辑与提交 → `PackedByteArray` → `Image::set_data` → `ImageTexture::update`。

每条路径都只动态提交 `R32G32` 位置数据，即每实例 X/Y 两个 32 位浮点数，共 8 字节。默认一百万实例的有效位置数据为 8,000,000 字节。

## 纯 C++ RenderingServer 模式

现有 `GDExample` 增加导出属性 `CompactTextureBenchmark`。仅在该属性开启时：

- 初始化自身的一百万个 C++ 实例和一份静态 identity MultiMesh transform。
- 创建双 `PackedByteArray`、`Image`、`ImageTexture` 和使用位置纹理的 shader material。
- 每个物理帧在 C++ 内更新实例逻辑，将 `current_pos` 提取为每实例8字节连续数据，并调用 `Image::set_data` 与 `ImageTexture::update`。
- 沿用现有 `benchmark_completed` signal，分别统计逻辑、填充、提交和总计。

该节点与只提供托管提交函数的 `RenderingServerBridge` 是两个独立 `GDExample` 实例。bridge 节点继续禁用处理；纯 C++ benchmark 节点只在自己的测试阶段启用。

旧的 `IsUseBuffer` 32MB MultiMesh transform 路径继续保留给其他场景，但综合基准中的纯 C++ 节点必须开启 `CompactTextureBenchmark`，不得回退到32MB动态提交。

## 组件设计

### RenderingDevice 两项

继续使用两个独立的 `TestNodeRD`：

- `RenderingDeviceCSharp` 固定为 `CSharpOriginal`。
- `RenderingDeviceGDExtension` 固定为 `GDExtensionSingleBuffer`。

两者保持现有 RD 纹理格式、shader、计时和资源释放逻辑。

### RenderingServer 两项

使用两个独立的 `TestNodeNormal`，并启用紧凑位置纹理：

- `RenderingServerCSharp` 设置 `UseNativePlugin = false`、`UseCompactPositionTexture = true`，由 C# 创建并复用 `Image`、`ImageTexture` 和位置 `byte[]`，每帧调用 `Image.SetData` 与 `ImageTexture.Update`。
- `RenderingServerGDExtension` 设置 `UseNativePlugin = true`、`UseCompactPositionTexture = true`，沿用 `GDExample` 原生 bridge 的双 `PackedByteArray`、`Image` 与 `ImageTexture::update` 路径。

`TestNodeNormal` 的纯 C# 紧凑纹理路径是新增行为。原有 32 字节 MultiMesh transform 路径保留给其他场景，但不进入本综合基准。

### 原生 bridge

- `RDSubmitBridge` 继续服务 GDExtension + RD 项。
- `GDExample` 继续服务 GDExtension + RenderingServer 项。
- RenderingServer C# 项不依赖原生 bridge。
- 场景中的 `GDExample` bridge 使用独立的最小 MultiMesh 资源，自身禁用处理，只提供原生方法和上下文。

## 场景与阶段隔离

`multi_rendering_benchmark.tscn` 保存五个独立 benchmark 节点。控制器在 `_EnterTree` 写入实例数与模式，只启用当前阶段节点。阶段完成后禁用并隐藏当前节点，在下一次主循环延迟启用下一节点。

独立节点会增加静态测试资源占用，但能避免切换 API 时重建纹理、材质、bridge 和计时状态，从而提高结果可信度。

## 计时与输出

所有路径继续报告：

- 逻辑：实例位置更新。
- 填充：从 `InstanceData` 提取 X/Y 并形成连续位置数据。
- 提交：C# 或原生 API 更新调用的 CPU 返回时间。
- CPU 准备：填充加提交。
- CPU 总计：逻辑、填充和提交之和。
- RD 路径可用时额外报告 GPU timestamp；非 RD 路径显示不可用。

最终摘要以“C# + RenderingDevice”为基线，输出其他路径的 CPU 准备加速比。名称明确标注语言边界、API 层级和 8MB 数据口径。

每个模式还必须输出完整提交链路，避免只用 RD/RS 名称掩盖中间封装成本：

1. C# + RD：`InstanceData[] → byte[] → RenderingDevice.TextureUpdate`。
2. C# + RS：`InstanceData[] → byte[] → Image.SetData → ImageTexture.Update`。
3. GDExtension + RD：`InstanceData* → PackedByteArray → RenderingDevice::texture_update`。
4. GDExtension + RS：`InstanceData* → PackedByteArray → Image::set_data → ImageTexture::update`。
5. 纯 C++ + RS：`C++ InstanceData → PackedByteArray → Image::set_data → ImageTexture::update`。

最终摘要在五项原始数据之后输出六组成对比较。前四组覆盖 C#/GDExtension 与 RD/RS 的 2×2 矩阵：

- C# 提交路径：RD 对 RS。
- GDExtension 提交路径：RD 对 RS。
- RD 语言边界：C# 对 GDExtension。
- RS 语言边界：C# 对 GDExtension。

每组成对比较同时显示左、右两项的填充、提交、CPU 准备和 CPU 总计，并以 CPU 准备计算右项相对左项的倍数与耗时变化百分比。百分比为正表示右项更快，为负表示右项更慢；除数为零时显示不可用。

新增两组纯 C++ 比较：

- C# + RS 对纯 C++ + RS，用于观察完整托管逻辑与纯原生逻辑的差异。
- GDExtension + RS 对纯 C++ + RS，用于区分“C# 逻辑 + 原生提交”与“逻辑和提交均在 C++”的差异。

## 错误处理

- C# RenderingServer 紧凑纹理创建失败时打印错误并停止该节点，不静默回退到 32MB transform，以免破坏比较口径。
- GDExtension bridge 缺失或函数地址无效时打印错误并停止对应节点。
- 五个 benchmark 节点的信号按固定索引接入控制器；非当前阶段信号被忽略。

## 中文注释

新增 C# RenderingServer 路径必须注释：8 字节布局、`Image.SetData` 可能发生的数据封装、`ImageTexture.Update` 的计时边界、纹理与材质生命周期。控制器注释四阶段映射和公平性约束。既有 RD、C++ bridge 与 shader 注释保留。

## 验证

1. 扩展结构回归测试，先验证它因缺少四阶段和 C# RenderingServer 紧凑路径而失败。
2. 实现后验证场景包含五个独立 benchmark 节点、五种模式映射，且综合控制器不包含 32MB 测试名称。
3. 验证 C# RenderingServer 分支实际调用 `Image.SetData` 和 `ImageTexture.Update`，且不调用 `RenderingDevice`。
4. 执行 `dotnet build demo/GDEXTest.csproj -c Release`，要求 0 错误。
5. 若 Godot 可执行文件可调用，运行综合场景完成启动检查；否则报告该限制。
6. 结构测试确认纯 C++ 节点开启 `CompactTextureBenchmark`，并确认其热路径调用 `submit_managed_positions_texture` 而非 `multimesh_set_buffer`。

## 非目标

- 不删除独立 RD benchmark 或旧的 32MB MultiMesh 实现。
- 不修改 Godot 引擎源码。
- 不把 GPU timestamp 强加到 RenderingServer 高层路径。
- 不在本次工作中修改乱码问题。
