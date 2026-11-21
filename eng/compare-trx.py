#!/usr/bin/env python3
"""Compares two TRX reports: the same tests, by name, with the same outcomes, and an error message and a stack trace
for every failure in both. eng/netwasm-test.sh compares a net10.0-netwasm run's report with desktop .NET's.

usage: compare-trx.py <netwasm.trx> <desktop.trx>
"""
import sys
import xml.etree.ElementTree as ET

NS = {"t": "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"}


def results(path):
    root = ET.parse(path).getroot()
    found = {}
    for result in root.iterfind(".//t:Results/t:UnitTestResult", NS):
        name = result.get("testName")
        if name in found:
            raise SystemExit(f"{path}: '{name}' is reported twice")
        error = result.find("t:Output/t:ErrorInfo", NS)
        message = error.findtext("t:Message", default="", namespaces=NS) if error is not None else ""
        stack = error.findtext("t:StackTrace", default="", namespaces=NS) if error is not None else ""
        found[name] = (result.get("outcome"), message.strip(), stack.strip())
    if not found:
        raise SystemExit(f"{path}: no test results")
    return found


def main():
    if len(sys.argv) != 3:
        raise SystemExit(__doc__)
    netwasm, desktop = results(sys.argv[1]), results(sys.argv[2])
    problems = []
    for name in sorted(set(netwasm) | set(desktop)):
        if name not in netwasm or name not in desktop:
            problems.append(f"'{name}' is only in the {'desktop' if name in desktop else 'net10.0-netwasm'} report")
            continue
        if netwasm[name][0] != desktop[name][0]:
            problems.append(f"'{name}': {netwasm[name][0]} on net10.0-netwasm, {desktop[name][0]} on desktop")
        for platform, (outcome, message, stack) in (("net10.0-netwasm", netwasm[name]), ("desktop", desktop[name])):
            if outcome == "Failed" and (not message or not stack):
                problems.append(f"'{name}' failed on {platform} without an error message and stack trace")
    if problems:
        raise SystemExit("TRX reports differ:\n  " + "\n  ".join(problems))
    print(f"TRX reports match: {len(netwasm)} tests, {sorted({o for o, _, _ in netwasm.values()})}")


if __name__ == "__main__":
    main()
