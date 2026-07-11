using Godot;
using System;

public partial class SequentialBenchmark : Node2D
{
	private enum BenchmarkStage
	{
		CSharp,
		Transition,
		Cpp,
		Completed
	}

	private sealed class SampleStats
	{
		public int Count { get; private set; }
		public double LogicSum { get; private set; }
		public double FillSum { get; private set; }
		public double SubmitSum { get; private set; }
		public double TotalSum { get; private set; }
		public double TotalMin { get; private set; } = double.MaxValue;
		public double TotalMax { get; private set; }
		public double RenderMin { get; private set; } = double.MaxValue;
		public double RenderMax { get; private set; }

		public double LogicAverage => Count == 0 ? 0.0 : LogicSum / Count;
		public double FillAverage => Count == 0 ? 0.0 : FillSum / Count;
		public double SubmitAverage => Count == 0 ? 0.0 : SubmitSum / Count;
		public double TotalAverage => Count == 0 ? 0.0 : TotalSum / Count;
		public double RenderAverage => FillAverage + SubmitAverage;

		public void Add(double logicMs, double fillMs, double submitMs, double totalMs)
		{
			Count++;
			LogicSum += logicMs;
			FillSum += fillMs;
			SubmitSum += submitMs;
			TotalSum += totalMs;
			TotalMin = Math.Min(TotalMin, totalMs);
			TotalMax = Math.Max(TotalMax, totalMs);
			double renderMs = fillMs + submitMs;
			RenderMin = Math.Min(RenderMin, renderMs);
			RenderMax = Math.Max(RenderMax, renderMs);
		}
	}

	[Export(PropertyHint.Range, "1,1000000,1")]
	public int InstanceCount { get; set; } = 100_000;

	[Export(PropertyHint.Range, "1,20,1")]
	public int SampleRounds { get; set; } = 5;

	private Node2D cppBenchmark;
	private TestNodeNormal csharpBenchmark;
	private Label resultsLabel;
	private BenchmarkStage stage = BenchmarkStage.CSharp;
	private readonly SampleStats cppStats = new();
	private readonly SampleStats csharpStats = new();

	public override void _EnterTree()
	{
		InstanceCount = Math.Max(1, InstanceCount);
		SampleRounds = Math.Max(1, SampleRounds);
		cppBenchmark = GetNode<Node2D>("CppBenchmark");
		csharpBenchmark = GetNode<TestNodeNormal>("CSharpBenchmark");

		cppBenchmark.Set("MeshInstanceCount", InstanceCount);
		csharpBenchmark.MeshInstanceCount = InstanceCount;
		csharpBenchmark.NativeSubmitBridge = cppBenchmark;
		csharpBenchmark.UseCompactPositionTexture = true;
		cppBenchmark.ProcessMode = ProcessModeEnum.Disabled;
		cppBenchmark.Visible = false;
		csharpBenchmark.ProcessMode = ProcessModeEnum.Inherit;
		csharpBenchmark.Visible = true;
	}

	public override void _Ready()
	{
		resultsLabel = GetNode<Label>("UI/Panel/Margin/VBox/Results");
		cppBenchmark.Connect(
			"benchmark_completed",
			Callable.From<double, double, double, double, long>(OnCppBenchmarkCompleted));
		csharpBenchmark.BenchmarkCompleted += OnCSharpBenchmarkCompleted;
		GD.Print(
			$"[Benchmark][Sequential] stage=C# started instances={InstanceCount} " +
			$"sample_rounds={SampleRounds}");
		UpdateResults();
	}

