#!/usr/bin/env bash
# Runs upstream's TUnit.Assertions tests on netwasm0.1 through the TUnit engine and reports each failure class.
#
# Usage: eng/netwasm-assertion-tests.sh [package-dir]
#   package-dir                       holds NetWasm.TUnit and NetWasm.App packages (default artifacts/netwasm-packages)
#   NETWASM_TUNIT_TEST_VERSION        the NetWasm.TUnit version to test (default eng/NetWasm.ReleaseVersion.txt)
#   NETWASM_TUNIT_TEST_OPTIMIZATION   the NetWasm preset (default O0)
#   NETWASM_TUNIT_ASSERTION_SHARDS    the number of test projects the files are split across (default 4)
#   NETWASM_TUNIT_ASSERTION_OUTPUT    keeps the projects and logs there instead of a temporary directory
#
# The suite is split across several projects so each compiles in a reasonable time and memory. Files listed in
# eng/netwasm-assertion-exclusions.txt are left out, each with its reason. The script fails when a project does not
# build or run; failing tests are reported, not fatal, so a run measures how much of the suite passes.

set -euo pipefail

REPOSITORY_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd -P)"
PACKAGE_DIR="$(cd "${1:-${REPOSITORY_ROOT}/artifacts/netwasm-packages}" && pwd -P)"
TEST_VERSION="${NETWASM_TUNIT_TEST_VERSION:-$(tr -d '[:space:]' < "${REPOSITORY_ROOT}/eng/NetWasm.ReleaseVersion.txt")}"
OPTIMIZATION="${NETWASM_TUNIT_TEST_OPTIMIZATION:-O0}"
SHARDS="${NETWASM_TUNIT_ASSERTION_SHARDS:-4}"
if [[ ! "${OPTIMIZATION}" =~ ^(None|O0|O1|O2|O3|Os|Oz)$ ]]; then
  echo "NETWASM_TUNIT_TEST_OPTIMIZATION must be None, O0, O1, O2, O3, Os or Oz." >&2
  exit 2
fi

if [[ -n "${NETWASM_TUNIT_ASSERTION_OUTPUT:-}" ]]; then
  run_root="$(mkdir -p "${NETWASM_TUNIT_ASSERTION_OUTPUT}" && cd "${NETWASM_TUNIT_ASSERTION_OUTPUT}" && pwd -P)"
else
  run_root="$(mktemp -d "${TMPDIR:-/tmp}/netwasm-tunit-assertions.XXXXXX")"
  trap 'rm -rf "${run_root}"' EXIT
fi
export NUGET_PACKAGES="${NUGET_PACKAGES:-${run_root}/packages}"
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1

cp "${REPOSITORY_ROOT}/packaging/global.json" "${run_root}/global.json"
cat > "${run_root}/NuGet.Config" <<EOF
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="netwasm-tunit" value="${PACKAGE_DIR}" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
  </packageSources>
</configuration>
EOF

python3 - "${REPOSITORY_ROOT}" "${run_root}" "${SHARDS}" "${TEST_VERSION}" <<'PY'
import sys
from pathlib import Path
from xml.sax.saxutils import quoteattr

repository, run_root, shards, version = Path(sys.argv[1]), Path(sys.argv[2]), int(sys.argv[3]), sys.argv[4]
tests = repository / 'tests' / 'TUnit.Assertions.Tests'
excluded = {
    line.strip()
    for line in (repository / 'eng' / 'netwasm-assertion-exclusions.txt').read_text().splitlines()
    if line.strip() and not line.lstrip().startswith('#')
}
missing = sorted(name for name in excluded if not (tests / name).is_file())
if missing:
    sys.exit(f'Excluded files that no longer exist: {", ".join(missing)}')

# Files without tests (global usings, helpers) go into every project; test files are spread by size.
shared, files = [], []
for path in sorted(tests.rglob('*.cs')):
    relative = path.relative_to(tests).as_posix()
    if relative.split('/')[0] in ('bin', 'obj') or relative in excluded:
        continue
    (files if '[Test' in path.read_text(errors='replace') else shared).append(path)
