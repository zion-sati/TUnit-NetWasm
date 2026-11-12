# TUnit on NetWasm

[![NetWasm publication](https://img.shields.io/github/actions/workflow/status/zion-sati/TUnit-NetWasm/netwasm-release.yml?label=NetWasm%20publish&event=release)](https://github.com/zion-sati/TUnit-NetWasm/actions/workflows/netwasm-release.yml)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue)](LICENSE)

This fork runs TUnit on `netwasm0.1`: upstream TUnit's source generator, engine and
assertions, hosted for NetWasm, with a VSTest adapter for `dotnet test`.

## Prerequisites

Follow the [NetWasm SDK quickstart](https://github.com/zion-sati/netwasm/blob/main/docs/sdk-quickstart.md).
Install .NET SDK 10.0.303 or newer. Restoring the NetWasm packages supplies
the pinned native build tools for supported development hosts.

## Create and run a test project

```bash
dotnet new install NetWasm.TUnit.Templates
dotnet new netwasm-tunit -n MyProject.Tests
cd MyProject.Tests
dotnet test
```

The generated project targets only `netwasm0.1` and has one package reference,
`NetWasm.TUnit`. List or filter tests:

```bash
dotnet test --list-tests
dotnet test --filter "FullyQualifiedName=MyProject.Tests.Tests.AnswerIsFortyTwo"
dotnet test --filter "Category=smoke"
```

## How tests run

A `netwasm0.1` test project compiles TUnit's source-generated test metadata, and the
package's entry point runs TUnit's engine inside the test program, in place of
Microsoft.Testing.Platform's test application. Discovery, data sources, attributes,
hooks, retries, timeouts, parallel constraints and `TestContext.Current` therefore
behave as upstream's do. The VSTest adapter starts the program to list and run tests.

## Differences on `netwasm0.1`

- TUnit's reflection mode is not available, nor are the reflection fallbacks of its
  source-generated mode. A custom data source attribute's `DisplayName`, `Skip` and
  `Categories` properties are not read; TUnit's own attributes are unaffected.
- Test classes are treated as top-level types of the test program's assembly, so a
  nested test class is named without its enclosing class.
- Test projects build at `NetWasmOptimization=O0` for now: an optimized build of
  TUnit's engine exceeds WebAssembly's function size limit (NW1013). Pass
  `-p:NetWasmOptimization=O0` to a Release build.
- A large test project can stop at O0 with NW1014, "Cannot allocate a buffer of size
  2147483592": its compiled units exceed the compiler's 2 GB store. Build it with
  `-p:NetWasmCompiledUnits=false`, or split it.
- `Satisfies` on a collection item that is itself a collection, such as
  `LastItem().Satisfies(item => item.Count().IsEqualTo(3))` for a list of lists, does
  not compile yet (NW1003).
- Assertions that need reflection throw `PlatformNotSupportedException`: structural
  equivalence (`IsEquivalentTo` on objects), equality between different types
  through conversion operators, `IsParsableInto` and `WhenParsedInto`, `Type`
  visibility and nesting assertions, and `Exception.TargetSite`.
- `Member(x => x.Value, ...)` needs the member to be a primitive, an enum, a class or
  an array; a `DateTime` or another struct is not supported by NetWasm's expression
  interpreter.
- WASI has no threads, processes or Unix file permissions, and NetWasm uses invariant
  globalization, so assertions about those, and `CompletesWithin` on a delegate that
  blocks, do not apply. Exceptions in Release builds carry no stack trace unless the
  project sets `NetWasmManagedStackTrace=true`.

`eng/netwasm-assertion-tests.sh` runs upstream's TUnit.Assertions tests on
`netwasm0.1` and reports each failure class.

## Packages

[![NuGet: NetWasm.TUnit](https://img.shields.io/badge/NuGet-NetWasm.TUnit-004880?logo=nuget)](https://www.nuget.org/packages/NetWasm.TUnit)
[![NuGet: NetWasm.TUnit.Assertions](https://img.shields.io/badge/NuGet-NetWasm.TUnit.Assertions-004880?logo=nuget)](https://www.nuget.org/packages/NetWasm.TUnit.Assertions)
[![NuGet: NetWasm.TUnit.Core](https://img.shields.io/badge/NuGet-NetWasm.TUnit.Core-004880?logo=nuget)](https://www.nuget.org/packages/NetWasm.TUnit.Core)
[![NuGet: NetWasm.TUnit.Engine](https://img.shields.io/badge/NuGet-NetWasm.TUnit.Engine-004880?logo=nuget)](https://www.nuget.org/packages/NetWasm.TUnit.Engine)
[![NuGet: NetWasm.TUnit.Templates](https://img.shields.io/badge/NuGet-NetWasm.TUnit.Templates-004880?logo=nuget)](https://www.nuget.org/packages/NetWasm.TUnit.Templates)

- `NetWasm.TUnit` is the aggregate package for normal test projects.
- `NetWasm.TUnit.Core`, `NetWasm.TUnit.Assertions` and
  `NetWasm.TUnit.Engine` are its component packages.
- `NetWasm.TUnit.Templates` supplies `dotnet new netwasm-tunit`.

All five packages use one coordinated NetWasm release version. This release is
based on upstream TUnit `v1.83.8`; that upstream version is source provenance
and does not dictate the downstream package version.

## Maintainer releases

Publish a GitHub Release using a `netwasm-vVERSION` tag targeted at a signed
commit on `main`. The GitHub Release tag sets the package version; the release
workflow validates the tagged source and exact package set before trusted
NuGet.org publishing. Publish the matching core NetWasm packages first.
