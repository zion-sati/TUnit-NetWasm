# TUnit on NetWasm

[![NetWasm publication](https://img.shields.io/github/actions/workflow/status/zion-sati/TUnit-NetWasm/netwasm-release.yml?label=NetWasm%20publish&event=release)](https://github.com/zion-sati/TUnit-NetWasm/actions/workflows/netwasm-release.yml)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue)](LICENSE)

This fork runs TUnit on `netwasm0.1`, with a source-generated test catalog,
sequential runner and VSTest adapter.

## Prerequisites

Follow the [NetWasm SDK quickstart](https://github.com/zion-sati/netwasm/blob/main/docs/sdk-quickstart.md).
The supported consumer floor is .NET SDK 10.0.300 or newer, Node.js 24 or newer
and LLD 24 or newer. The recommended setup installs and activates Emscripten SDK
6.0.7, which supplies the supported Node.js and LLD toolchain.

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
based on upstream TUnit `v1.68.4`; that upstream version is source provenance
and does not dictate the downstream package version.

## Maintainer releases

From a clean `main` checkout, with the public Git author and an approved signing
key configured, run `python3 eng/prepare-release.py --version VERSION`, replacing
`VERSION` with the next semantic version. It updates coordinated package versions,
creates a signed source commit/tag, and commits the matching release manifest.
The `netwasm-v` tag prefix is preserved. No manifest editing is needed; existing
tags and unapproved author metadata are rejected.

Review the output, push `main` and the printed tag atomically, then publish the
GitHub Release. Preparation does not build, test, push, or publish. The release
workflow validates the tagged source and exact package set before trusted
NuGet.org publishing. Publish the matching core NetWasm packages first.
