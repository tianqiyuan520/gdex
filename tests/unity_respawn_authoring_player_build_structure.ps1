$ErrorActionPreference = "Stop"

$sourcePath = "E:/UnityProject/RoadToDotsTutorials-main/Assets/EntitiesTutorials/Lesson10/Scripts/Authoring/RespawnControllerAuthoring.cs"
$source = Get-Content -Raw -Encoding UTF8 -LiteralPath $sourcePath

$classMarker = "public class RespawnControllerAuthoring"
$bakerMarker = "public class Baker : Baker<RespawnControllerAuthoring>"
$classIndex = $source.IndexOf($classMarker)
$bakerIndex = $source.IndexOf($bakerMarker)
$editorGuardIndex = $source.IndexOf("#if UNITY_EDITOR")
$editorEndIndex = $source.LastIndexOf("#endif")

if ($classIndex -lt 0) { throw "RespawnControllerAuthoring class not found." }
if ($bakerIndex -lt 0) { throw "RespawnControllerAuthoring.Baker class not found." }
if ($editorGuardIndex -lt $classIndex -or $editorGuardIndex -gt $bakerIndex) {
	throw "RespawnControllerAuthoring.Baker must be excluded from Player builds with UNITY_EDITOR."
}
if ($editorEndIndex -lt $bakerIndex) {
	throw "UNITY_EDITOR guard must close after RespawnControllerAuthoring.Baker."
}

Write-Host "PASS: Respawn authoring is excluded from Player compilation."
