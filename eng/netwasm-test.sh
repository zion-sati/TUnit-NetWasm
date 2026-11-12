#!/usr/bin/env bash

set -euo pipefail

REPOSITORY_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd -P)"
PACKAGE_DIR="${1:-${REPOSITORY_ROOT}/artifacts/netwasm-packages}"
TEST_VERSION="${NETWASM_TUNIT_TEST_VERSION:-$(tr -d '[:space:]' < "${REPOSITORY_ROOT}/eng/NetWasm.ReleaseVersion.txt")}"
mkdir -p "${PACKAGE_DIR}"
PACKAGE_DIR="$(cd "${PACKAGE_DIR}" && pwd -P)"

# The NetWasm projects build at the SDK's default preset unless a lane names
# one and, optionally, a collector; MSBuild reads both as properties.
if [[ -n "${NETWASM_TUNIT_TEST_OPTIMIZATION:-}" ]]; then
  if [[ ! "${NETWASM_TUNIT_TEST_OPTIMIZATION}" =~ ^(None|O0|O1|O2|O3|Os|Oz)$ ]]; then
    echo "NETWASM_TUNIT_TEST_OPTIMIZATION must be None, O0, O1, O2, O3, Os or Oz." >&2
    exit 2
  fi
  export NetWasmOptimization="${NETWASM_TUNIT_TEST_OPTIMIZATION}"
fi
if [[ -n "${NETWASM_TUNIT_TEST_COLLECTOR:-}" ]]; then
  if [[ ! "${NETWASM_TUNIT_TEST_COLLECTOR}" =~ ^(Compact|NetWasm)$ ]]; then
    echo "NETWASM_TUNIT_TEST_COLLECTOR must be Compact or NetWasm." >&2
    exit 2
  fi
  export NetWasmGarbageCollector="${NETWASM_TUNIT_TEST_COLLECTOR}"
fi

test_root="$(mktemp -d "${TMPDIR:-/tmp}/netwasm-tunit-test.XXXXXX")"
test_root="$(cd "${test_root}" && pwd -P)"
cleanup() {
  if [[ -d "${test_root}/source" ]]; then
    git -C "${REPOSITORY_ROOT}" -c core.longpaths=true worktree remove --force "${test_root}/source" >/dev/null 2>&1 || true
  fi
  rm -rf "${test_root}"
}
trap cleanup EXIT

export DOTNET_CLI_HOME="${test_root}/dotnet-home"
export NUGET_PACKAGES="${NUGET_PACKAGES:-${test_root}/packages}"
export NUGET_HTTP_CACHE_PATH="${test_root}/http-cache"
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1
mkdir -p "${DOTNET_CLI_HOME}" "${NUGET_PACKAGES}" "${NUGET_HTTP_CACHE_PATH}"

if [[ -n "${NETWASM_TUNIT_EXPECT_DOTNET_ROOT:-}" ]]; then
  isolated_root="$(cd "${NETWASM_TUNIT_EXPECT_DOTNET_ROOT}" && pwd -P)"
  expected_dotnet="${isolated_root}/dotnet"
  [[ -f "${expected_dotnet}" ]] || expected_dotnet="${isolated_root}/dotnet.exe"
  expected_dotnet="$(realpath "${expected_dotnet}")"
  actual_dotnet="$(realpath "$(command -v dotnet)")"
  [[ "${actual_dotnet}" == "${expected_dotnet}" ]] || {
    echo "TUnit qualification did not select the isolated dotnet host." >&2
    exit 1
  }
  [[ "$(dotnet --list-sdks | wc -l | tr -d ' ')" == 1 ]] || {
    echo "TUnit qualification dotnet root contains more than one SDK." >&2
    exit 1
  }
  expected_major="${NETWASM_TUNIT_EXPECT_SDK_VERSION%%.*}"
  if dotnet --list-runtimes | awk '{ print $2 }' | cut -d. -f1 | grep -Fvxq "${expected_major}"; then
    echo "TUnit qualification dotnet root contains another runtime major." >&2
    exit 1
  fi
  dotnet --list-sdks
  dotnet --list-runtimes
