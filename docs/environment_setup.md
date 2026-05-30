# Signo — Environment Setup

**Date:** 2026-05-25  
**Target OS:** Windows 11 / macOS / Ubuntu 24.04  
**Runtime:** .NET 8 SDK

---

## 1. Install .NET 8 SDK

### Windows / macOS
Download from: https://dotnet.microsoft.com/en-us/download/dotnet/8.0

### Ubuntu 24.04
```bash
sudo apt-get install -y dotnet-sdk-8.0
dotnet --version  # expected: 8.x.xxx
```

---

## 2. Clone the Repository

```bash
git clone https://github.com/YOUR_USERNAME/signo
cd signo
```

---

## 3. Download NuGet Packages (Manual)

NuGet access may be blocked in some environments.  
Download the following `.nupkg` files and place them in `local-packages/`.

### Direct download URLs

| Package | Version | URL |
|---|---|---|
| Microsoft.NET.Test.Sdk | 17.12.0 | https://api.nuget.org/v3-flatcontainer/microsoft.net.test.sdk/17.12.0/microsoft.net.test.sdk.17.12.0.nupkg |
| Microsoft.TestPlatform.TestHost | 17.12.0 | https://api.nuget.org/v3-flatcontainer/microsoft.testplatform.testhost/17.12.0/microsoft.testplatform.testhost.17.12.0.nupkg |
| Microsoft.TestPlatform.ObjectModel | 17.12.0 | https://api.nuget.org/v3-flatcontainer/microsoft.testplatform.objectmodel/17.12.0/microsoft.testplatform.objectmodel.17.12.0.nupkg |
| Microsoft.CodeCoverage | 17.12.0 | https://api.nuget.org/v3-flatcontainer/microsoft.codecoverage/17.12.0/microsoft.codecoverage.17.12.0.nupkg |
| NUnit | 4.2.2 | https://api.nuget.org/v3-flatcontainer/nunit/4.2.2/nunit.4.2.2.nupkg |
| NUnit3TestAdapter | 4.6.0 | https://api.nuget.org/v3-flatcontainer/nunit3testadapter/4.6.0/nunit3testadapter.4.6.0.nupkg |
| NUnit.Analyzers | 4.4.0 | https://api.nuget.org/v3-flatcontainer/nunit.analyzers/4.4.0/nunit.analyzers.4.4.0.nupkg |
| coverlet.collector | 6.0.2 | https://api.nuget.org/v3-flatcontainer/coverlet.collector/6.0.2/coverlet.collector.6.0.2.nupkg |
| System.Reflection.Metadata | 1.6.0 | https://api.nuget.org/v3-flatcontainer/system.reflection.metadata/1.6.0/system.reflection.metadata.1.6.0.nupkg |
| Newtonsoft.Json | 13.0.3 | https://api.nuget.org/v3-flatcontainer/newtonsoft.json/13.0.3/newtonsoft.json.13.0.3.nupkg |

### Place downloaded files

```
signo/
  local-packages/
    microsoft.net.test.sdk.17.12.0.nupkg
    microsoft.testplatform.testhost.17.12.0.nupkg
    microsoft.testplatform.objectmodel.17.12.0.nupkg
    microsoft.codecoverage.17.12.0.nupkg
    nunit.4.2.2.nupkg
    nunit3testadapter.4.6.0.nupkg
    nunit.analyzers.4.4.0.nupkg
    coverlet.collector.6.0.2.nupkg
    system.reflection.metadata.1.6.0.nupkg
    newtonsoft.json.13.0.3.nupkg
```

`nuget.config` is pre-configured to use `local-packages/` as the primary feed.

---

## 4. Restore and Build

```bash
dotnet restore Signo.sln
dotnet build Signo.sln
```

---

## 5. Run Tests

```bash
# All tests
dotnet test "Tests~/EditModeTests/Signo.Tests.EditMode.csproj"

# Verbose output
dotnet test "Tests~/EditModeTests/Signo.Tests.EditMode.csproj" --logger "console;verbosity=detailed"

# Exclude benchmarks
dotnet test "Tests~/EditModeTests/Signo.Tests.EditMode.csproj" --filter "Category!=Benchmark"

# Benchmarks only
dotnet test "Tests~/EditModeTests/Signo.Tests.EditMode.csproj" --filter "Category=Benchmark"
```

Expected output:
```
Passed Constructor_NotPowerOfTwo_ThrowsArgumentException
Passed Constructor_PowerOfTwo_DoesNotThrow
Passed Dequeue_WhenEmpty_ReturnsFalse
Passed EnqueueDequeue_ReturnsCorrectItem

Test Run Successful. Total tests: 4  Passed: 4
```

---

## 6. Project Structure

```
signo/
  Signo.sln
  nuget.config                          ← local-packages feed (nuget.org disabled)
  local-packages/                       ← place .nupkg files here (not committed)
  docs/
    project_proposal_v1.md
    development_plan_v1.md
    synthesizer_spec_v1.md
    class_and_method_design_v1.md
    environment_setup.md
  Scripts/                              ← Unity recognizes this directory
    Signo.Core.csproj                   ← for dotnet test (standalone)
    Signo.Core.asmdef                   ← for Unity (Germio integration)
    Audio/
      AudioRingBuffer.cs
    Synth/                              ← Phase 2+
    Filter/                             ← Phase 2+
    Effects/                            ← Phase 3+
    Preset/                             ← Phase 4+
  Tests~/                               ← Unity ignores folders ending with ~
    EditModeTests/
      Signo.Tests.EditMode.csproj
      Signo.Tests.EditMode.asmdef
      AudioRingBufferTests.cs
    MiniUnity/                          ← Phase 2+ Unity mock harness
```

---

## 7. Unity Integration (Germio)

`Scripts/Signo.Core.asmdef` allows Germio to reference Signo.Core directly.  
Unity-specific code is wrapped in `#if UNITY_5_3_OR_NEWER` — excluded from `dotnet test`.

---

## 8. IDE

Open `Signo.sln` with **Rider**, **Visual Studio 2022**, or **VS Code + C# Dev Kit**.

---

*© STUDIO MeowToon — MIT License*
