using Godot;
using System;

public partial class MultiRenderingBenchmark : Node2D
{
	// 每个 Stats 对象只负责一个测试阶段。阶段结束后不清零，便于最终统一输出平均值。
	private sealed class Stats
	{
		public int Count;
		public double Logic, Fill, Submit, Gpu, Total;
		public int GpuCount;
		public double LogicAvg => Logic / Math.Max(1, Count);
		public double FillAvg => Fill / Math.Max(1, Count);
		public double SubmitAvg => Submit / Math.Max(1, Count);
		public double RenderAvg => FillAvg + SubmitAvg;
		public double TotalAvg => Total / Math.Max(1, Count);
		public double GpuAvg => GpuCount == 0 ? -1 : Gpu / GpuCount;

		public void Add(double logic, double fill, double submit, double gpu, double total)
		{
			Count++; Logic += logic; Fill += fill; Submit += submit; Total += total;
			if (gpu >= 0) { Gpu += gpu; GpuCount++; }
		}
	}

	[Export(PropertyHint.Range, "1,1000000,1")] public int InstanceCount { get; set; } = 1_000_000;
	[Export(PropertyHint.Range, "1,20,1")] public int SampleRounds { get; set; } = 5;

	private readonly string[] names =
	{
		"RenderingDevice + C# direct (8MB)",
		"RenderingServer + C# direct (8MB)",
		"RenderingDevice + GDExtension (8MB)",
		"RenderingServer + GDExtension (8MB)",
		"RenderingServer + pure C++ (8MB)"
	};
	// 这里描述的不只是最终 API，还明确列出托管/原生边界前后的数据容器。
	private readonly string[] submissionPaths =
	{
		"InstanceData[] -> byte[] -> RenderingDevice.TextureUpdate",
		"InstanceData[] -> byte[] -> Image.SetData -> ImageTexture.Update",
		"InstanceData* -> PackedByteArray -> RenderingDevice::texture_update",
		"InstanceData* -> PackedByteArray -> Image::set_data -> ImageTexture::update",
		"C++ InstanceData -> PackedByteArray -> Image::set_data -> ImageTexture::update"
	};
	private readonly Stats[] stats = { new(), new(), new(), new(), new() };
	private TestNodeRD rdCSharpBenchmark;
	private TestNodeRD rdGDExtensionBenchmark;
	private TestNodeNormal rsCSharpBenchmark;
	private TestNodeNormal rsGDExtensionBenchmark;
	private Node2D rsCppBenchmark;
	private GodotObject rsTextureNativeBridge;
	private CanvasItem[] benchmarkItems;
	private Label results;
	private int stage;

	public override void _EnterTree()
	{
		// 子节点的 _Ready 会创建纹理和实例数组，因此必须在 _EnterTree 中先写入测试参数。
		// 如果到 _Ready 才设置，子节点可能已经按默认实例数分配资源，导致测试口径不一致。
		InstanceCount = Math.Max(1, InstanceCount);
		SampleRounds = Math.Max(1, SampleRounds);
		rdCSharpBenchmark = GetNode<TestNodeRD>("RenderingDeviceCSharp");
		rdGDExtensionBenchmark = GetNode<TestNodeRD>("RenderingDeviceGDExtension");
		rsCSharpBenchmark = GetNode<TestNodeNormal>("RenderingServerCSharp");
		rsGDExtensionBenchmark = GetNode<TestNodeNormal>("RenderingServerGDExtension");
		rsCppBenchmark = GetNode<Node2D>("RenderingServerCpp");
		rsTextureNativeBridge = GetNode<GodotObject>("RenderingServerBridge");
		benchmarkItems = new CanvasItem[]
		{
			rdCSharpBenchmark,
			rsCSharpBenchmark,
			rdGDExtensionBenchmark,
			rsGDExtensionBenchmark,
			rsCppBenchmark
		};

		// 两个 RD 节点使用相同实例数，只改变托管数据进入 RenderingDevice 的路径。
		rdCSharpBenchmark.InstanceCount = InstanceCount;
		rdCSharpBenchmark.UploadMode = TestNodeRD.SubmitMode.CSharpOriginal;
		rdGDExtensionBenchmark.InstanceCount = InstanceCount;
		rdGDExtensionBenchmark.UploadMode = TestNodeRD.SubmitMode.GDExtensionSingleBuffer;
		// 两条 RenderingServer 路径同样只改变语言边界，位置纹理格式和实例数完全一致。
		rsCSharpBenchmark.MeshInstanceCount = InstanceCount;
		rsCSharpBenchmark.UseNativePlugin = false;
		rsCSharpBenchmark.UseCompactPositionTexture = true;
		rsGDExtensionBenchmark.MeshInstanceCount = InstanceCount;
		rsGDExtensionBenchmark.UseNativePlugin = true;
		rsGDExtensionBenchmark.UseCompactPositionTexture = true;
		// RenderingServer 位置纹理仍需要 GDExample 提供的原生桥接，但桥接节点本身不参与基准。
		rsGDExtensionBenchmark.NativeSubmitBridge = rsTextureNativeBridge;
		rsCppBenchmark.Set("MeshInstanceCount", InstanceCount);
		rsCppBenchmark.Set("CompactTextureBenchmark", true);

		for (int i = 0; i < benchmarkItems.Length; i++)
		{
			// 同一时刻只让一个节点执行物理帧，避免其他路径的逻辑更新和上传污染计时。
			benchmarkItems[i].ProcessMode = i == 0 ? ProcessModeEnum.Inherit : ProcessModeEnum.Disabled;
			benchmarkItems[i].Visible = i == 0;
		}
	}