fi

assert_vstest_pass_summary() {
  local log_file="$1"
  local expected_count="$2"
  grep -Eq "Failed:[[:space:]]*0([^0-9]|$)" "${log_file}" &&
    grep -Eq "Passed:[[:space:]]*${expected_count}([^0-9]|$)" "${log_file}" &&
    grep -Eq "Total:[[:space:]]*${expected_count}([^0-9]|$)" "${log_file}" || {
      echo "Expected ${expected_count} passing VSTest tests in ${log_file}." >&2
      exit 1
    }
}

build_arguments=(--version "${TEST_VERSION}" --output "${PACKAGE_DIR}")
if [[ -n "${NETWASM_TUNIT_BUILD_SDK_VERSION:-}" ]]; then
  build_arguments+=(--sdk-version "${NETWASM_TUNIT_BUILD_SDK_VERSION}")
fi
"${REPOSITORY_ROOT}/eng/netwasm-build-packages.sh" "${build_arguments[@]}"

qualification_root="${test_root}/source"
# A detached worktree, not an archive: the source generator tests locate
# the repository through Git (Sourcy.Git).
git -C "${REPOSITORY_ROOT}" -c core.longpaths=true worktree add --quiet --detach "${qualification_root}" HEAD
if [[ -n "${NETWASM_TUNIT_BUILD_SDK_VERSION:-}" ]]; then
  python3 - "${qualification_root}" "${NETWASM_TUNIT_BUILD_SDK_VERSION}" <<'PY'
import json, sys
from pathlib import Path

root, version = Path(sys.argv[1]), sys.argv[2]
for name in ('global.json', 'packaging/global.json'):
    path = root / name
    value = json.loads(path.read_text())
    value['sdk'].update(version=version, rollForward='disable', allowPrerelease=True)
    path.write_text(json.dumps(value, indent=2) + '\n')
PY
fi
# SDK resolution starts from the process working directory. Keep every
# qualification command inside the disposable source whose global.json was
# projected to the isolated SDK above.
cd "${qualification_root}"

nuget_config="${test_root}/NuGet.Config"
xml_escape() {
  printf '%s' "$1" | sed -e 's/&/\&amp;/g' -e 's/"/\&quot;/g' -e 's/</\&lt;/g' -e 's/>/\&gt;/g'
}
# dotnet on Windows reads NuGet.Config paths natively, not as Git Bash paths.
native_path() {
  if [[ -n "$1" ]] && command -v cygpath >/dev/null 2>&1; then
    cygpath -w "$1"
  else
    printf '%s' "$1"
  fi
}
package_dir_xml="$(xml_escape "$(native_path "${PACKAGE_DIR}")")"
printf '%s\n' \
  '<?xml version="1.0" encoding="utf-8"?>' \
  '<configuration>' \
  '  <packageSources>' \
  '    <clear />' \
  "    <add key=\"current-build\" value=\"${package_dir_xml}\" />" \
  '    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />' \
  '  </packageSources>' \
  '</configuration>' > "${nuget_config}"

# The closed-world catalog generator and the catalog model run inside desktop
# builds, so their tests use upstream TUnit on the host runtime. Upstream CI
# passes TUnit's version to every build; without it, a GitHub Actions build of
# strong-named TUnit.Core is 1.0.0.0, below the version Verify.TUnit
# references. Use the high version Directory.Build.props gives local builds.
upstream_test_version=(-p:Version=99.99.99 -p:AssemblyVersion=99.99.99.0 -p:FileVersion=99.99.99.0)
catalog_projects=(tests/TUnit.UnitTests/TUnit.UnitTests.csproj)
# The generator tests include upstream's Verify snapshots, which are keyed by
# the runtime that runs them and exist for .NET 10. Lanes with only a newer
# runtime skip them; every OS still runs them on its .NET 10 lane.
if dotnet --list-runtimes | grep -q '^Microsoft\.NETCore\.App 10\.'; then
  catalog_projects=(tests/TUnit.Core.SourceGenerator.Tests/TUnit.Core.SourceGenerator.Tests.csproj "${catalog_projects[@]}")
