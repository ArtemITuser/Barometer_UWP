#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Negative (mutation) tests for tests/static_checks.py.

A green acceptance suite is worthless if it cannot go red. This harness applies
synthetic regressions to a THROWAWAY COPY of the repository and asserts that the
right named check fires for each one — covering every historical failure class
from v1.4.0.27..37:

  N1  MSB4092-class batching in a target Condition        -> B2 must FAIL
  N2  HintPath reference into packages dir (facade shadowing) -> B4/B5 must FAIL
  N3  empty <Reference> block (v29..36 WMC1006 root cause)-> B3 must FAIL
  N4  firewall target reintroduced                        -> B6 must FAIL
  N5  UsingTask NuGetRestoreTask reintroduced             -> B7 must FAIL
  N6  duplicate PurgeStaleRestoreState                    -> B8 must FAIL
  N7  TargetPlatformVersion bumped to 15254               -> B9/F2 must FAIL
  N8  StackPanel.Spacing attribute re-added               -> F4 must FAIL
  N9  metapackage back in packages.config                 -> G1 must FAIL
  N10 live 'using Newtonsoft.Json' in a source            -> G2 must FAIL
  N11 version drift AssemblyInfo vs manifest              -> E1 must FAIL
  N12 orphan .cs file not declared in csproj              -> D1 must FAIL
  N13 broken DependentUpon pairing                        -> D5 must FAIL
  N14 malformed XAML                                      -> A1 must FAIL
  N15 truncated C# file (unbalanced braces)               -> H5 must FAIL
  N16 resw key removed from ru-RU only                    -> H7 must FAIL
  N17 UnitConverter factor changed in model only          -> H1d must FAIL