files.sort(key=lambda path: -path.stat().st_size)
buckets, sizes = [[] for _ in range(shards)], [0] * shards
for path in files:
    index = sizes.index(min(sizes))
    buckets[index].append(path)
    sizes[index] += path.stat().st_size

props = repository / 'packaging' / 'NetWasmUpstreamSources.props'
for index, bucket in enumerate(buckets):
    project = run_root / f'Shard{index}'
    project.mkdir(parents=True, exist_ok=True)
    items = '\n'.join(f'    <Compile Include={quoteattr(str(path))} />' for path in shared + bucket)
    (project / f'Shard{index}.csproj').write_text(f'''<Project Sdk="NetWasm.Sdk">
  <PropertyGroup>
    <TargetFramework>netwasm0.1</TargetFramework>
    <IsTestProject>true</IsTestProject>
    <Nullable>enable</Nullable>
    <LangVersion>preview</LangVersion>
    <TreatWarningsAsErrors>false</TreatWarningsAsErrors>
    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
    <RootNamespace>TUnit.Assertions.Tests</RootNamespace>
  </PropertyGroup>
  <Import Project={quoteattr(str(props))} />
  <ItemGroup>
    <PackageReference Include="NetWasm.TUnit" Version="[{version}]" />
  </ItemGroup>
  <ItemGroup>
{items}
  </ItemGroup>
</Project>
''')
    print(f'Shard{index}: {len(bucket)} test files')
PY

properties=(-c Release "-p:NetWasmOptimization=${OPTIMIZATION}")
summary="${run_root}/summary.txt"
: > "${summary}"
status=0
cd "${run_root}"
for (( index = 0; index < SHARDS; index++ )); do
  project="${run_root}/Shard${index}"
  if ! dotnet build "${project}" "${properties[@]}" --nologo -v q > "${project}/build.log" 2>&1; then
    echo "Shard${index} did not build:" >&2
    grep -E " error " "${project}/build.log" | sed -E 's# \[[^]]*\]$##' | sort -u | head -5 >&2
    status=1
    continue
  fi
  dotnet test "${project}" "${properties[@]}" --no-build --nologo > "${project}/test.log" 2>&1 || true
  line="$(grep -E '^(Passed|Failed)!' "${project}/test.log" | tail -1 || true)"
  if [[ -z "${line}" ]]; then
    echo "Shard${index} did not report a result; see ${project}/test.log." >&2
    status=1
    continue
  fi
  echo "Shard${index}: ${line}" | tee -a "${summary}"
done

python3 - "${run_root}" "${SHARDS}" <<'PY'
import re, sys
from collections import Counter
from pathlib import Path

run_root, shards = Path(sys.argv[1]), int(sys.argv[2])
totals, classes = Counter(), Counter()
for index in range(shards):
    log = run_root / f'Shard{index}' / 'test.log'
    if not log.is_file():
        continue
    text = log.read_text(errors='replace')
    for line in text.splitlines():
        match = re.search(r'Failed:\s*(\d+), Passed:\s*(\d+), Skipped:\s*(\d+), Total:\s*(\d+)', line)
        if match:
            for key, value in zip(('failed', 'passed', 'skipped', 'total'), match.groups()):
                totals[key] += int(value)
    # The first line of each failure's message, with numbers and type arguments folded so one cause is one class.
    for message in re.findall(r'Error Message:\n\s*(.+)', text):
        message = re.sub(r'`\d+<.*', '`N<...>', message.strip())
        classes[re.sub(r'\d+', 'N', message)[:150]] += 1
print(f"Total {totals['total']}: {totals['passed']} passed, {totals['failed']} failed, {totals['skipped']} skipped")
print('Failure classes:')
for message, count in classes.most_common():
    print(f'{count:6}  {message}')
PY
exit "${status}"