	private void OnCSharpBenchmarkCompleted(
		double logicMs,
		double fillMs,
		double submitMs,
		double totalMs,
		long instanceCount)
	{
		if (stage != BenchmarkStage.CSharp)
		{
			return;
		}

		csharpStats.Add(logicMs, fillMs, submitMs, totalMs);
		GD.Print(
			$"[Benchmark][Sequential] stage=C# sample={csharpStats.Count}/{SampleRounds} " +
			$"total_ms={totalMs:F4}");
		UpdateResults();
		if (csharpStats.Count < SampleRounds)
		{
			return;
		}

		csharpBenchmark.ProcessMode = ProcessModeEnum.Disabled;
		csharpBenchmark.Visible = false;
		stage = BenchmarkStage.Transition;
		GD.Print(
			$"[Benchmark][Sequential] stage=C# completed " +
			$"average_ms={csharpStats.TotalAverage:F4}");
		UpdateResults();
		Callable.From(StartCppBenchmark).CallDeferred();
	}

	private void StartCppBenchmark()
	{
		if (stage != BenchmarkStage.Transition)
		{
			return;
		}

		stage = BenchmarkStage.Cpp;
		cppBenchmark.Visible = true;
		cppBenchmark.ProcessMode = ProcessModeEnum.Inherit;
		GD.Print(
			$"[Benchmark][Sequential] stage=C++ started instances={InstanceCount} " +
			$"sample_rounds={SampleRounds}");
		UpdateResults();
	}

	private void OnCppBenchmarkCompleted(
		double logicMs,
		double fillMs,
		double submitMs,
		double totalMs,
		long instanceCount)
	{
		if (stage != BenchmarkStage.Cpp)
		{
			return;
		}

		cppStats.Add(logicMs, fillMs, submitMs, totalMs);
		GD.Print(
			$"[Benchmark][Sequential] stage=C++ sample={cppStats.Count}/{SampleRounds} " +
			$"total_ms={totalMs:F4}");
		UpdateResults();
		if (cppStats.Count < SampleRounds)
		{
			return;
		}

		cppBenchmark.ProcessMode = ProcessModeEnum.Disabled;
		stage = BenchmarkStage.Completed;
		GD.Print(
			$"[Benchmark][Sequential] completed instances={InstanceCount} samples={SampleRounds} " +
			$"cpp_logic_avg_ms={cppStats.LogicAverage:F4} csharp_logic_avg_ms={csharpStats.LogicAverage:F4} " +
			$"cpp_render_avg_ms={cppStats.RenderAverage:F4} csharp_render_avg_ms={csharpStats.RenderAverage:F4} " +
			$"csharp_render_over_cpp={csharpStats.RenderAverage / cppStats.RenderAverage:F2}x");
		UpdateResults();
		foreach (string argument in OS.GetCmdlineUserArgs())
		{
			if (argument == "--benchmark-quit") { GetTree().Quit(); break; }
		}
	}

	private void UpdateResults()
	{
		string stageText = stage switch
		{
			BenchmarkStage.CSharp => "Running C# (C++ disabled)",
			BenchmarkStage.Transition => "Switching stages",
			BenchmarkStage.Cpp => "Running C++ (C# disabled)",
			_ => "Completed (both disabled)"
		};
		string ratio = cppStats.Count > 0
			? $"C# / C++ render average: {csharpStats.RenderAverage / cppStats.RenderAverage:F2}x"
			: "C# / C++ render average: waiting for C++";

		resultsLabel.Text =
			$"Stage: {stageText}\n" +
			$"Instances: {InstanceCount:N0} | rounds: {SampleRounds} | each round: 60 frames\n" +
			FormatStats("C# ", csharpStats) + "\n" +
			FormatStats("C++", cppStats) + "\n" +
			ratio;
	}

	private string FormatStats(string name, SampleStats stats)
	{
		if (stats.Count == 0)
		{
			return $"{name}: waiting";
		}
		return
			$"{name}: logic {stats.LogicAverage:F4} | fill {stats.FillAverage:F4} | submit {stats.SubmitAverage:F4}\n" +
			$"    render avg/min/max {stats.RenderAverage:F4}/{stats.RenderMin:F4}/{stats.RenderMax:F4} ms | " +
			$"total avg {stats.TotalAverage:F4} ms " +
			$"({stats.Count}/{SampleRounds})";
	}
}
