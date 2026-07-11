using Godot;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

public partial class TestNodeRD : Node2D
{
	// 三种模式共享完全相同的逻辑更新和纹理格式，只改变 CPU 数据进入 RD 的路径。
	// 综合基准使用前两种单缓冲模式；三缓冲模式保留给独立的 RD 对比场景。
    public enum SubmitMode
    {
        CSharpOriginal,
        GDExtensionSingleBuffer,
        GDExtensionTripleBuffer
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct InstanceData
    {
		// CurrentPos 必须保持为结构体首字段：C++ bridge 按 stride 遍历实例，并复制开头的 Vector2。
        public Vector2 CurrentPos;
        public Vector2 TargetPos;
        public Vector2 Velocity;
        public bool Arrived;
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private unsafe delegate void NativeSubmitDelegate(
        nint context, nint instances, int count, int stride, int textureIndex,
        out double fillUsec, out double submitUsec);

    [Signal]
    public delegate void BenchmarkCompletedEventHandler(
        double logicMs, double fillMs, double submitMs, double gpuMs,
        double totalMs, long instanceCount);

    [Export] public Button Button { get; set; }

    [Export(PropertyHint.Range, "1,1000000,1")]
    public int InstanceCount { get; set; } = 100_000;

    [Export] public SubmitMode UploadMode { get; set; } = SubmitMode.GDExtensionSingleBuffer;

    private const int WarmupFrames = 60;
    private const int SampleFrames = 60;
    private const int TextureRingSize = 3;

    private RenderingDevice rd;
    private readonly Rid[] rdTextures = new Rid[TextureRingSize];
    private readonly Texture2Drd[] textureWrappers = new Texture2Drd[TextureRingSize];
	// [8-byte-per-instance] R32G32_SFLOAT 只保存 X/Y 两个 float，即每实例 8 字节。
	// byte[] 是 RenderingDevice.TextureUpdate 的 C# API 形态；unsafe 填充避免逐元素 BitConverter 分配。
    private byte[] positionData;
    private GCHandle positionHandle;
    private nint positionPtr;
    private GCHandle instancesHandle;
    private nint instancesPtr;
    private int instanceCount;
    private int textureWidth;
    private int textureHeight;
    private int uploadTexelCount;
    private int textureIndex;

    private MultiMeshInstance2D multiMeshInstance;
    private ShaderMaterial shaderMaterial;
    private InstanceData[] instances;
    private Vector2 viewportSize;

    private GodotObject nativeBridge;
	// [managed/native-boundary] delegate 和 context 共同构成零 Variant 的热路径：每帧从 C#
	// 直接进入导出的 C ABI 函数，不经过 GodotObject.Call 的参数封装。
    private NativeSubmitDelegate nativeSubmit;
    private nint nativeContext;

    private double logicTimeUsec;
    private double fillTimeUsec;
    private double submitTimeUsec;
    private double gpuTimeNsec;
    private int gpuSampleCount;
    private int benchmarkWarmupFrames;
    private int benchmarkFrameCount;
    private ulong lastTimestampFrame = ulong.MaxValue;
    private string timestampPrefix;

    public override void _Ready()
    {
        if (Button != null) Button.Pressed += ShowEntityInfo;

        instanceCount = Math.Max(1, InstanceCount);
        InstanceCount = instanceCount;
        viewportSize = GetViewportRect().Size;
        timestampPrefix = $"rd_upload_{GetInstanceId()}_";
        rd = RenderingServer.GetRenderingDevice();
        if (rd == null)
        {
            GD.PrintErr("[基准测试][RD] RenderingDevice 不可用。");
            SetPhysicsProcess(false);
            return;
        }

        InitializeInstances();
        SetPhysicsProcess(false);
        RenderingServer.CallOnRenderThread(Callable.From(CreateTexturesOnRenderThread));
    }

    private void InitializeInstances()
    {
		// 实例逻辑结构长期复用并固定地址。固定期间数组不能被 GC 移动，退出节点时必须释放句柄。
        instances = new InstanceData[instanceCount];
        var rng = new Random(12345);
        for (int i = 0; i < instanceCount; i++)
        {
            instances[i].CurrentPos = new Vector2((i % 50) * 10f, (i / 50) * 10f);
            instances[i].Velocity = new Vector2(
                (float)rng.NextDouble() * 400f - 200f,
                (float)rng.NextDouble() * 400f - 200f);
        }
        instancesHandle = GCHandle.Alloc(instances, GCHandleType.Pinned);
        instancesPtr = instancesHandle.AddrOfPinnedObject();

        textureWidth = (int)Math.Min((long)instanceCount,
            (long)rd.LimitGet(RenderingDevice.Limit.MaxTextureSize2D));
        textureHeight = (instanceCount + textureWidth - 1) / textureWidth;
        uploadTexelCount = textureWidth * textureHeight;
		// 最后一行可能有未使用 texel，所以按完整纹理尺寸分配；shader 只读取有效 INSTANCE_ID。
        positionData = new byte[uploadTexelCount * sizeof(float) * 2];
        positionHandle = GCHandle.Alloc(positionData, GCHandleType.Pinned);
        positionPtr = positionHandle.AddrOfPinnedObject();
    }

    private void CreateTexturesOnRenderThread()
    {
		// RID 的创建与释放都安排在渲染线程。SamplingBit 允许 shader 采样，CanUpdateBit 允许每帧更新。
        var format = new RDTextureFormat
        {
            Format = RenderingDevice.DataFormat.R32G32Sfloat,
            Width = (uint)textureWidth,
            Height = (uint)textureHeight,
            UsageBits = RenderingDevice.TextureUsageBits.SamplingBit |
                        RenderingDevice.TextureUsageBits.CanUpdateBit
        };
        var initialData = new Godot.Collections.Array<byte[]> { positionData };
        for (int i = 0; i < TextureRingSize; i++)
        {
            rdTextures[i] = rd.TextureCreate(format, new RDTextureView(), initialData);
            if (!rdTextures[i].IsValid)
            {
                throw new InvalidOperationException($"无法创建 RD 位置纹理 {i}。");
            }
        }
        Callable.From(FinishInitialization).CallDeferred();
    }

    private void FinishInitialization()
    {
        for (int i = 0; i < TextureRingSize; i++)
        {
            textureWrappers[i] = new Texture2Drd { TextureRdRid = rdTextures[i] };
        }
        InitializeRenderer();
        InitializeNativeBridge();
        SetPhysicsProcess(true);
        GD.Print($"[基准测试][RD] 初始化完成 模式={ModeName} 实例={instanceCount} 纹理={textureWidth}x{textureHeight} 预热={WarmupFrames}帧");
    }

    private void InitializeRenderer()
    {
        multiMeshInstance = GetNode<MultiMeshInstance2D>("MultiMeshInstance2D");
        MultiMesh multiMesh = multiMeshInstance.Multimesh;
        multiMesh.InstanceCount = instanceCount;
        // The vertex shader moves instances outside the static identity transforms.
        // Give the canvas renderer the real bounds and avoid accidental culling.
        multiMesh.CustomAabb = new Aabb(
            new Vector3(-32f, -32f, -1f),
            new Vector3(viewportSize.X + 64f, viewportSize.Y + 64f, 2f));

        var shader = ResourceLoader.Load<Shader>("res://c_sharp/rd_instance.gdshader");
        shaderMaterial = new ShaderMaterial { Shader = shader };
        shaderMaterial.SetShaderParameter("instance_positions", textureWrappers[0]);
        shaderMaterial.SetShaderParameter("position_texture_width", textureWidth);
        multiMeshInstance.Material = shaderMaterial;

		// Godot 的 2D MultiMesh 仍要求每个实例有 transform。这里只在初始化时提交单位矩阵；
		// 每帧动态位置不再写入该 32 字节 transform，而由 8 字节位置纹理在 vertex shader 中叠加。
		var identityBuffer = new float[instanceCount * 8];
        for (int i = 0; i < instanceCount; i++)
        {
            int offset = i * 8;
            identityBuffer[offset] = 1f;
            identityBuffer[offset + 5] = 1f;
        }
        RenderingServer.MultimeshSetBuffer(multiMesh.GetRid(), identityBuffer);
    }

    private void InitializeNativeBridge()
    {
        if (UploadMode == SubmitMode.CSharpOriginal) return;
        nativeBridge = GetNodeOrNull<GodotObject>("RDSubmitBridge");
        if (nativeBridge == null)
        {
            GD.PrintErr("[基准测试][RD] 缺少 RDSubmitBridge，回退到 C# 原版提交。");
            UploadMode = SubmitMode.CSharpOriginal;
            return;
        }

		// 单缓冲只把纹理 0 交给 C++；三缓冲才注册全部 RID。
		int configuredTextures = UploadMode == SubmitMode.GDExtensionTripleBuffer ? TextureRingSize : 1;
        for (int i = 0; i < configuredTextures; i++)
        {
            nativeBridge.Call("configure_texture", i, rdTextures[i], uploadTexelCount);
        }
        nativeContext = (nint)(long)nativeBridge.Call("get_context_address");
        nint address = (nint)(long)nativeBridge.Call("get_submit_address");
        nativeSubmit = Marshal.GetDelegateForFunctionPointer<NativeSubmitDelegate>(address);
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public override void _PhysicsProcess(double delta)
    {
        if (rd == null) return;
        CollectGpuTimestamp();

        ulong logicStart = Time.GetTicksUsec();
        UpdatePositions((float)delta);
        ulong logicEnd = Time.GetTicksUsec();

        double fillUsec;
        double submitUsec;
        string beginName = timestampPrefix + "开始";
        string endName = timestampPrefix + "结束";
        rd.CaptureTimestamp(beginName);
        if (UploadMode == SubmitMode.CSharpOriginal)
        {
			// 直接路径分两段计时：fill 是 C# 提取位置，submit 包含 C# binding 和 RD 更新调用。
            ulong fillStart = Time.GetTicksUsec();
            FillPositionBuffer();
            ulong submitStart = Time.GetTicksUsec();
            rd.TextureUpdate(rdTextures[0], 0, positionData);
            ulong submitEnd = Time.GetTicksUsec();
            fillUsec = submitStart - fillStart;
            submitUsec = submitEnd - submitStart;
        }
        else
        {
			// 原生路径在 C++ 内用相同边界计时，再把两个结果通过 out 参数返回。
            SubmitNative(out fillUsec, out submitUsec);
        }
        rd.CaptureTimestamp(endName);

        if (benchmarkWarmupFrames < WarmupFrames)
        {
            benchmarkWarmupFrames++;
            return;
        }

        logicTimeUsec += logicEnd - logicStart;
        fillTimeUsec += fillUsec;
        submitTimeUsec += submitUsec;
        benchmarkFrameCount++;
        if (benchmarkFrameCount < SampleFrames) return;

        double logicMs = logicTimeUsec / SampleFrames / 1000.0;
        double fillMs = fillTimeUsec / SampleFrames / 1000.0;
        double submitMs = submitTimeUsec / SampleFrames / 1000.0;
        double gpuMs = gpuSampleCount > 0 ? gpuTimeNsec / gpuSampleCount / 1_000_000.0 : -1.0;
        string gpuText = gpuMs >= 0 ? $"{gpuMs:F4}ms" : "不可用";
        GD.Print(
            $"[基准测试][RD] 模式={ModeName} 实例={instanceCount} 帧={SampleFrames} " +
            $"纯逻辑平均={logicMs:F4}ms 填充平均={fillMs:F4}ms 提交平均={submitMs:F4}ms " +
            $"CPU渲染准备平均={fillMs + submitMs:F4}ms GPU上传平均={gpuText} " +
            $"CPU总计平均={logicMs + fillMs + submitMs:F4}ms");
        EmitSignal(SignalName.BenchmarkCompleted, logicMs, fillMs, submitMs, gpuMs,
            logicMs + fillMs + submitMs, (long)instanceCount);
        ResetSampleAccumulators();
    }

    private unsafe void FillPositionBuffer()
    {
		// positionData 已固定，可把它视为 float* 连续写入；不会在热路径创建临时 float[]。
        float* destination = (float*)positionPtr;
        for (int i = 0; i < instanceCount; i++)
        {
            destination[i * 2] = instances[i].CurrentPos.X;
            destination[i * 2 + 1] = instances[i].CurrentPos.Y;
        }
    }

    private unsafe void SubmitNative(out double fillUsec, out double submitUsec)
    {
		// 传入的是 InstanceData[] 首地址和真实 stride。C++ 只复制每个结构开头的 CurrentPos，
		// TargetPos、Velocity、Arrived 属于逻辑状态，不进入 GPU 位置纹理。
        textureIndex = UploadMode == SubmitMode.GDExtensionTripleBuffer
            ? (textureIndex + 1) % TextureRingSize
            : 0;
        ulong callStart = Time.GetTicksUsec();
        nativeSubmit(nativeContext, instancesPtr, instanceCount, Unsafe.SizeOf<InstanceData>(), textureIndex,
            out fillUsec, out double nativeSubmitUsec);
        if (UploadMode == SubmitMode.GDExtensionTripleBuffer)
        {
            shaderMaterial.SetShaderParameter("instance_positions", textureWrappers[textureIndex]);
        }
        ulong callEnd = Time.GetTicksUsec();
        submitUsec = Math.Max(nativeSubmitUsec, callEnd - callStart - fillUsec);
    }

    private void CollectGpuTimestamp()
    {
		// GPU timestamp 通常延迟若干帧可读，以 RD 返回的 frame 编号去重，不能当帧同步使用。
        uint count = rd.GetCapturedTimestampsCount();
        ulong frame = rd.GetCapturedTimestampsFrame();
        if (count == 0 || frame == lastTimestampFrame) return;
        lastTimestampFrame = frame;

        ulong begin = 0;
        ulong end = 0;
        string beginName = timestampPrefix + "开始";
        string endName = timestampPrefix + "结束";
        for (uint i = 0; i < count; i++)
        {
            string name = rd.GetCapturedTimestampName(i);
            if (name == beginName) begin = rd.GetCapturedTimestampGpuTime(i);
            else if (name == endName) end = rd.GetCapturedTimestampGpuTime(i);
        }
        if (benchmarkWarmupFrames >= WarmupFrames && begin > 0 && end >= begin)
        {
            gpuTimeNsec += end - begin;
            gpuSampleCount++;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private void UpdatePositions(float delta)
    {
        float width = viewportSize.X;
        float height = viewportSize.Y;
        for (int i = 0; i < instances.Length; i++)
        {
            ref InstanceData instance = ref instances[i];
            float x = instance.CurrentPos.X + instance.Velocity.X * delta;
            float y = instance.CurrentPos.Y + instance.Velocity.Y * delta;
            if (x < 0) { x = 0; instance.Velocity.X = -instance.Velocity.X; }
            else if (x > width) { x = width; instance.Velocity.X = -instance.Velocity.X; }
            if (y < 0) { y = 0; instance.Velocity.Y = -instance.Velocity.Y; }
            else if (y > height) { y = height; instance.Velocity.Y = -instance.Velocity.Y; }
            instance.CurrentPos.X = x;
            instance.CurrentPos.Y = y;
        }
    }

    private void ResetSampleAccumulators()
    {
        logicTimeUsec = 0;
        fillTimeUsec = 0;
        submitTimeUsec = 0;
        gpuTimeNsec = 0;
        gpuSampleCount = 0;
        benchmarkFrameCount = 0;
    }

    public void ShowEntityInfo()
    {
        if (instances == null) return;
        for (int i = 0; i < Math.Min(100, instanceCount); i++)
        {
            GD.Print($"实体 {i}/{instanceCount}: {instances[i].CurrentPos}");
        }
    }

    public string ModeName => UploadMode switch
    {
        SubmitMode.CSharpOriginal => "C#原版单缓冲",
        SubmitMode.GDExtensionSingleBuffer => "GDExtension原生单缓冲",
        _ => "GDExtension原生三缓冲"
    };

    public override void _ExitTree()
    {
		// 先停物理帧并解除托管固定，再断开 Texture2Drd 对 RID 的引用，最后在渲染线程释放 RID。
        SetPhysicsProcess(false);
        if (instancesHandle.IsAllocated) instancesHandle.Free();
        if (positionHandle.IsAllocated) positionHandle.Free();
        if (rd != null)
        {
            if (multiMeshInstance != null) multiMeshInstance.Material = null;
            for (int i = 0; i < textureWrappers.Length; i++)
            {
                if (textureWrappers[i] != null) textureWrappers[i].TextureRdRid = default;
            }
            Rid[] texturesToFree = (Rid[])rdTextures.Clone();
            RenderingServer.CallOnRenderThread(Callable.From(() =>
            {
                foreach (Rid texture in texturesToFree)
                {
                    if (texture.IsValid) rd.FreeRid(texture);
                }
            }));
        }
    }
}