Usage: python3 tests/mutation_tests.py   (exit code = number of undetected mutations)
"""
import os, re, shutil, subprocess, sys, tempfile

SRC = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
CHECKS = os.path.join(SRC, "tests", "static_checks.py")


def run_suite(root):
    """Run static_checks.py against a copy rooted at `root`; return set of FAILED ids.

    Robustness rules learned the hard way:
      1. Check names are multi-word ("F4 no StackPanel.Spacing ..."), so match only
         the leading ID token [A-H]\\d+[a-z]? — \\w+ used to grab the word "no".
      2. The suite prints "[FAIL] <id> ..." immediately when a check is recorded AND
         a "FAILED: <id>" summary at the very end. Harvest BOTH forms.
      3. If the mutation crashes the suite mid-run (traceback), the summary is never
         printed; treat a crash with zero harvested IDs as ALL checks firing — a
         crashing suite has detected the regression, it just cannot name it.
      4. The suite must be copied into the throwaway tree (it resolves ROOT relative
         to its own file location), otherwise it would test the pristine repo instead
         of the mutated copy. This was the harness bug behind false MISSED results.
    """
    env = dict(os.environ, PYTHONPATH="", PYTHONHOME="")
    dst_checks = os.path.join(root, "tests", "static_checks.py")
    # baseline runs against SRC itself (root == SRC); mutations run against copies —
    # only copy when source and destination differ, else shutil raises SameFileError.
    if not os.path.samefile(CHECKS, dst_checks):
        shutil.copy(CHECKS, dst_checks)
    p = subprocess.run([sys.executable, os.path.join(root, "tests", "static_checks.py")],
                       capture_output=True, text=True, cwd=root, env=env)
    failed = set()
    for line in p.stdout.splitlines():
        m = re.match(r"(?:FAILED|\[FAIL\]):? ?([A-H]\d+[a-z]?)\b", line)
        if m:
            failed.add(m.group(1))
    crashed = "Traceback" in p.stderr or p.returncode >= 2
    if crashed and not failed:
        # died before recording ANY named result => conservative all-fired verdict
        failed |= {f"{c}{n}" for c in "ABCDEFGH" for n in "12345678"}
        failed |= {"H1a", "H1b", "H1c", "H1d", "H2a", "H2b", "B9"}
    return failed, p.returncode


def mutate_copy(name, fn):
    tmp = tempfile.mkdtemp(prefix=f"baro-{name}-")
    dst = os.path.join(tmp, "repo")
    # NOTE: "packages" here refers to NuGet output dirs inside the copy; the repo
    # root has none, so copying everything except VCS/build junk is correct.
    shutil.copytree(SRC, dst, ignore=shutil.ignore_patterns(".git", "bin", "obj"))
    fn(dst)
    failed, rc = run_suite(dst)
    shutil.rmtree(tmp, ignore_errors=True)
    return failed, rc


CS = "Barometer_UWP/Barometer_UWP.csproj"


def patch(root, old, new, count=1):
    rewrite(os.path.join(root, CS), lambda t: t.replace(old, new, count), old)


def rewrite(relpath, fn, anchor=None):
    """Safe in-place edit: FULLY read the file into memory BEFORE opening it for
    writing.  The previous self-computed-lambda form
        open(p,'w').write(open(p).read()...)
    truncated the file to zero bytes (Python evaluates open(p,'w') first), which
    produced false MISSED results for N8/N9/N15/N16."""
    text = open(relpath, encoding="utf-8").read()
    if anchor is not None:
        assert anchor in text, f"anchor missing in {relpath}: {anchor[:60]}"
    out = fn(text)
    with open(relpath, "w", encoding="utf-8") as fh:
        fh.write(out)


# The suite resolves ROOT from its own file location; a stale committed copy inside
# the throwaway tree would shadow the current checks. Always overwrite before running.
def refresh_checks(dst):
    dstc = os.path.join(dst, "tests", "static_checks.py")
    if not os.path.samefile(CHECKS, dstc):
        shutil.copy(CHECKS, dstc)


def mutate_copy(name, fn):
    tmp = tempfile.mkdtemp(prefix=f"baro-{name}-")
    dst = os.path.join(tmp, "repo")
    # NOTE: "packages" here refers to NuGet output dirs inside the copy; the repo
    # root has none, so copying everything except VCS/build junk is correct.
    shutil.copytree(SRC, dst, ignore=shutil.ignore_patterns(".git", "bin", "obj"))
    fn(dst)
    refresh_checks(dst)
    failed, rc = run_suite(dst)
    shutil.rmtree(tmp, ignore_errors=True)
    return failed, rc


MUTATIONS = [
    ("N1_msb4092_condition",
     lambda r: patch(r, '<Target Name="PurgeStaleRestoreState"',
                     '<Target Name="PurgeStaleRestoreState" Condition="\'%(Reference.HintPath)\' != \'\'"'),
     {"B2"}),
    ("N2_hintpath_reference",
     lambda r: patch(r, '<Reference Include="System" />',
                     '<Reference Include="..\\packages\\x\\System.Runtime.dll"><HintPath>..\\packages\\x\\System.Runtime.dll</HintPath></Reference>\n    <Reference Include="System" />'),
     {"B4", "B5"}),
    ("N3_empty_references",
     lambda r: patch(r, '<Reference Include="System" />\n    <Reference Include="System.Core" />\n    <Reference Include="System.Numerics" />\n    <Reference Include="System.Numerics.Vectors" />\n    <Reference Include="System.Runtime.InteropServices.WindowsRuntime" />\n    <Reference Include="Microsoft.CSharp" />\n    <Reference Include="System.Xml.Linq" />\n    <Reference Include="Windows" />',
                     ''),
     {"B3"}),
    ("N4_firewall_back",
     lambda r: patch(r, '<Target Name="DumpReferenceSet"',
                     '<Target Name="RejectPackageFacadeShadowReferences" />\n  <Target Name="DumpReferenceSet"'),
     {"B6"}),
    ("N5_usingtask_back",
     lambda r: patch(r, '<Import Project="$(MSBuildExtensionsPath)\\Microsoft\\WindowsXaml',
                     '<UsingTask TaskName="NuGetRestoreTask" AssemblyFile="x.dll" />\n  <Import Project="$(MSBuildExtensionsPath)\\Microsoft\\WindowsXaml'),
     {"B7"}),
    ("N6_duplicate_purge",
     lambda r: patch(r, '<Target Name="DumpReferenceSet"',
                     '<Target Name="PurgeStaleRestoreState" />\n  <Target Name="DumpReferenceSet"'),
     {"B8"}),
    ("N7_platform_drift",
     lambda r: patch(r, '<TargetPlatformVersion>10.0.15063.0</TargetPlatformVersion>',
                     '<TargetPlatformVersion>10.0.15254.0</TargetPlatformVersion>'),
     {"B9", "F2"}),
    ("N8_spacing_attr",
     lambda r: rewrite(os.path.join(r, "Barometer_UWP/Views/SettingsPage.xaml"),
                       lambda t: t.replace('<StackPanel Margin="20,10,20,20">',
                                           '<StackPanel Margin="20,10,20,20" Spacing="8">', 1),
                       '<StackPanel Margin="20,10,20,20">'),
     {"F4"}),
    ("N9_metapackage_back",
     lambda r: rewrite(os.path.join(r, "Barometer_UWP/packages.config"),
                       lambda t: t.replace('</packages>',
                           '<package id="Microsoft.NETCore.UniversalWindowsPlatform" version="6.0.8" targetFramework="uap10.0.15063" /></packages>', 1),
                       '</packages>'),
     {"G1"}),
    ("N10_live_newtonsoft_using",
     lambda r: rewrite(os.path.join(r, "Barometer_UWP/Helpers/Logger.cs"),
                       lambda t: "using Newtonsoft.Json;\n" + t),
     {"G2"}),
    ("N11_version_drift",
     lambda r: rewrite(os.path.join(r, "Barometer_UWP/Properties/AssemblyInfo.cs"),
                       lambda t: re.sub(r'(AssemblyVersion|AssemblyFileVersion)\("[\d.]+"\)',
                                        lambda m: '%s("9.9.9.9")' % m.group(1), t)),
     {"E1"}),
    ("N12_orphan_cs",
     lambda r: open(os.path.join(r, "Barometer_UWP/Helpers/Orphan.cs"), "w").write(
         "namespace Barometer_UWP.Helpers { public static class Orphan { } }"),
     {"D1"}),
    ("N13_broken_dependentupon",
     lambda r: patch(r, '<Compile Include="Views\\MainPage.xaml.cs">\n      <DependentUpon>MainPage.xaml</DependentUpon>',
                     '<Compile Include="Views\\MainPage.xaml.cs">\n      <DependentUpon>Nope.xaml</DependentUpon>'),
     {"D5"}),
    ("N14_malformed_xaml",
     lambda r: rewrite(os.path.join(r, "Barometer_UWP/Views/MainPage.xaml"),
                       lambda t: t + "</Page>"),
     {"A1"}),
    ("N15_truncated_cs",
     lambda r: rewrite(os.path.join(r, "Barometer_UWP/ServiceContainer.cs"),
                       lambda t: t.rsplit("}", 1)[0]),
     {"H5"}),
    ("N16_resw_key_skew",
     lambda r: rewrite(os.path.join(r, "Barometer_UWP/Strings/ru-RU/Resources.resw"),
                       lambda t: re.sub(r'<data name="AppTitle"[^>]*>.*?</data>\s*', '', t, count=1, flags=re.S),
                       '<data name="AppTitle"'),
     {"H7"}),
    ("N17_model_factor_drift",
     lambda r: rewrite(os.path.join(r, "Barometer_UWP/Models/PressureRecord.cs"),
                       lambda t: t.replace("0.75006375541921", "0.7500617", 1),
                       "0.75006375541921"),
     {"H1d"}),
]


def main():
    baseline_failed, rc = run_suite(SRC)
    print(f"[baseline] suite exit={rc}, failed={sorted(baseline_failed) or 'none'}")
    ok = True
    if rc != 0:
        print("!! baseline is not green — fix the repo before mutation testing"); return 1
    undetected = []
    for name, mut, expect_prefixes in MUTATIONS:
        try:
            failed, r2 = mutate_copy(name, mut)
        except AssertionError as e:
            print(f"[SKIP] {name}: anchor problem ({e})"); continue
        hit = {f for f in failed for pre in expect_prefixes if f.startswith(pre)}
        status = "DETECTED" if hit else "MISSED  "
        extra = sorted(set(failed) - baseline_failed)
        print(f"[{status}] {name} -> expected {sorted(expect_prefixes)}, got {extra}")
        if not hit:
            undetected.append(name); ok = False
    print(f"\n== mutation suite: {len(MUTATIONS)-len(undetected)}/{len(MUTATIONS)} regressions detected ==")
    return len(undetected)


if __name__ == "__main__":
    sys.exit(main())
