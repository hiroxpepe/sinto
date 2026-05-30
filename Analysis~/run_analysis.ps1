# Copyright (c) STUDIO MeowToon. All rights reserved.
# Licensed under the MIT License.
#
# run_analysis.ps1 - one command for Windows: build & run the REAL Filter.cs,
# then visualize its output with Node (zero dependencies).
#
# Because Analysis.csproj links the production sources directly, the charts
# always reflect the current Filter.cs. No Python, no npm install required.
#
#   powershell -ExecutionPolicy Bypass -File run_analysis.ps1
#
$ErrorActionPreference = "Stop"
Set-Location -Path $PSScriptRoot

$dataDir = "data"
New-Item -ItemType Directory -Force -Path $dataDir | Out-Null

Write-Host "[1/2] building and running the real Filter.cs (Measure.cs harness)..."
dotnet run --project Analysis.csproj -c Release -- $dataDir

Write-Host "[2/2] visualizing measured output with Node..."
node visualize.js $dataDir moog
node visualize.js $dataDir roland

Write-Host "done. see $dataDir\sweep_speed_moog.svg and $dataDir\sweep_speed_roland.svg"