else
  echo "Skipping TUnit.Core.SourceGenerator.Tests: its snapshots need the .NET 10 runtime."
fi
for catalog_tests in "${catalog_projects[@]}"; do
  dotnet restore "${qualification_root}/${catalog_tests}" \
    --configfile "${nuget_config}" \
    --disable-build-servers \
    --nologo \
    "${upstream_test_version[@]}"
  dotnet run --project "${qualification_root}/${catalog_tests}" \
    -c Release \
    -f net10.0 \
    --no-restore \
    --no-launch-profile \
    --disable-build-servers \
    "${upstream_test_version[@]}"
done

runner_tests="${qualification_root}/packaging/NetWasm.TUnit.Runner.Tests/NetWasm.TUnit.Runner.Tests.csproj"
dotnet restore "${runner_tests}" \
  --configfile "${nuget_config}" \
  --disable-build-servers \
  --nologo \
  -p:NetWasmTUnitPackageVersion="${TEST_VERSION}"
dotnet run --project "${runner_tests}" \
  -c Release \
  --no-restore \
  --no-launch-profile \
  --disable-build-servers \
  -p:NetWasmTUnitPackageVersion="${TEST_VERSION}" \
  -- --minimum-expected-tests 39

desktop_package_tests="${qualification_root}/packaging/NetWasm.TUnit.Desktop.Package.Tests/NetWasm.TUnit.Desktop.Package.Tests.csproj"
dotnet restore "${desktop_package_tests}" \
  --configfile "${nuget_config}" \
  --disable-build-servers \
  --nologo \
  -p:NetWasmTUnitPackageVersion="${TEST_VERSION}"
dotnet run --project "${desktop_package_tests}" \
  -c Release \
  --no-restore \
  --no-launch-profile \
  --disable-build-servers \
  -p:NetWasmTUnitPackageVersion="${TEST_VERSION}"

package_tests="NetWasm.TUnit.Package.Tests/NetWasm.TUnit.Package.Tests.csproj"
(
  cd "${qualification_root}/packaging"
  dotnet restore "${package_tests}" --configfile "${nuget_config}" --disable-build-servers --nologo \
    -p:NetWasmTUnitPackageVersion="${TEST_VERSION}"
  dotnet test "${package_tests}" -c Release --no-restore --disable-build-servers --nologo |
    tee "${test_root}/package-run.log"
  assert_vstest_pass_summary "${test_root}/package-run.log" 18
  dotnet test "${package_tests}" -c Release --no-build --no-restore --list-tests --nologo |
    tee "${test_root}/package-list.log"
  grep -E -q '^[[:space:]]+PackageOnlyConsumerRunsThroughStandardDotNetTest$' \
    "${test_root}/package-list.log"
  grep -E -q '^[[:space:]]+FilterCanaryPreservesTraits$' \
    "${test_root}/package-list.log"
  dotnet test "${package_tests}" -c Release --no-build --no-restore \
    --filter "FullyQualifiedName=NetWasm.TUnit.Package.Tests.PackageSmokeTests.PackageOnlyConsumerRunsThroughStandardDotNetTest" \
    --nologo | tee "${test_root}/package-fqn.log"
  assert_vstest_pass_summary "${test_root}/package-fqn.log" 1
  dotnet test "${package_tests}" -c Release --no-build --no-restore \
    --filter "Category=filter-canary" \
    --nologo | tee "${test_root}/package-category.log"
  assert_vstest_pass_summary "${test_root}/package-category.log" 1
  dotnet test "${package_tests}" -c Release --no-build --no-restore \
    --filter "TestCategory=filter-canary" \
    --nologo | tee "${test_root}/package-test-category.log"
  assert_vstest_pass_summary "${test_root}/package-test-category.log" 1
)

template_package="$(find "${PACKAGE_DIR}" -maxdepth 1 -type f -name 'NetWasm.TUnit.Templates.*.nupkg' -print -quit)"
if [[ -z "${template_package}" ]]; then
  echo "NetWasm.TUnit.Templates package was not produced." >&2
  exit 1
