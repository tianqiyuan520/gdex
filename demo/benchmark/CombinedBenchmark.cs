using Godot;

public partial class CombinedBenchmark : Node2D
{
	[Export(PropertyHint.Range, "1,1000000,1")]
	public int InstanceCount { get; set; } = 20_000;

	private Label summaryLabel;
	private double cppLogicMs;
	private double cppFillMs;
	private double cppSubmitMs;
	private double cppTotalMs;
	private double csharpLogicMs;
	private double csharpFillMs;
	private double csharpSubmitMs;
	private double csharpTotalMs;
	private int cppSamples;
	private int csharpSamples;
	private int reportedSamples;

	public override void _EnterTree()
	{
		InstanceCount = System.Math.Max(1, InstanceCount);
		GetNode("CppBenchmark").Set("MeshInstanceCount", InstanceCount);
		TestNodeNormal csharp = GetNode<TestNodeNormal>("CSharpBenchmark");
		csharp.MeshInstanceCount = InstanceCount;
		csharp.NativeSubmitBridge = GetNode("CppBenchmark");
	}

	public override void _Ready()
	{
		summaryLabel = GetNode<Label>("UI/Panel/Margin/VBox/Results");
		GetNode("CppBenchmark").Connect(
			"benchmark_completed",
			Callable.From<double, double, double, double, long>(OnCppBenchmarkCompleted));
		GetNode<TestNodeNormal>("CSharpBenchmark").BenchmarkCompleted += OnCSharpBenchmarkCompleted;
		UpdateSummary();
	}

	private void OnCppBenchmarkCompleted(
		double logicMs,
		double fillMs,
		double submitMs,
		double totalMs,
		long instanceCount)
	{
		cppLogicMs = logicMs;
		cppFillMs = fillMs;
		cppSubmitMs = submitMs;
		cppTotalMs = totalMs;
		cppSamples++;
		UpdateSummary();
	}

	private void OnCSharpBenchmarkCompleted(
		double logicMs,
		double fillMs,
		double submitMs,
		double totalMs,
		long instanceCount)
	{
		csharpLogicMs = logicMs;
		csharpFillMs = fillMs;
		csharpSubmitMs = submitMs;
		csharpTotalMs = totalMs;
		csharpSamples++;
		UpdateSummary();
	}

	private void UpdateSummary()
	{
		string ratio = cppTotalMs > 0.0 && csharpTotalMs > 0.0
			? $"C# / C++: {csharpTotalMs / cppTotalMs:F2}x"
			: "C# / C++: waiting for samples";

		summaryLabel.Text =
			$"Instances: {InstanceCount:N0} | buffer mode | 60 warmup + 60 sample frames\n" +
			$"C++  logic {cppLogicMs:F4} | fill {cppFillMs:F4} | submit {cppSubmitMs:F4} | total {cppTotalMs:F4} ms | samples {cppSamples}\n" +
			$"C#   logic {csharpLogicMs:F4} | fill {csharpFillMs:F4} | submit {csharpSubmitMs:F4} | total {csharpTotalMs:F4} ms | samples {csharpSamples}\n" +
			ratio;

		int completedSamples = System.Math.Min(cppSamples, csharpSamples);
		if (completedSamples > reportedSamples)
		{
			reportedSamples = completedSamples;
			GD.Print(
				$"[Benchmark][Combined] sample={reportedSamples} instances={InstanceCount} " +
				$"cpp_total_ms={cppTotalMs:F4} csharp_total_ms={csharpTotalMs:F4} " +
				$"csharp_over_cpp={csharpTotalMs / cppTotalMs:F2}x");
		}
	}
}