	public override void _Ready()
	{
		results = GetNode<Label>("UI/Panel/Margin/VBox/Results");
		rdCSharpBenchmark.BenchmarkCompleted += OnRdCSharpSample;
		rdGDExtensionBenchmark.BenchmarkCompleted += OnRdGDExtensionSample;
		rsCSharpBenchmark.BenchmarkCompleted += OnRsCSharpSample;
		rsGDExtensionBenchmark.BenchmarkCompleted += OnRsGDExtensionSample;
		rsCppBenchmark.Connect("benchmark_completed",
			Callable.From<double, double, double, double, long>(OnRsCppSample));
		PrintStageStart();
		UpdateResults();
	}

	private void OnRdCSharpSample(double logic, double fill, double submit, double gpu, double total, long count)
	{
		if (stage == 0) AddSample(logic, fill, submit, gpu, total);
	}

	private void OnRdGDExtensionSample(double logic, double fill, double submit, double gpu, double total, long count)
	{
		if (stage == 2) AddSample(logic, fill, submit, gpu, total);
	}

	private void OnRsCSharpSample(double logic, double fill, double submit, double total, long count)
	{
		if (stage == 1) AddSample(logic, fill, submit, -1, total);
	}

	private void OnRsGDExtensionSample(double logic, double fill, double submit, double total, long count)
	{
		if (stage == 3) AddSample(logic, fill, submit, -1, total);
	}

	private void OnRsCppSample(double logic, double fill, double submit, double total, long count)
	{
		if (stage == 4) AddSample(logic, fill, submit, -1, total);
	}

	private void AddSample(double logic, double fill, double submit, double gpu, double total)
	{
		// 子 benchmark 已经完成预热和 60 帧平均；这里的 Count 表示“完整采样轮数”。
		Stats current = stats[stage];
		current.Add(logic, fill, submit, gpu, total);
		GD.Print($"[综合基准] 模式={names[stage]} 轮次={current.Count}/{SampleRounds} " +
			$"逻辑={logic:F4}ms 填充={fill:F4}ms 提交={submit:F4}ms 总计={total:F4}ms");
		UpdateResults();
		if (current.Count < SampleRounds) return;

		benchmarkItems[stage].ProcessMode = ProcessModeEnum.Disabled;
		benchmarkItems[stage].Visible = false;
		stage++;
		if (stage >= benchmarkItems.Length)
		{
			PrintSummary();
			UpdateResults();
			return;
		}
		Callable.From(StartNextStage).CallDeferred();
	}

	private void StartNextStage()
	{
		// 延迟到下一次主循环再启用节点，避免在 signal 回调栈中切换 ProcessMode。
		benchmarkItems[stage].Visible = true;
		benchmarkItems[stage].ProcessMode = ProcessModeEnum.Inherit;
		PrintStageStart();
		UpdateResults();
	}