fi
template_hive="${test_root}/template-hive"
template_consumer="${test_root}/Generated.Tests"
dotnet new install "${template_package}" --debug:custom-hive "${template_hive}"
dotnet new netwasm-tunit \
  -n Generated.Tests \
  -o "${template_consumer}" \
  --debug:custom-hive "${template_hive}"
if [[ -e "${template_consumer}/global.json" ]]; then
  echo "The template selects an SDK; it should leave SDK selection to the user's dotnet." >&2
  exit 1
fi
if [[ -n "${NETWASM_TUNIT_BUILD_SDK_VERSION:-}" ]]; then
  python3 - "${template_consumer}/global.json" "${NETWASM_TUNIT_BUILD_SDK_VERSION}" <<'PY'
import json, sys
from pathlib import Path

path, version = Path(sys.argv[1]), sys.argv[2]
value = {'sdk': {'version': version, 'rollForward': 'disable', 'allowPrerelease': True}}
path.write_text(json.dumps(value, indent=2) + '\n')
PY
fi
(
  cd "${template_consumer}"
  dotnet restore Generated.Tests.csproj \
    --configfile "${nuget_config}" \
    --disable-build-servers \
    --nologo
  dotnet test Generated.Tests.csproj \
    -c Release \
    --no-restore \
    --disable-build-servers \
    --nologo | tee "${test_root}/template-run.log"
  assert_vstest_pass_summary "${test_root}/template-run.log" 1
  dotnet publish Generated.Tests.csproj \
    -c Release \
    --no-restore \
    --disable-build-servers \
    --nologo \
    -p:NetWasmPublishTarget=portable \
    -o "${test_root}/template-publish"
  [[ -f "${test_root}/template-publish/deployment.json" ]] || {
    echo "The TUnit candidate publish output is missing deployment.json." >&2
    exit 1
  }
)

# A test suite that also targets desktop .NET, as a portable library's does, with warnings as errors: on
# netwasm0.1 the packages must bring no .NET Standard asset (NU1701), so both frameworks restore, build and run.
dual_consumer="${test_root}/DualTarget.Tests"
mkdir -p "${dual_consumer}"
cp "${template_consumer}/Tests.cs" "${dual_consumer}/Tests.cs"
{
  head -n 1 "${template_consumer}/Generated.Tests.csproj"
  cat <<EOF
  <PropertyGroup>
    <TargetFrameworks>netwasm0.1;net10.0</TargetFrameworks>
    <IsTestProject>true</IsTestProject>
    <Nullable>enable</Nullable>
    <ImplicitUsings>disable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="NetWasm.TUnit" Version="[${TEST_VERSION}]" />
  </ItemGroup>
</Project>
EOF
} > "${dual_consumer}/DualTarget.Tests.csproj"
(
  cd "${dual_consumer}"
  dotnet restore DualTarget.Tests.csproj --configfile "${nuget_config}" --disable-build-servers --nologo \
    | tee "${test_root}/dual-restore.log"
  if grep -q "NU1701" "${test_root}/dual-restore.log"; then
    echo "A netwasm0.1 dependency of NetWasm.TUnit restored from a .NET Standard asset (NU1701)." >&2
    exit 1
  fi
  # Each framework runs as its suites do: netwasm0.1 through dotnet test's VSTest adapter, and desktop .NET as
  # the Microsoft.Testing.Platform application TUnit builds, since .NET 10's dotnet test has no VSTest mode for it.
  dotnet test DualTarget.Tests.csproj -c Release -f netwasm0.1 --no-restore --disable-build-servers --nologo \
    | tee "${test_root}/dual-netwasm.log"
  assert_vstest_pass_summary "${test_root}/dual-netwasm.log" 1
  dotnet run --project DualTarget.Tests.csproj -c Release -f net10.0 --no-restore --disable-build-servers \
    | tee "${test_root}/dual-desktop.log"
)

echo "NetWasm TUnit package, runner, VSTest, template and dual-target tests passed."
