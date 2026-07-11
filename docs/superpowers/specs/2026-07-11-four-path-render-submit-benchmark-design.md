# 四路径渲染提交基准设计

## 目标

在 `MultiRenderingBenchmark` 中使用相同的百万实例位置数据和相同绘制负载，对比 C#、GDExtension 与 RenderingDevice、RenderingServer 组合形成的四条提交路径。

## 测试矩阵

综合基准依次运行以下四项：

1. C# → `RenderingDevice.TextureUpdate`。
2. C# → `Image.SetData` → `ImageTexture.Update`（RenderingServer 管理的非 RD 路径）。
3. C# 函数指针 → GDExtension → `RenderingDevice::texture_update`。
4. C# 函数指针 → GDExtension → `Image::set_data` → `ImageTexture::update`（RenderingServer 管理的非 RD 路径）。

每条路径都只动态提交 `R32G32` 位置数据，即每实例 X/Y 两个 32 位浮点数，共 8 字节。默认一百万实例的有效位置数据为 8,000,000 字节。

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

`multi_rendering_benchmark.tscn` 保存四个独立 benchmark 节点。控制器在 `_EnterTree` 写入实例数与模式，只启用当前阶段节点。阶段完成后禁用并隐藏当前节点，在下一次主循环延迟启用下一节点。

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

## 错误处理

- C# RenderingServer 紧凑纹理创建失败时打印错误并停止该节点，不静默回退到 32MB transform，以免破坏比较口径。
- GDExtension bridge 缺失或函数地址无效时打印错误并停止对应节点。
- 四个节点的信号按固定索引接入控制器；非当前阶段信号被忽略。

## 中文注释

新增 C# RenderingServer 路径必须注释：8 字节布局、`Image.SetData` 可能发生的数据封装、`ImageTexture.Update` 的计时边界、纹理与材质生命周期。控制器注释四阶段映射和公平性约束。既有 RD、C++ bridge 与 shader 注释保留。

## 验证

1. 扩展结构回归测试，先验证它因缺少四阶段和 C# RenderingServer 紧凑路径而失败。
2. 实现后验证场景包含四个独立节点、四种模式映射，且综合控制器不包含 32MB 测试名称。
3. 验证 C# RenderingServer 分支实际调用 `Image.SetData` 和 `ImageTexture.Update`，且不调用 `RenderingDevice`。
4. 执行 `dotnet build demo/GDEXTest.csproj -c Release`，要求 0 错误。
5. 若 Godot 可执行文件可调用，运行综合场景完成启动检查；否则报告该限制。

## 非目标

- 不删除独立 RD benchmark 或旧的 32MB MultiMesh 实现。
- 不修改 Godot 引擎源码。
- 不把 GPU timestamp 强加到 RenderingServer 高层路径。
- 不在本次工作中修改乱码问题。
