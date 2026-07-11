$ErrorActionPreference = "Stop"

$sourcePath = "E:/UnityProject/RoadToDotsTutorials-main/Assets/EntitiesGraphicsTutorials/Lesson3/Scripts/BatchRendererGroupSetup.cs"
if (-not (Test-Path -LiteralPath $sourcePath)) {
	throw "Unity source file not found: $sourcePath"
}
$source = Get-Content -Raw -Encoding UTF8 -LiteralPath $sourcePath

function Assert-Contains {
	param([string]$Expected, [string]$Description)
	if (-not $source.Contains($Expected)) {
		throw "Missing: $Description (expected: $Expected)"
	}
}

Assert-Contains "System.Diagnostics.Stopwatch.GetTimestamp()" "high-resolution timestamp"
Assert-Contains "const int BenchmarkWarmupFrames = 60" "60-frame warmup"
Assert-Contains "const int BenchmarkSampleFrames = 60" "60-frame sample"
Assert-Contains "logicStartTicks" "logic start boundary"
Assert-Contains "fillStartTicks" "fill start boundary"
Assert-Contains "submitStartTicks" "submit start boundary"
Assert-Contains "frameEndTicks" "frame end boundary"
Assert-Contains 'RGBA32F={fillMs:F4}ms' "RGBA32F fill output"
Assert-Contains '={totalMs:F4}ms' "CPU total output"
Assert-Contains 'UnityEngine.Debug.Log' "unambiguous Unity logger"

Write-Host "PASS: Unity BRG benchmark logging structure matches the design."
