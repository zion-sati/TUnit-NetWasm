#!/usr/bin/env bash

set -euo pipefail

REPOSITORY_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd -P)"
PACKAGE_DIR="${1:-${REPOSITORY_ROOT}/artifacts/netwasm-packages}"
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

"${REPOSITORY_ROOT}/eng/netwasm-build-packages.sh" "${PACKAGE_DIR}"

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

runner_tests="${REPOSITORY_ROOT}/packaging/NetWasm.TUnit.Runner.Tests/NetWasm.TUnit.Runner.Tests.csproj"
dotnet restore "${runner_tests}" --configfile "${nuget_config}" --disable-build-servers --nologo
dotnet run --project "${runner_tests}" \
  -c Release \
  --no-restore \
  --no-launch-profile \
  --disable-build-servers \
  -- --minimum-expected-tests 52

package_tests="NetWasm.TUnit.Package.Tests/NetWasm.TUnit.Package.Tests.csproj"
(
  cd "${REPOSITORY_ROOT}/packaging"
  dotnet restore "${package_tests}" --configfile "${nuget_config}" --disable-build-servers --nologo
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
)

echo "NetWasm TUnit package, runner, VSTest and template tests passed."
