from __future__ import annotations

import hashlib
import importlib.util
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
import zipfile


SCRIPT = Path(__file__).parents[1] / "release-train.py"
SPEC = importlib.util.spec_from_file_location("release_train", SCRIPT)
assert SPEC is not None and SPEC.loader is not None
MODULE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(MODULE)
ROOT = Path(__file__).resolve().parents[2]
WORKFLOW = Path(
    os.environ.get(
        "NETWASM_RELEASE_WORKFLOW_PATH",
        ROOT / ".github" / "workflows" / "netwasm-release.yml",
    )
)


def workflow_step(document: str, name: str) -> str:
    marker = f"      - name: {name}\n"
    start = document.index(marker)
    end = document.find("\n      - name: ", start + len(marker))
    return document[start:] if end < 0 else document[start:end]


class ReleaseTrainTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temporary = tempfile.TemporaryDirectory()
        self.root = Path(self.temporary.name)
        self.packages = self.root / "packages"
        self.packages.mkdir()
        self.commit = "a" * 40
        self.tag = "netwasm-v0.5.0-preview.1"
        self.package = self.packages / "NetWasm.TUnit.Example.0.5.0-preview.1.nupkg"
        self.package.write_bytes(b"package payload")
        self.manifest = self.root / "manifest.json"
        self.manifest.write_text(json.dumps({
            "schemaVersion": 1,
            "repository": "zion-sati/TUnit-NetWasm",
            "repositoryUrl": "https://github.com/zion-sati/TUnit-NetWasm",
            "releaseVersion": "0.5.0-preview.1",
            "releaseTag": self.tag,
            "sourceCommit": self.commit,
            "dependencyVersions": {"NetWasm.Sdk": "0.5.0"},
            "packages": ["NetWasm.TUnit.Example"],
        }))
        self.receipt = self.root / "receipt.json"
        self.receipt.write_text(json.dumps({
            "schemaVersion": 1,
            "status": "PASS",
            "repository": "zion-sati/TUnit-NetWasm",
            "releaseVersion": "0.5.0-preview.1",
            "releaseTag": self.tag,
            "sourceCommit": self.commit,
            "packageCount": 1,
            "packages": [{
                "id": "NetWasm.TUnit.Example",
                "version": "0.5.0-preview.1",
                "fileName": self.package.name,
                "size": self.package.stat().st_size,
                "sha256": hashlib.sha256(self.package.read_bytes()).hexdigest(),
            }],
        }))

    def tearDown(self) -> None:
        self.temporary.cleanup()

    def create_bundle(self, name: str = "preview.zip") -> Path:
        output = self.root / name
        MODULE.create_bundle(self.manifest, self.receipt, self.packages, output)
        return output

    def test_bundle_round_trip_is_deterministic_and_receipt_bound(self) -> None:
        first = self.create_bundle("first.zip")
        second = self.create_bundle("second.zip")

        first_summary = MODULE.inspect_bundle(first)
        second_summary = MODULE.inspect_bundle(second)
        self.assertEqual(first.read_bytes(), second.read_bytes())
        self.assertEqual("0.5.0-preview.1", first_summary["version"])
        self.assertEqual(first_summary["packages"], second_summary["packages"])
        extracted = self.root / "extracted"
        MODULE.extract_bundle(first, extracted)
        self.assertEqual(
            b"package payload",
            (extracted / "packages" / self.package.name).read_bytes(),
        )

        self.package.write_bytes(b"changed")
        with self.assertRaisesRegex(ValueError, "does not match"):
            MODULE.create_bundle(
                self.manifest, self.receipt, self.packages, self.root / "changed.zip"
            )

    def test_train_binds_both_channels_to_source_run_and_target_tags(self) -> None:
        preview = self.create_bundle("preview.zip")
        stable_package = self.packages / "NetWasm.TUnit.Example.0.5.0.nupkg"
        self.package.rename(stable_package)
        stable_manifest = json.loads(self.manifest.read_text())
        stable_manifest["releaseVersion"] = "0.5.0"
        stable_manifest_path = self.root / "stable-manifest.json"
        stable_manifest_path.write_text(json.dumps(stable_manifest))
        stable_receipt = json.loads(self.receipt.read_text())
        stable_receipt["releaseVersion"] = "0.5.0"
        stable_receipt["packages"][0].update({
            "version": "0.5.0",
            "fileName": stable_package.name,
        })
        stable_receipt_path = self.root / "stable-receipt.json"
        stable_receipt_path.write_text(json.dumps(stable_receipt))
        stable = self.root / "stable.zip"
        MODULE.create_bundle(stable_manifest_path, stable_receipt_path, self.packages, stable)
        train = MODULE.create_train_manifest(
            repository="zion-sati/TUnit-NetWasm",
            source_commit=self.commit,
            producing_tag=self.tag,
            preview_tag=self.tag,
            stable_tag="netwasm-v0.5.0",
            run_id="42",
            artifact_name="netwasm-tunit-release-train-0.5.0",
            preview_bundle=preview,
            stable_bundle=stable,
        )

        self.assertEqual("42", train["workflowRunId"])
        self.assertEqual("netwasm-v0.5.0", train["stable"]["releaseTag"])
        self.assertEqual(
            ("42", "netwasm-tunit-release-train-0.5.0"),
            MODULE.verify_train_identity(
                train, "zion-sati/TUnit-NetWasm", self.commit, self.tag,
                self.tag, "netwasm-v0.5.0", "netwasm-tunit-release-train-0.5.0",
            ),
        )
        MODULE.verify_train(
            train, stable, "stable", "zion-sati/TUnit-NetWasm", self.commit,
            "netwasm-v0.5.0"
        )
        with self.assertRaisesRegex(ValueError, "does not target"):
            MODULE.verify_train(
                train, stable, "stable", "zion-sati/TUnit-NetWasm", self.commit,
                "netwasm-v0.5.1",
            )
        with self.assertRaisesRegex(ValueError, "sourceCommit"):
            MODULE.verify_train_identity(
                train, "zion-sati/TUnit-NetWasm", "b" * 40, self.tag,
                self.tag, "netwasm-v0.5.0", "netwasm-tunit-release-train-0.5.0",
            )
        with self.assertRaisesRegex(ValueError, "coordinates"):
            MODULE.verify_train_identity(
                train, "zion-sati/TUnit-NetWasm", self.commit, self.tag,
                self.tag, "netwasm-v0.5.0", "unexpected-artifact",
            )

    def test_rejects_unsafe_and_unexpected_bundle_entries(self) -> None:
        path = self.root / "unsafe.zip"
        with zipfile.ZipFile(path, "w") as archive:
            archive.writestr("../escape", b"bad")
        with self.assertRaisesRegex(ValueError, "unsafe"):
            MODULE.inspect_bundle(path)

    def test_identity_command_writes_exact_producer_coordinates(self) -> None:
        preview = self.create_bundle("preview.zip")
        stable_package = self.packages / "NetWasm.TUnit.Example.0.5.0.nupkg"
        self.package.rename(stable_package)
        stable_manifest = json.loads(self.manifest.read_text())
        stable_manifest["releaseVersion"] = "0.5.0"
        stable_manifest_path = self.root / "stable-manifest.json"
        stable_manifest_path.write_text(json.dumps(stable_manifest))
        stable_receipt = json.loads(self.receipt.read_text())
        stable_receipt["releaseVersion"] = "0.5.0"
        stable_receipt["packages"][0].update({
            "version": "0.5.0",
            "fileName": stable_package.name,
        })
        stable_receipt_path = self.root / "stable-receipt.json"
        stable_receipt_path.write_text(json.dumps(stable_receipt))
        stable = self.root / "stable.zip"
        MODULE.create_bundle(
            stable_manifest_path, stable_receipt_path, self.packages, stable
        )
        train = MODULE.create_train_manifest(
            repository="zion-sati/TUnit-NetWasm",
            source_commit=self.commit,
            producing_tag=self.tag,
            preview_tag=self.tag,
            stable_tag="netwasm-v0.5.0",
            run_id="42",
            artifact_name="netwasm-tunit-release-train-0.5.0",
            preview_bundle=preview,
            stable_bundle=stable,
        )
        train_path = self.root / "train.json"
        train_path.write_text(json.dumps(train))
        output = self.root / "github-output"

        completed = subprocess.run(
            [
                sys.executable,
                str(SCRIPT),
                "identity",
                "--train", str(train_path),
                "--repository", "zion-sati/TUnit-NetWasm",
                "--source-commit", self.commit,
                "--producing-tag", self.tag,
                "--preview-tag", self.tag,
                "--stable-tag", "netwasm-v0.5.0",
                "--artifact-name", "netwasm-tunit-release-train-0.5.0",
                "--github-output", str(output),
            ],
            capture_output=True,
            text=True,
            check=False,
        )

        self.assertEqual(0, completed.returncode, completed.stderr)
        self.assertEqual(
            "producing_run_id=42\nartifact_name=netwasm-tunit-release-train-0.5.0\n",
            output.read_text(),
        )



