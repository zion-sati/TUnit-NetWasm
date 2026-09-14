# TUnit on NetWasm

This fork packages TUnit as an ordinary consumer of the public NetWasm SDK,
runtime, hosting and generic VSTest support. It does not bundle the compiler,
copy the JavaScript host, introduce a custom runtime identifier or replace the
standard `dotnet test` command.

## Prerequisites

Follow the [NetWasm SDK quickstart](https://github.com/zion-sati/netwasm/blob/main/docs/sdk-quickstart.md).
The supported consumer floor is .NET SDK 10.0.300 or newer, Node.js 24 or newer
and LLD 24 or newer. The recommended setup installs and activates Emscripten SDK
6.0.7, which supplies the supported Node.js and LLD toolchain.

## Create and run a test project

Install the template package from NuGet.org, create a project, and use the
normal .NET test command:

```bash
dotnet new install NetWasm.TUnit.Templates@0.1.0-rc.1
dotnet new netwasm-tunit -n MyProject.Tests
cd MyProject.Tests
dotnet test
```

The generated project targets only `netwasm0.1` and has one package reference,
`NetWasm.TUnit`. Discovery, listing, filtering and execution use stock VSTest
commands:

```bash
dotnet test --list-tests
dotnet test --filter "FullyQualifiedName=MyProject.Tests.Tests.AnswerIsFortyTwo"
dotnet test --filter "Category=smoke"
```

The test package generates the managed `Main` entry point. NetWasm owns the
reusable JavaScript host modules and local launcher, so the template does not
copy JavaScript or introduce a second execution entry point.

## Packages

- `NetWasm.TUnit` is the aggregate package for normal test projects.
- `NetWasm.TUnit.Core`, `NetWasm.TUnit.Assertions` and
  `NetWasm.TUnit.Engine` are its component packages.
- `NetWasm.TUnit.Templates` supplies `dotnet new netwasm-tunit`.

All five packages use one coordinated NetWasm release version. This release is
based on upstream TUnit `v1.66.27`; that upstream version is source provenance
and does not dictate the downstream package version. Consumer packages are
distributed through NuGet.org and use normal NuGet configuration.
