# NetWasm.TUnit.Templates

Creates a TUnit test project for the NetWasm target.

```bash
dotnet new install NetWasm.TUnit.Templates
dotnet new netwasm-tunit -n MyProject.Tests
cd MyProject.Tests
dotnet test
```

The generated project uses `NetWasm.Sdk`, targets `netwasm0.1`, and references
only the aggregate `NetWasm.TUnit` package.

See the [TUnit-NetWasm repository](https://github.com/zion-sati/TUnit-NetWasm)
for prerequisites and package documentation.