class ReleaseTrainStateTests(unittest.TestCase):
    def test_first_preview_builds_when_no_train_is_bound(self) -> None:
        self.assertEqual("build", MODULE.effective_mode("build", 0))

    def test_preview_retry_resumes_when_train_is_bound(self) -> None:
        self.assertEqual("resume", MODULE.effective_mode("build", 1))

    def test_stable_promotes_when_preview_train_is_bound(self) -> None:
        self.assertEqual("promote", MODULE.effective_mode("promote", 1))

    def test_stable_fails_closed_without_preview_train(self) -> None:
        with self.assertRaisesRegex(ValueError, "requires the retained preview"):
            MODULE.effective_mode("promote", 0)

    def test_duplicate_manifest_identity_is_rejected(self) -> None:
        with self.assertRaisesRegex(ValueError, "ambiguous"):
            MODULE.effective_mode("build", 2)

    def test_state_command_writes_effective_mode(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            output = Path(temporary) / "github-output"
            completed = subprocess.run(
                [
                    sys.executable,
                    str(SCRIPT),
                    "state",
                    "--resolved-mode", "build",
                    "--retained-manifest-count", "1",
                    "--github-output", str(output),
                ],
                capture_output=True,
                text=True,
                check=False,
            )

            self.assertEqual(0, completed.returncode, completed.stderr)
            self.assertEqual("mode=resume\n", output.read_text(encoding="utf-8"))


class ReleaseWorkflowTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        if not WORKFLOW.is_file():
            raise unittest.SkipTest("Release workflow is unavailable in this checkout.")
        cls.document = WORKFLOW.read_text(encoding="utf-8")

    def test_stable_promotion_cannot_start_build_steps(self) -> None:
        for name in (
            "Install .NET SDK",
            "Build and qualify preview and stable packages",
        ):
            self.assertIn(
                "if: steps.train-state.outputs.mode == 'build'",
                workflow_step(self.document, name),
            )

    def test_stable_candidate_is_bound_to_preview_release_and_producing_run(self) -> None:
        coordinates = workflow_step(
            self.document, "Resolve retained release train coordinates"
        )
        retained = workflow_step(
            self.document, "Resolve and verify retained candidate"
        )
        for contract in (
            'gh release download "$PREVIEW_TAG"',
            "release-train.py identity",
            '--artifact-name "$ARTIFACT_NAME"',
        ):
            self.assertIn(contract, coordinates)
        for contract in (
            'gh run download "${{ steps.retained-train.outputs.producing_run_id }}"',
            'cmp --silent "$train" "$downloaded/$ARTIFACT_NAME.json"',
            "release-train.py verify",
            '--channel "$channel"',
            "release-train.py extract",
            "verify-release-packages.py",
        ):
            self.assertIn(contract, retained)
        self.assertNotIn("netwasm-test.sh", retained)

    def test_preview_retry_selects_retained_preview_without_building(self) -> None:
        state = workflow_step(self.document, "Resolve release train state")
        retained = workflow_step(
            self.document, "Resolve and verify retained candidate"
        )
        self.assertIn("release-train.py state", state)
        self.assertIn("steps.train-state.outputs.mode", self.document)
        self.assertIn('if [[ "$RELEASE_MODE" == "resume" ]]; then', retained)
        self.assertIn("channel=preview", retained)
        self.assertIn('version="$PREVIEW_VERSION"', retained)
        self.assertIn('release_tag="$PREVIEW_TAG"', retained)
        self.assertIn("if: needs.build.outputs.mode != 'build'", self.document)

    def test_publication_has_preflight_and_topological_parallel_waves(self) -> None:
        self.assertEqual(1, self.document.count("uses: NuGet/login@"))
        self.assertIn("--preflight", self.document)
        self.assertIn("--publish", self.document)
        self.assertIn("Retain release timing evidence", self.document)

    def test_coordinated_approval_precedes_nuget_credentials(self) -> None:
        approval = self.document.index("Verify coordinated publication approval")
        credential = self.document.index("Request temporary NuGet.org credential")
        self.assertLess(approval, credential)
        self.assertIn(
            "release-train.py approval",
            workflow_step(self.document, "Verify coordinated publication approval"),
        )


if __name__ == "__main__":
    unittest.main()
