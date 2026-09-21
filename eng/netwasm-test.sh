#!/usr/bin/env bash

set -euo pipefail

REPOSITORY_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd -P)"
PACKAGE_DIR="${1:-${REPOSITORY_ROOT}/artifacts/netwasm-packages}"
TEST_VERSION="${NETWASM_TUNIT_TEST_VERSION:-$(tr -d '[:space:]' < "${REPOSITORY_ROOT}/eng/NetWasm.ReleaseVersion.txt")}"
mkdir -p "${PACKAGE_DIR}"
PACKAGE_DIR="$(cd "${PACKAGE_DIR}" && pwd -P)"

test_root="$(mktemp -d "${TMPDIR:-/tmp}/netwasm-tunit-test.XXXXXX")"
test_root="$(cd "${test_root}" && pwd -P)"
cleanup() {
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
if [[ -n "${NETWASM_CORE_CANDIDATE_VERSION:-}" ]]; then
  build_arguments+=(--netwasm-candidate-version "${NETWASM_CORE_CANDIDATE_VERSION}")
fi
if [[ -n "${NETWASM_TUNIT_BUILD_SDK_VERSION:-}" ]]; then
  build_arguments+=(--sdk-version "${NETWASM_TUNIT_BUILD_SDK_VERSION}")
fi
"${REPOSITORY_ROOT}/eng/netwasm-build-packages.sh" "${build_arguments[@]}"

qualification_root="${REPOSITORY_ROOT}"
if [[ -n "${NETWASM_CORE_CANDIDATE_VERSION:-}" ]]; then
  qualification_root="${test_root}/source"
  mkdir -p "${qualification_root}"
  git -C "${REPOSITORY_ROOT}" archive HEAD | tar -x -C "${qualification_root}"
  python3 "${qualification_root}/eng/project-netwasm-candidate-version.py" \
    --source-root "${qualification_root}" \
    --version "${NETWASM_CORE_CANDIDATE_VERSION}" \
    --receipt "${test_root}/NetWasm.TUnit.test-source-projection.json"
fi
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

nuget_config="${test_root}/NuGet.Config"
xml_escape() {
  printf '%s' "$1" | sed -e 's/&/\&amp;/g' -e 's/"/\&quot;/g' -e 's/</\&lt;/g' -e 's/>/\&gt;/g'
}
package_dir_xml="$(xml_escape "${PACKAGE_DIR}")"
ci_package_source_xml="$(xml_escape "${NETWASM_CI_PACKAGE_SOURCE:-}")"
{
  printf '%s\n' \
    '<?xml version="1.0" encoding="utf-8"?>' \
    '<configuration>' \
    '  <packageSources>' \
    '    <clear />' \
    "    <add key=\"current-build\" value=\"${package_dir_xml}\" />"
  if [[ -n "${NETWASM_CI_PACKAGE_SOURCE:-}" ]]; then
    printf '    <add key="ci-artifacts" value="%s" />\n' "${ci_package_source_xml}"
  fi
  printf '%s\n' \
    '    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />' \
    '  </packageSources>' \
    '</configuration>'
} > "${nuget_config}"

runner_tests="${qualification_root}/packaging/NetWasm.TUnit.Runner.Tests/NetWasm.TUnit.Runner.Tests.csproj"
dotnet restore "${runner_tests}" --configfile "${nuget_config}" --disable-build-servers --nologo
dotnet run --project "${runner_tests}" \
  -c Release \
  --no-restore \
  --no-launch-profile \
  --disable-build-servers \
  -- --minimum-expected-tests 52

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
  assert_vstest_pass_summary "${test_root}/package-run.log" 2
  dotnet test "${package_tests}" -c Release --no-build --no-restore --list-tests --nologo |
    tee "${test_root}/package-list.log"
  grep -F -q 'PackageOnlyConsumerRunsThroughStandardDotNetTest()' \
    "${test_root}/package-list.log"
  grep -F -q 'FilterCanaryPreservesTraits()' \
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

echo "NetWasm TUnit package, runner, VSTest and template tests passed."
