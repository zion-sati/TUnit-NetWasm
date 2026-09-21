#!/usr/bin/env bash

set -euo pipefail

REPOSITORY_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd -P)"
OUTPUT_DIR="${REPOSITORY_ROOT}/artifacts/netwasm-packages"
RELEASE_VERSION="$(tr -d '[:space:]' < "${REPOSITORY_ROOT}/eng/NetWasm.ReleaseVersion.txt")"
NETWASM_CANDIDATE_VERSION=""
output_was_set=false
while [[ $# -gt 0 ]]; do
  case "$1" in
    --output)
      [[ $# -ge 2 ]] || { echo "--output requires a directory." >&2; exit 2; }
      OUTPUT_DIR="$2"
      output_was_set=true
      shift 2
      ;;
    --version)
      [[ $# -ge 2 ]] || { echo "--version requires a value." >&2; exit 2; }
      RELEASE_VERSION="$2"
      shift 2
      ;;
    --netwasm-candidate-version)
      [[ $# -ge 2 ]] || { echo "--netwasm-candidate-version requires a value." >&2; exit 2; }
      NETWASM_CANDIDATE_VERSION="$2"
      shift 2
      ;;
    --help|-h)
      echo "Usage: $0 [--version VERSION] [--netwasm-candidate-version VERSION] [--output DIRECTORY] [DIRECTORY]"
      exit 0
      ;;
    --*)
      echo "Unknown option: $1" >&2
      exit 2
      ;;
    *)
      if [[ "${output_was_set}" == true ]]; then
        echo "Package output was specified more than once." >&2
        exit 2
      fi
      OUTPUT_DIR="$1"
      output_was_set=true
      shift
      ;;
  esac
done
mkdir -p "${OUTPUT_DIR}"
OUTPUT_DIR="$(cd "${OUTPUT_DIR}" && pwd -P)"

case "${OUTPUT_DIR}" in
  "${REPOSITORY_ROOT}/artifacts"|"${REPOSITORY_ROOT}/artifacts"/*)
    ;;
  "${REPOSITORY_ROOT}"|"${REPOSITORY_ROOT}"/*)
    echo "Package output inside the repository must stay under artifacts/: ${OUTPUT_DIR}" >&2
    exit 2
    ;;
esac

find "${OUTPUT_DIR}" -maxdepth 1 -type f \
  \( -name 'NetWasm.TUnit*.nupkg' -o -name 'NetWasm.TUnit*.snupkg' \) -delete

build_root="$(mktemp -d "${TMPDIR:-/tmp}/netwasm-tunit-build.XXXXXX")"
build_root="$(cd "${build_root}" && pwd -P)"
source_root="${build_root}/source"
cleanup() {
  if [[ -d "${source_root}" ]]; then
    git -C "${REPOSITORY_ROOT}" worktree remove --force "${source_root}" >/dev/null 2>&1 || true
  fi
  rm -rf "${build_root}"
}
trap cleanup EXIT

for command in dotnet git python3; do
  command -v "${command}" >/dev/null 2>&1 || {
    echo "Missing required command: ${command}" >&2
    exit 2
  }
done

[[ "$(git -C "${REPOSITORY_ROOT}" rev-parse --is-inside-work-tree 2>/dev/null || true)" == "true" ]] || {
  echo "Package construction requires a Git checkout." >&2
  exit 2
}
[[ -z "$(git -C "${REPOSITORY_ROOT}" status --porcelain=v1)" ]] || {
  echo "Package construction requires a clean checkout." >&2
  exit 2
}

repository_commit="$(git -C "${REPOSITORY_ROOT}" rev-parse HEAD)"
git -C "${REPOSITORY_ROOT}" worktree add --quiet --detach "${source_root}" "${repository_commit}"
python3 "${source_root}/eng/project-release-version.py" \
  --source-root "${source_root}" \
  --version "${RELEASE_VERSION}" \
  --receipt "${OUTPUT_DIR}/NetWasm.TUnit.release-version-projection.json"
if [[ -n "${NETWASM_CANDIDATE_VERSION}" ]]; then
  python3 "${source_root}/eng/project-netwasm-candidate-version.py" \
    --source-root "${source_root}" \
    --version "${NETWASM_CANDIDATE_VERSION}" \
    --receipt "${OUTPUT_DIR}/NetWasm.TUnit.netwasm-candidate-projection.json"
fi
# global.json discovery follows the process working directory, not an absolute
# project argument. Anchor every dotnet invocation to the detached source.
cd "${source_root}"

nuget_config="${build_root}/NuGet.Config"
xml_escape() {
  printf '%s' "$1" | sed -e 's/&/\&amp;/g' -e 's/"/\&quot;/g' -e 's/</\&lt;/g' -e 's/>/\&gt;/g'
}
output_dir_xml="$(xml_escape "${OUTPUT_DIR}")"
ci_package_source_xml="$(xml_escape "${NETWASM_CI_PACKAGE_SOURCE:-}")"
{
  printf '%s\n' \
    '<?xml version="1.0" encoding="utf-8"?>' \
    '<configuration>' \
    '  <packageSources>' \
    '    <clear />' \
    "    <add key=\"current-build\" value=\"${output_dir_xml}\" />"
  if [[ -n "${NETWASM_CI_PACKAGE_SOURCE:-}" ]]; then
    printf '    <add key="ci-artifacts" value="%s" />\n' "${ci_package_source_xml}"
  fi
  printf '%s\n' \
    '    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />' \
    '  </packageSources>' \
    '</configuration>'
} > "${nuget_config}"

package_cache="${NUGET_PACKAGES:-${build_root}/packages}"
sdk_version="$(sed -n 's/.*"NetWasm.Sdk": "\([^"]*\)".*/\1/p' "${source_root}/packaging/global.json")"
if [[ -z "${sdk_version}" ]]; then
  echo "Unable to read the NetWasm.Sdk version from packaging/global.json." >&2
  exit 1
fi

seed_project="${build_root}/SeedSdk.csproj"
printf '%s\n' \
  '<Project Sdk="Microsoft.NET.Sdk">' \
  '  <PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>' \
  "  <ItemGroup><PackageReference Include=\"NetWasm.Sdk\" Version=\"${sdk_version}\" PrivateAssets=\"all\" /></ItemGroup>" \
  '</Project>' > "${seed_project}"
NUGET_PACKAGES="${package_cache}" dotnet restore "${seed_project}" \
  --configfile "${nuget_config}" \
  --disable-build-servers \
  --nologo

build_projects=(
  src/TUnit.Analyzers.Roslyn44/TUnit.Analyzers.Roslyn44.csproj
  src/TUnit.Analyzers.Roslyn47/TUnit.Analyzers.Roslyn47.csproj
  src/TUnit.Analyzers.Roslyn414/TUnit.Analyzers.Roslyn414.csproj
  src/TUnit.Analyzers.CodeFixers/TUnit.Analyzers.CodeFixers.csproj
  src/TUnit.Core.SourceGenerator.Roslyn44/TUnit.Core.SourceGenerator.Roslyn44.csproj
  src/TUnit.Core.SourceGenerator.Roslyn47/TUnit.Core.SourceGenerator.Roslyn47.csproj
  src/TUnit.Core.SourceGenerator.Roslyn414/TUnit.Core.SourceGenerator.Roslyn414.csproj
  src/TUnit.Assertions.Analyzers/TUnit.Assertions.Analyzers.csproj
  src/TUnit.Assertions.Analyzers.CodeFixers/TUnit.Assertions.Analyzers.CodeFixers.csproj
  src/TUnit.Assertions.SourceGenerator/TUnit.Assertions.SourceGenerator.csproj
  src/TUnit.Engine/TUnit.Engine.csproj
)

for project in "${build_projects[@]}"; do
  NUGET_PACKAGES="${package_cache}" dotnet restore "${source_root}/${project}" \
    --configfile "${nuget_config}" \
    --disable-build-servers \
    --nologo
  NUGET_PACKAGES="${package_cache}" dotnet build "${source_root}/${project}" \
    -c Release \
    --no-restore \
    --disable-build-servers \
    --nologo
done

package_projects=(
  packaging/NetWasm.TUnit.Core.csproj
  packaging/NetWasm.TUnit.Assertions.csproj
  packaging/NetWasm.TUnit.Engine.csproj
  packaging/NetWasm.TUnit.Package/NetWasm.TUnit.Package.csproj
  packaging/NetWasm.TUnit.Templates/NetWasm.TUnit.Templates.csproj
)

for project in "${package_projects[@]}"; do
  NUGET_PACKAGES="${package_cache}" dotnet restore "${source_root}/${project}" \
    --configfile "${nuget_config}" \
    --disable-build-servers \
    --nologo
  NUGET_PACKAGES="${package_cache}" dotnet pack "${source_root}/${project}" \
    -c Release \
    --no-restore \
    --disable-build-servers \
    --nologo \
    -o "${OUTPUT_DIR}"
done

package_count="$(find "${OUTPUT_DIR}" -maxdepth 1 -type f -name 'NetWasm.TUnit*.nupkg' | wc -l | tr -d ' ')"
if [[ "${package_count}" -ne 5 ]]; then
  echo "Expected exactly five NetWasm TUnit packages, found ${package_count}." >&2
  exit 1
fi

echo "Built five NetWasm TUnit packages at ${RELEASE_VERSION} in ${OUTPUT_DIR}"
