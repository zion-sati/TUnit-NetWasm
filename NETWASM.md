# TUnit on NetWasm

[![NetWasm publication](https://img.shields.io/github/actions/workflow/status/zion-sati/TUnit-NetWasm/netwasm-release.yml?label=NetWasm%20publish&event=release)](https://github.com/zion-sati/TUnit-NetWasm/actions/workflows/netwasm-release.yml)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue)](LICENSE)

This fork runs TUnit on `netwasm0.1`, with a source-generated test catalog,
sequential runner and VSTest adapter.

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

## Supported test attributes

A `netwasm0.1` test project runs a catalog that the TUnit source generator
builds at compile time. The catalog supports:

- Tests and data: `[Test]`, `[Arguments]`, `[MethodDataSource]`,
  `[ClassDataSource<T>]`, `[DisplayName]`, `[InheritsTests]` and
  `[GenerateGenericTest]`.
- Metadata and selection: `[Category]`, `[Property]`, `[Skip]`, `[Explicit]`
  and `[NotDiscoverable]`.
- Execution: `[DependsOn]`, `[ExecutionPriority]`, `[Repeat]`, `[Retry]` and
  `[Timeout]`.
- Parallelism: `[NotInParallel]`, `[ParallelGroup]` and
  `[ParallelLimiter<T>]`. The runner executes one test at a time, which
  satisfies every constraint. Tests keep TUnit's order: unconstrained tests,
  keyed `[NotInParallel]` tests, parallel groups, then global
  `[NotInParallel]` tests, each by `Order`.
- Hooks: test and class `[Before]` and `[After]` hooks, which may take a
  `CancellationToken`.

The build reports any other TUnit attribute as unsupported instead of
ignoring it. Features that need runtime reflection are not available.

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
based on upstream TUnit `v1.75.0`; that upstream version is source provenance
and does not dictate the downstream package version.

## Maintainer releases

Publish a GitHub Release using a `netwasm-vVERSION` tag targeted at a signed
commit on `main`. The GitHub Release tag sets the package version; the release
workflow validates the tagged source and exact package set before trusted
NuGet.org publishing. Publish the matching core NetWasm packages first.
