# NetWasm.TUnit.Templates

Creates an ordinary TUnit test project for the NetWasm target.

```bash
dotnet new install NetWasm.TUnit.Templates@1.66.27-rc.1
dotnet new netwasm-tunit -n MyProject.Tests
cd MyProject.Tests
dotnet test
```

The generated project uses `NetWasm.Sdk`, targets `netwasm0.1`, and references
only the aggregate `NetWasm.TUnit` package. NetWasm supplies the reusable
JavaScript host and local launcher; the template contains no copied host files.

See the [TUnit-NetWasm repository](https://github.com/zion-sati/TUnit-NetWasm)
for prerequisites and package documentation.