	private void PrintStageStart() => GD.Print(
		$"[综合基准] 开始 模式={names[stage]} 实例={InstanceCount} 轮数={SampleRounds}");

	private void PrintSummary()
	{
		GD.Print($"[综合基准] 完成 实例={InstanceCount} 每轮=60帧 轮数={SampleRounds}");
		for (int i = 0; i < stats.Length; i++)
		{
			Stats value = stats[i];
			string gpu = value.GpuAvg < 0 ? "不可用" : $"{value.GpuAvg:F4}ms";
			string speedup = i == 0 || value.RenderAvg <= 0.0
				? "baseline"
				: $"{stats[0].RenderAvg / value.RenderAvg:F2}x";
			GD.Print($"[综合基准] 模式={names[i]} 逻辑={value.LogicAvg:F4}ms " +
				$"填充={value.FillAvg:F4}ms 提交={value.SubmitAvg:F4}ms " +
				$"CPU渲染准备={value.RenderAvg:F4}ms GPU={gpu} CPU总计={value.TotalAvg:F4}ms " +
				$"相对C#RD={speedup}");
			GD.Print($"[综合基准][提交链路] 模式={names[i]} 路径={submissionPaths[i]}");
		}

		// 横向比较 RD/RS，纵向比较 C#/GDExtension，组成完整的 2×2 路径矩阵。
		PrintComparison("C# submit path: RD vs RS", 0, 1);
		PrintComparison("GDExtension submit path: RD vs RS", 2, 3);
		PrintComparison("RD language boundary: C# vs GDExtension", 0, 2);
		PrintComparison("RS language boundary: C# vs GDExtension", 1, 3);
		PrintComparison("RS full native: C# vs pure C++", 1, 4);
		PrintComparison("RS native submit vs full native C++", 3, 4);
		foreach (string argument in OS.GetCmdlineUserArgs())
		{
			if (argument == "--benchmark-quit") { GetTree().Quit(); break; }
		}
	}

	private void PrintComparison(string comparisonName, int leftIndex, int rightIndex)
	{
		Stats left = stats[leftIndex];
		Stats right = stats[rightIndex];
		string ratioText = "unavailable";
		string percentText = "unavailable";
		if (left.RenderAvg > 0.0 && right.RenderAvg > 0.0)
		{
			double ratio = left.RenderAvg / right.RenderAvg;
			// 正数表示右侧路径耗时更少，负数表示右侧路径反而更慢。
			double fasterPercent = (left.RenderAvg - right.RenderAvg) / left.RenderAvg * 100.0;
			ratioText = $"{ratio:F2}x";
			percentText = $"{fasterPercent:+0.00;-0.00;0.00}%";
		}

		GD.Print(
			$"[综合基准][路径对比] 组={comparisonName} " +
			$"左={names[leftIndex]}(填充={left.FillAvg:F4}ms,提交={left.SubmitAvg:F4}ms," +
			$"CPU准备={left.RenderAvg:F4}ms,总计={left.TotalAvg:F4}ms) " +
			$"右={names[rightIndex]}(填充={right.FillAvg:F4}ms,提交={right.SubmitAvg:F4}ms," +
			$"CPU准备={right.RenderAvg:F4}ms,总计={right.TotalAvg:F4}ms) " +
			$"右相对左={ratioText},变化={percentText}");
	}

	private void UpdateResults()
	{
		string text = stage < names.Length ? $"正在测试：{names[stage]}" : "全部测试完成";
		text += $"\n实例：{InstanceCount:N0}，每项：{SampleRounds}轮 × 60帧\n";
		for (int i = 0; i < stats.Length; i++)
		{
			Stats value = stats[i];
			text += value.Count == 0
				? $"{names[i]}：等待\n"
				: $"{names[i]}：逻辑 {value.LogicAvg:F4} | 填充 {value.FillAvg:F4} | " +
				  $"提交 {value.SubmitAvg:F4} | CPU准备 {value.RenderAvg:F4} | 总计 {value.TotalAvg:F4} ms " +
				  $"({value.Count}/{SampleRounds})\n路径：{submissionPaths[i]}\n";
		}
		results.Text = text;
	}
}
