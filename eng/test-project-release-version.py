#!/usr/bin/env python3

from __future__ import annotations

import importlib.util
import subprocess
import tempfile
import unittest
from pathlib import Path


SCRIPT_PATH = Path(__file__).parent / "project-release-version.py"
SPEC = importlib.util.spec_from_file_location("project_release_version", SCRIPT_PATH)
assert SPEC is not None and SPEC.loader is not None
MODULE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(MODULE)


class ProjectReleaseVersionTests(unittest.TestCase):
    def test_projects_only_tracked_text(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            root = Path(temporary_directory)
            subprocess.run(["git", "init", "--quiet", str(root)], check=True)
            subprocess.run(["git", "-C", str(root), "config", "user.name", "Test"], check=True)
            subprocess.run(
                ["git", "-C", str(root), "config", "user.email", "test@example.invalid"],
                check=True,
            )
            (root / "eng").mkdir()
            (root / "eng/NetWasm.ReleaseVersion.txt").write_text("0.1.0-rc.1\n")
            (root / "project.txt").write_text("Package=0.1.0-rc.1\nDependency=0.1.0-rc.1\n")
            (root / "binary.dat").write_bytes(b"\0" + b"0.1.0-rc.1")
            subprocess.run(["git", "-C", str(root), "add", "."], check=True)
            subprocess.run(["git", "-C", str(root), "commit", "--quiet", "-m", "fixture"], check=True)

            receipt = MODULE.project_version(root, "0.1.0-alpha.1", root.parent / "receipt.json")

            self.assertEqual(3, receipt["replacementCount"])
            self.assertNotIn("0.1.0-rc.1", (root / "project.txt").read_text())
            self.assertEqual(b"\0" + b"0.1.0-rc.1", (root / "binary.dat").read_bytes())


if __name__ == "__main__":
    unittest.main()
