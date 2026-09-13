# Package-only qualification consumer

This project is the public-shape TUnit DX canary. It targets only
`netwasm0.1`, references only the aggregate `NetWasm.TUnit` package, and uses
the ordinary SDK-owned `VSTest` target through `dotnet test`. It has no project
reference, desktop target, `Microsoft.NET.Test.Sdk`, copied JavaScript host,
private tool path, or test-framework-owned execution target. Public consumers
restore the package from NuGet.org through normal NuGet configuration.
