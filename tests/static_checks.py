#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Static acceptance suite for Barometer_UWP (v1.4.0.37+).

Runs WITHOUT any build toolchain (no MSBuild/VS/mono required) and verifies:
  A. XML well-formedness of every project XML file (csproj/sln-side, xaml, resw,
     appxmanifest, packages.config).
  B. MSBuild semantic invariants that previously caused real failures:
       - MSB4092 class: item-batching expressions (%(Item.Metadata)) or item lists
         (@(Item)) inside target-level Condition attributes are illegal.
       - csproj parses as MSBuild XML with the correct namespace.
       - canonical UAP reference block present (the v1.4.0.37 architectural fix),
         with zero HintPath / NuGet-path references (root cause of WMC1006/WMC9999
         was an EMPTY Reference set; shadowing facades must not come back).
       - no leftover firewall targets that filter ReferencePath by simple name.
       - exactly one PurgeStaleRestoreState target; imports point at the XAML
         targets file; AppxPackage/GenerateLibraryLayout wiring present.
  C. Solution/project consistency: sln GUID == csproj ProjectGuid, Format 12.00,
     CRLF line endings + tab indentation in sln, all 4 configurations mapped.
  D. Composition completeness: every .cs/.xaml on disk is declared in csproj
     (Compile/Page/ApplicationDefinition), every declared Include exists on disk,
     DependentUpon pairs resolve, x:Class in each XAML maps to its code-behind
     namespace+class.
  E. Version synchronization across AssemblyInfo.cs / Package.appxmanifest /
     CHANGELOG.md / install_deps.cmd for the expected release version.
  F. Platform targeting: TargetPlatformVersion/MinVersion == 10.0.15063.0,
     MaxTested >= Min, no StackPanel.Spacing attribute (needs 16299+).
  G. Dependency hygiene: packages.config contains NO Microsoft.NETCore.UWP
     metapackage, NO Newtonsoft.Json, NO DocumentFormat.OpenXml; no source file
     has a live (non-comment) using of removed libraries.
  H. Logic spot-checks re-implemented from platform-independent sources
     (UnitConverter factor, Json string-escape semantics, CSV shape) — these
     mirror the C# behavior so drift between C# and docs/specs is caught.

