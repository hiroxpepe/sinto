#!/usr/bin/env bash
# Copyright (c) STUDIO MeowToon. All rights reserved.
# Licensed under the MIT License.
#
# run_analysis.sh - one command (Linux/macOS/WSL/Git Bash): build & run the REAL
# Filter.cs, then visualize with Node. Mirror of run_analysis.ps1. No Python,
# no npm install. Charts always reflect the current Filter.cs.
#
#   ./run_analysis.sh
#
set -euo pipefail
cd "$(dirname "$0")"

DATA_DIR="data"
mkdir -p "$DATA_DIR"

echo "[1/2] building and running the real Filter.cs (Measure.cs harness)..."
dotnet run --project Analysis.csproj -c Release -- "$DATA_DIR"

echo "[2/2] visualizing measured output with Node..."
node visualize.js "$DATA_DIR" moog
node visualize.js "$DATA_DIR" roland

echo "done. see $DATA_DIR/sweep_speed_moog.svg and $DATA_DIR/sweep_speed_roland.svg"
