# NetWasm.TUnit

TUnit support for ordinary NetWasm test projects. The package supplies the
managed generated-catalog runner and a standard VSTest adapter; NetWasm owns
the compiler, runtime, reusable JavaScript hosts, local launcher and VSTest
runtime provider.

Add the package to a project created with the NetWasm SDK:

```bash
dotnet add package NetWasm.TUnit --prerelease
```

Write tests with the normal TUnit APIs, then use the standard .NET test
commands:

```bash
dotnet test
dotnet test --list-tests
dotnet test --filter "FullyQualifiedName=MyProject.Tests.Tests.AnswerIsFortyTwo"
dotnet test --filter "Category=smoke"
dotnet test --no-build --no-restore
```

VSTest performs discovery and filtering through the packaged adapter. The
adapter invokes the generated guest catalog by stable ID and maps TUnit source,
trait, output, duration and result records back to normal VSTest test cases.
There is no NetWasm-specific test command, custom `VSTest` target, host script,
environment-variable transport or test-filter syntax.

NetWasm packages restore from NuGet.org through the machine's normal NuGet
configuration.

This package is maintained in the
[TUnit-NetWasm](https://github.com/zion-sati/TUnit-NetWasm) integration and is
MIT licensed.