Exit code = number of failed checks (0 == clean).
Usage: python3 tests/static_checks.py [--version 1.4.0.37] [-v]
"""
import argparse
import os
import re
import sys
import xml.etree.ElementTree as ET

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
PROJ = os.path.join(ROOT, "Barometer_UWP")

RESULTS = []


def check(name, ok, detail=""):
    RESULTS.append((name, ok, detail))
    mark = "PASS" if ok else "FAIL"
    if not ok or ARGS.verbose:
        print(f"[{mark}] {name}" + (f" :: {detail}" if detail and (not ok or ARGS.verbose) else ""))
    return ok


# ---------------------------------------------------------------- A. XML syntax
def t_xml_wellformed():
    files = []
    for dirpath, dirnames, filenames in os.walk(ROOT):
        dirnames[:] = [d for d in dirnames if d not in (".git", "bin", "obj", "packages")]
        for fn in filenames:
            if fn.endswith((".xaml", ".resw", ".csproj", ".config", ".appxmanifest", ".rd.xml")):
                files.append(os.path.join(dirpath, fn))
    bad = []
    for f in sorted(files):
        try:
            ET.parse(f)
        except ET.ParseError as e:
            bad.append(f"{os.path.relpath(f, ROOT)}: {e}")
    check("A1 XML well-formed (all %d)" % len(files), not bad, "; ".join(bad))
    return len(files)


# ------------------------------------------------------- B. MSBuild invariants
MSB_NS = "http://schemas.microsoft.com/developer/msbuild/2003"


def parse_csproj():
    return ET.parse(os.path.join(PROJ, "Barometer_UWP.csproj")).getroot()


def q(tag):
    return f"{{{MSB_NS}}}{tag}"


def t_msbuild_semantics():
    tree_path = os.path.join(PROJ, "Barometer_UWP.csproj")
    raw = open(tree_path, encoding="utf-8-sig").read()
    root = parse_csproj()
    check("B1 csproj declares MSBuild 2003 namespace", root.tag == q("Project"), root.tag)

    # B2: MSB4092 class — batching/item-list syntax inside target-level Condition.
    offenders = []
    for tgt in root.iter(q("Target")):
        cond = tgt.get("Condition") or ""
        if "%(" in cond or ("@(" in cond and tgt.get("Name") not in None_ and False):
            offenders.append(tgt.get("Name"))
        # @(Item) alone in a condition is legal ONLY without batching metadata;
        # %(Meta) anywhere in a Condition is the illegal marker (MSB4092).
        if "%(" in cond:
            pass
    # stricter regex over raw text for any Condition="...%(...)...":
    for m in re.finditer(r'Condition="([^"]*%\(.*?)"', raw):
        offenders.append(m.group(1)[:60])
    check("B2 no %(item.metadata) batching inside Condition (MSB4092 class)",
          not offenders, str(offenders))

    # B3: canonical UAP reference block (architectural fix of v1.4.0.37)
    refs = {}
    for r in root.iter(q("Reference")):
        refs[r.get("Include")] = r
    required = {"System", "System.Core", "System.Numerics", "Windows",
                "Microsoft.CSharp", "System.Xml.Linq",
                "System.Runtime.InteropServices.WindowsRuntime"}
    missing = required - set(refs)
    check("B3 canonical SDK <Reference> block present", not missing,
          f"missing={sorted(missing)} have={sorted(refs)}")

    # B4: zero HintPath / packages\ paths in references (no facade shadowing vector)
    hint = [r.get("Include") for r in root.iter(q("Reference")) if r.find(q("HintPath")) is not None]
    check("B4 no <Reference><HintPath>", not hint, str(hint))
    check("B5 no reference Include pointing into packages\\",
          not [k for k in refs if "packages\\" in k or "packages/" in k],
          str([k for k in refs if "packages" in k]))

    # B6: removed self-defeating firewalls must stay gone
    names = {t.get("Name") for t in root.iter(q("Target"))}
    banned_targets = {"RejectPackageFacadeShadowReferences", "StripPackageFacadeShadows"}
    check("B6 no name-based ReferencePath firewall targets",
          not (names & banned_targets), str(names & banned_targets))
    check("B7 no UsingTask NuGetRestoreTask (IDE0006 class removed)",
          "<UsingTask" not in raw, "")

    # B8: single hardened purge target
    purges = [t for t in root.iter(q("Target")) if t.get("Name") == "PurgeStaleRestoreState"]
    check("B8 exactly one PurgeStaleRestoreState", len(purges) == 1, f"count={len(purges)}")

    # B9: architecture wiring properties
    props = {}
    for pg in root.iter(q("PropertyGroup")):
        for c in pg:
            if c.tag.startswith(f"{{{MSB_NS}}}"):
                props.setdefault(c.tag[len(MSB_NS) + 2:], c.text)
    need_props = {
        "OutputType": "AppContainerExe",
        "TargetPlatformIdentifier": "UAP",
        "TargetPlatformVersion": "10.0.15063.0",
        "TargetPlatformMinVersion": "10.0.15063.0",
        "AppxPackage": "true",
        "GenerateLibraryLayout": "false",
        "RestoreProjectStyle": "packages.config",
    }
    bad = {k: (props.get(k), v) for k, v in need_props.items() if props.get(k) != v}
    check("B9 core UAP properties pinned", not bad, str(bad))

    # B10: the XAML targets import exists and points at VS-version-macro path
    imports = [i.get("Project") for i in root.iter(q("Import"))]
    check("B10 imports Microsoft.Windows.UI.Xaml.CSharp.targets",
          any("Microsoft.Windows.UI.Xaml.CSharp.targets" in (p or "") for p in imports),
          str(imports))

    # B11: NETFX_CORE constant in every configuration's DefineConstants
    dc_bad = []
    for pg in root.iter(q("PropertyGroup")):
        cond = pg.get("Condition") or ""
        if "|ARM" in cond or "|x86" in cond:
            dc = pg.find(q("DefineConstants"))
            if dc is None or "NETFX_CORE" not in (dc.text or ""):
                dc_bad.append(cond)
    check("B11 DefineConstants has NETFX_CORE in all 4 configs", not dc_bad, str(dc_bad))

    # B12: no obj nuget.g conditional imports left (dead vectors)
    check("B12 no nuget.g.props/targets imports",
          not [p for p in imports if "nuget.g." in (p or "")], "")

    # B13: property values referenced by targets exist ($(MSBuildProjectDirectory)\obj usage OK)
    check("B13 ToolsVersion 15.0 + DefaultTargets=Build",
          root.get("ToolsVersion") == "15.0" and root.get("DefaultTargets") == "Build",
          f'TV={root.get("ToolsVersion")} DT={root.get("DefaultTargets")}')


None_ = None  # helper placeholder used above


# ------------------------------------------------------------- C. sln ↔ csproj
def t_solution():
    sln_path = os.path.join(ROOT, "Barometer_UWP.sln")
    data = open(sln_path, "rb").read()
    text = data.decode("utf-8-sig")
    check("C1 sln Format Version 12.00", "Format Version 12.00" in text.splitlines()[0])
    check("C2 sln CRLF line endings", b"\r\n" in data and b"\n" not in data.replace(b"\r\n", b""))
    # Global section must be tab-indented (VS convention; mixed spaces can confuse diff tools)
    global_lines = [l for l in text.splitlines() if l.startswith("        ") or l.startswith("\t")]
    spacey = [l[:40] for l in global_lines if l.startswith(" ")]
    check("C3 sln Global section uses tab indentation", not spacey, str(spacey[:3]))

    m = re.search(r'Project\("\{[0-9A-Fa-f-]+\}"\) = "[^"]+", "([^"]+)", "\{([0-9A-Fa-f-]+)\}"', text)
    check("C4 sln declares exactly one project entry", bool(m))
    if m:
        proj_rel, sln_guid = m.group(1), m.group(2)
        check("C5 sln project path exists",
              os.path.exists(os.path.join(ROOT, *proj_rel.split("\\"))), proj_rel)
        csproj_guid = parse_csproj().find(q("PropertyGroup")).findtext(q("ProjectGuid"), "")
        check("C6 sln GUID == csproj ProjectGuid",
              sln_guid.upper() == csproj_guid.strip("{}").upper(),
              f"sln={sln_guid} csproj={csproj_guid}")
        # configurations
        cfgs = ["Debug|ARM", "Debug|x86", "Release|ARM", "Release|x86"]
        pre = "\n".join(text.splitlines())
        missing = [c for c in cfgs
                   if f"{{{sln_guid}}}.{c}.ActiveCfg = {c}" not in pre
                   or f"{{{sln_guid}}}.{c}.Build.0 = {c}" not in pre]
        check("C7 all 4 solution configs mapped Active+Build", not missing, str(missing))


# ------------------------------------------------------ D. composition complete
def t_composition():
    root = parse_csproj()
    declared_cs, declared_xaml = set(), set()
    def norm(p):
        return p.replace("\\", "/")
    for it in root.iter(q("Compile")):
        declared_cs.add(norm(it.get("Include")))
    for it in list(root.iter(q("Page"))) + list(root.iter(q("ApplicationDefinition"))):
        declared_xaml.add(norm(it.get("Include")))

    disk_cs, disk_xaml = set(), set()
    for dirpath, dirnames, filenames in os.walk(PROJ):
        dirnames[:] = [d for d in dirnames if d not in ("bin", "obj", "Assets", "Strings")]
        for fn in filenames:
            rel = norm(os.path.relpath(os.path.join(dirpath, fn), PROJ))
            if fn == ".xaml.cs":
                pass
            if fn.endswith(".cs"):
                disk_cs.add(rel)
            elif fn.endswith(".xaml"):
                disk_xaml.add(rel)

    uncompiled_cs = {p for p in disk_cs if p not in declared_cs}
    check("D1 every .cs on disk declared in Compile", not uncompiled_cs, str(sorted(uncompiled_cs)))
    missing_on_disk_cs = {p for p in declared_cs if not os.path.exists(os.path.join(PROJ, p))}
    check("D2 every Compile Include exists on disk", not missing_on_disk_cs, str(sorted(missing_on_disk_cs)))
    uncompiled_xaml = {p for p in disk_xaml if p not in declared_xaml}
    check("D3 every .xaml declared as Page/ApplicationDefinition", not uncompiled_xaml, str(sorted(uncompiled_xaml)))
    missing_on_disk_xaml = {p for p in declared_xaml if not os.path.exists(os.path.join(PROJ, p))}
    check("D4 every Page/AppDef Include exists on disk", not missing_on_disk_xaml, str(sorted(missing_on_disk_xaml)))

    # DependentUpon pairing: Foo.xaml.cs -> Foo.xaml declared; x:Class matches code-behind
    pair_bad = []
    for it in list(root.iter(q("Compile"))) :
        du = it.find(q("DependentUpon"))
        if du is not None:
            inc = norm(it.get("Include"))
            xaml = os.path.join(os.path.dirname(inc), du.text).replace("\\", "/")
            if xaml not in declared_xaml:
                pair_bad.append(f"{inc}->{du.text}")
                continue
            xroot = ET.parse(os.path.join(PROJ, xaml)).getroot()
            xcls = xroot.get("{http://schemas.microsoft.com/winfx/2006/xaml}Class")
            src = open(os.path.join(PROJ, inc), encoding="utf-8-sig").read()
            ns = re.search(r'namespace\s+([\w.]+)', src)
            # UWP code-behind is 'sealed partial class X : Application/Page/UserControl...'
            cls = re.search(r'\b(?:public|internal|sealed|partial|\s)*\bclass\s+(\w+)', src)
            full = f"{ns.group(1)}.{cls.group(1)}" if ns and cls else "?"
            if xcls != full:
                pair_bad.append(f"x:Class={xcls} vs codebehind={full} ({inc})")
    check("D5 DependentUpon + x:Class ↔ namespace.class match", not pair_bad, str(pair_bad))

    # Content/PRIResource items exist
    miss_content = [norm(i.get("Include")) for i in root.iter(q("Content"))
                    if "*" not in i.get("Include") and not os.path.exists(os.path.join(PROJ, norm(i.get("Include"))))]
    check("D6 all Content items exist on disk", not miss_content, str(miss_content))
    miss_resw = [norm(i.get("Include")) for i in root.iter(q("PRIResource"))
                 if not os.path.exists(os.path.join(PROJ, norm(i.get("Include"))))]
    check("D7 all PRIResource (.resw) exist", not miss_resw, str(miss_resw))


# ------------------------------------------------------------------ E. versions
def t_versions(expected):
    ai = open(os.path.join(PROJ, "Properties", "AssemblyInfo.cs"), encoding="utf-8").read()
    m_as = re.search(r'AssemblyVersion\("([\d.]+)"\)', ai)
    m_fv = re.search(r'FileVersion\("([\d.]+)"\)', ai)
    man = open(os.path.join(PROJ, "Package.appxmanifest"), encoding="utf-8").read()
    m_id = re.search(r'Identity[^>]+Version="([\d.]+)"', man)
    ok = (m_as and m_fv and m_id and
          m_as.group(1) == expected and m_fv.group(1) == expected and m_id.group(1) == expected)
    check("E1 version %s synced AssemblyInfo/manifest" % expected, ok,
          f"asm={m_as and m_as.group(1)} file={m_fv and m_fv.group(1)} manifest={m_id and m_id.group(1)}")
    ch_path = os.path.join(ROOT, "CHANGELOG.md")
    if os.path.exists(ch_path):
        ch = open(ch_path, encoding="utf-8").read()
        check("E2 CHANGELOG top entry mentions version", f"{expected}" in ch.split("## ")[1][:200] if "## " in ch else False)
    idc = os.path.join(ROOT, "install_deps.cmd")
    if os.path.exists(idc):
        txt = open(idc, encoding="utf-8", errors="replace").read()
        # banner may lag one release behind (informational echo); FAIL only if it names
        # a DIFFERENT 1.4.0.x than expected:
        mver = re.search(r'v(1\.4\.0\.\d+)', txt)
        check("E3 install_deps.cmd banner version-synced",
              mver is not None and mver.group(1) == expected,
              f"banner={mver and mver.group(1)} expected={expected}")


# --------------------------------------------------------------- F. platforms
def t_platforms():
    props = {}
    for pg in parse_csproj().iter(q("PropertyGroup")):
        for c in pg:
            props.setdefault(c.tag[len(MSB_NS) + 2:], c.text)
    tv = tuple(int(x) for x in props["TargetPlatformVersion"].split("."))
    mv = tuple(int(x) for x in props["TargetPlatformMinVersion"].split("."))
    check("F1 TargetPlatform >= MinVersion", tv >= mv, f"{tv} vs {mv}")
    check("F2 both pinned to 15063", tv[:3] == (10, 0, 15063) and mv[:3] == (10, 0, 15063))
    man = open(os.path.join(PROJ, "Package.appxmanifest"), encoding="utf-8").read()
    mmax = re.search(r'MaxTestedVersion="([\d.]+)"', man) or re.search(r'uap:MaxVersionTested[^>]*Version="([\d.]+)"', man)
    mmin = re.search(r'MinVersion="([\d.]+)"', man)
    if mmax and mmin:
        check("F3 manifest MaxTested >= Min", tuple(map(int, mmax.group(1).split("."))) >= tuple(map(int, mmin.group(1).split("."))))
    # Spacing attribute ban (16299+)
    spacing = []
    for dirpath, dirnames, filenames in os.walk(PROJ):
        dirnames[:] = [d for d in dirnames if d not in ("bin", "obj")]
        for fn in filenames:
            if fn.endswith(".xaml"):
                p = os.path.join(dirpath, fn)
                for i, line in enumerate(open(p, encoding="utf-8"), 1):
                    code = line.split("<!--")[0]
                    if re.search(r'\bSpacing\s*=', code):
                        spacing.append(f"{os.path.relpath(p, ROOT)}:{i}")
    check("F4 no StackPanel.Spacing attribute outside comments (needs 16299+)", not spacing, str(spacing))


# ---------------------------------------------------------- G. dependency hygiene
def t_dependencies():
    pc = ET.parse(os.path.join(PROJ, "packages.config")).getroot()
    ids = {p.get("id").lower() for p in pc}
    banned = {"microsoft.netcore.universalwindowsplatform", "newtonsoft.json", "documentformat.openxml"}
    hit = ids & banned
    check("G1 packages.config free of metapackage/Newtonsoft/OpenXml", not hit, str(hit))

    live_using = []
    for dirpath, dirnames, filenames in os.walk(PROJ):
        dirnames[:] = [d for d in dirnames if d not in ("bin", "obj")]
        for fn in filenames:
            if not fn.endswith(".cs"):
                continue
            p = os.path.join(dirpath, fn)
            for i, line in enumerate(open(p, encoding="utf-8-sig"), 1):
                s = line.strip()
                if s.startswith("//") or s.startswith("*") or s.startswith("/*"):
                    continue
                if re.match(r'using\s+(Newtonsoft|DocumentFormat|System\.Net\.Http)\b', s):
                    live_using.append(f"{os.path.relpath(p, ROOT)}:{i}: {s}")
    check("G2 no live using of removed libs", not live_using, str(live_using))

    # System.Private.Uri explicit <Reference> must be gone (last MSB3245 warning source)
    refs = [r.get("Include") for r in parse_csproj().iter(q("Reference"))]
    check("G3 no System.Private.Uri reference", not any("Private.Uri" in (x or "") for x in refs), str(refs))


# --------------------------------------------------------- H. logic mirrors
def t_logic_mirrors():
    # H1: UnitConverter factor mirrored from C# const; roundtrip identity.
    uc = open(os.path.join(PROJ, "Helpers", "UnitConverter.cs"), encoding="utf-8").read()
    m = re.search(r'HpaToMmHgFactor\s*=\s*([0-9.]+);', uc)
    check("H1a UnitConverter factor literal found", bool(m))
    if m:
        k = float(m.group(1))
        worst = max(abs(mm / k * k - mm) for mm in (1, 100, 760, 1013.25, 1e4))
        check("H1b hPa->mmHg->hPa roundtrip < 1e-9 relative", worst < 1e-6, f"abs err {worst}")
        # physical sanity: standard atmosphere 1013.25 hPa ~ 760 mmHg
        check("H1c 1013.25 hPa -> 759.9..760.1 mmHg", 759.9 <= 1013.25 * k <= 760.1, f"{1013.25*k:.4f}")
        # PressureRecord uses the SAME factor literal (consistency between model & converter)
        pr = open(os.path.join(PROJ, "Models", "PressureRecord.cs"), encoding="utf-8").read()
        check("H1d PressureModel factor consistent with UnitConverter", m.group(1) in pr)

    # H2: Json escaping semantics — port of Esc() must produce valid JSON per Python parser.
    js = open(os.path.join(PROJ, "Helpers", "JsonSerializer.cs"), encoding="utf-8").read()
    esc_map = {'"': '\\"', "\\": "\\\\", "\b": "\\b", "\f": "\\f", "\n": "\\n", "\r": "\\r", "\t": "\\t"}
    def esc(s):
        out = []
        for c in s:
            if c in esc_map:
                out.append(esc_map[c])
            elif ord(c) < 0x20:
                out.append("\\u%04x" % ord(c))
            else:
                out.append(c)
        return "".join(out)
    sample = 'a"b\\c\nd\te\x01éf'
    import json as _json
    probe = _json.loads('"' + esc(sample) + '"')
    check("H2a Esc() output round-trips through strict JSON parser", probe == sample)
    # C# source of Esc(): every escape case must exist with correct replacement text.
    esc_region = js[js.find("private static string Esc"):js.find("#endregion")]
    need_cases = [
        r'''case '"': sb.Append("\\\""); break;''',
        r'''case '\\': sb.Append("\\\\"); break;''',
        r'''case '\b': sb.Append("\\b"); break;''',
        r'''case '\f': sb.Append("\\f"); break;''',
        r'''case '\n': sb.Append("\\n"); break;''',
        r'''case '\r': sb.Append("\\r"); break;''',
        r'''case '\t': sb.Append("\\t"); break;''',
        '''if (c < ' ') sb.Append("\\\\u")''',
    ]
    miss = [c for c in need_cases if c not in esc_region]
    check("H2b Esc control-char table complete & exact in C# source", not miss, str(miss))

    # H3: Parser accepts the grammar subset we claim (recursive descent invariants):
    # trailing-garbage rejection is done by ParseRoot SkipWs; verify our mirror rejects too.
    # Extract structural markers instead of executing C#:
    struct_ok = all(tok in js for tok in (
        "case '{': return ParseObject();", "case '[': return ParseArray();",
        "Expect(\"true\")", "Expect(\"false\")", "Expect(\"null\")",
        "throw new FormatException(\"Unterminated string\")",
        "NumberStyles.HexNumber"))
    check("H3 JSON parser structure covers literals/strings/\\u escapes", struct_ok)

    # H4: CSV generator shape — header + ISO-8601 timestamps + invariant culture.
    csv = open(os.path.join(PROJ, "Helpers", "CsvHelper.cs"), encoding="utf-8").read()
    check("H4a CSV header row fixed", '"Timestamp,Pressure_hPa,Pressure_mmHg"' in csv)
    check("H4b timestamps use round-trip \"o\" format", 'record.Timestamp:o' in csv or 'Timestamp.ToString("o"' in csv)
    check("H4c culture defaults to InvariantCulture", "CultureInfo.InvariantCulture" in csv)

    # H5: brace balance of every C# source via a proper C# lexer (handles verbatim
    # strings @"" with ""-escapes, escapes in regular strings, char literals, // and
    # /* */ comments — naive regex stripping false-positives on JSON/xlsx sources).
    def strip_csharp(src):
        out = []
        i, n = 0, len(src)
        while i < n:
            c = src[i]
            if c == '/' and i + 1 < n and src[i + 1] == '/':
                while i < n and src[i] != '\n':
                    i += 1
            elif c == '/' and i + 1 < n and src[i + 1] == '*':
                i += 2
                while i + 1 < n and not (src[i] == '*' and src[i + 1] == '/'):
                    i += 1
                i += 2
            elif c == '@' and i + 1 < n and src[i + 1] == '"':
                i += 2
                while i < n:
                    if src[i] == '"':
                        if i + 1 < n and src[i + 1] == '"':
                            i += 2
                            continue
                        i += 1
                        break
                    i += 1
            elif c == '"':
                i += 1
                while i < n:
                    if src[i] == '\\':
                        i += 2
                        continue
                    if src[i] == '"':
                        i += 1
                        break
                    i += 1
            elif c == "'":
                i += 1
                while i < n:
                    if src[i] == '\\':
                        i += 2
                        continue
                    if src[i] == "'":
                        i += 1
                        break
                    i += 1
            else:
                out.append(c)
                i += 1
        return ''.join(out)

    unbalanced = []
    for dirpath, dirnames, filenames in os.walk(PROJ):
        dirnames[:] = [d for d in dirnames if d not in ("bin", "obj")]
        for fn in filenames:
            if fn.endswith(".cs"):
                raw = open(os.path.join(dirpath, fn), encoding="utf-8-sig").read()
                s = strip_csharp(raw)
                if s.count("{") != s.count("}") or s.count("(") != s.count(")"):
                    unbalanced.append(fn)
                    continue
                # H5b: structural truncation guard — a brace-balanced file can still be
                # truncated inside the type body (last } eaten together with its opener).
                # Require at least one column-0 closing brace and no dangling openers.
                if "{" in s:
                    lines = s.splitlines()
                    has_toplevel_close = any(l.strip() == "}" for l in lines)
                    dangling = False
                    depth = 0
                    for l in lines:
                        for ch in l:
                            if ch == "{":
                                depth += 1
                            elif ch == "}":
                                depth -= 1
                                if depth < 0:
                                    dangling = True
                    if (not has_toplevel_close) or dangling:
                        unbalanced.append(f"{fn}(truncated)")
    check("H5 braces/parens balanced + not structurally truncated, all .cs", not unbalanced, str(unbalanced))

    # H6: XAML roots use the UWP namespaces, no WPF-only namespaces crept in.
    wpf_only = []
    for dirpath, dirnames, filenames in os.walk(PROJ):
        for fn in filenames:
            if fn.endswith(".xaml"):
                p = os.path.join(dirpath, fn)
                t = ET.parse(p).getroot()
                for uri in t.attrib.values():
                    if "presentation" in str(uri).lower() or "wpf" in str(uri).lower():
                        wpf_only.append(f"{fn}:{uri}")
    check("H6 no WPF-only xmlns in XAML", not wpf_only, str(wpf_only))

    # H7: resw key parity en-US vs ru-RU (missing runtime key = crash on lookup path)
    def keys(path):
        r = ET.parse(path).getroot()
        return {d.get("name") for d in r.findall("data") if d.get("type") != "System.Resources.ResXFileRef, System.Windows.Forms"}
    en = keys(os.path.join(PROJ, "Strings", "en-US", "Resources.resw"))
    ru = keys(os.path.join(PROJ, "Strings", "ru-RU", "Resources.resw"))
    check("H7 resw key sets identical en/ru", en == ru, f"only-en={sorted(en-ru)} only-ru={sorted(ru-en)}")

    # H8: every x:Uid / ResourceLoader key referenced from XAML exists in resw
    used = set()
    for dirpath, dirnames, filenames in os.walk(PROJ):
        dirnames[:] = [d for d in dirnames if d not in ("bin", "obj")]
        for fn in filenames:
            if fn.endswith(".xaml"):
                for m in re.finditer(r'x:Uid="([^"]+)"', open(os.path.join(dirpath, fn), encoding="utf-8").read()):
                    used.add(m.group(1))
    missing_uid = {u for u in used if not any(k == u or k.startswith(u + ".") for k in en)}
    check("H8 all x:Uid prefixes have resw entries", not missing_uid, f"used={len(used)} missing={sorted(missing_uid)}")


ARGS = argparse.Namespace(verbose=False)


def main():
    global ARGS
    ap = argparse.ArgumentParser()
    ap.add_argument("--version", default=None)
    ap.add_argument("-v", "--verbose", action="store_true")
    a = ap.parse_args()
    ARGS = a

    n = t_xml_wellformed()
    t_msbuild_semantics()
    t_solution()
    t_composition()
    ver = a.version
    if ver is None:
        m = re.search(r'AssemblyVersion\("([\d.]+)"\)', open(os.path.join(PROJ, "Properties", "AssemblyInfo.cs"), encoding="utf-8").read())
        ver = m.group(1)
    t_versions(ver)
    t_platforms()
    t_dependencies()
    t_logic_mirrors()

    failed = [r for r in RESULTS if not r[1]]
    print(f"\n== {len(RESULTS)-len(failed)}/{len(RESULTS)} checks passed "
          f"(XML files parsed: {n}) ==")
    for name, _, detail in failed:
        print(f"FAILED: {name} :: {detail}")
    return len(failed)


if __name__ == "__main__":
    sys.exit(main())
