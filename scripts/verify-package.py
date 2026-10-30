"""Check the artifact that will be published, using only the Python standard library."""
import json
import pathlib
import re
import sys
import xml.etree.ElementTree as ET
import zipfile

def verify(package, version):
    match = re.fullmatch(r"(13|17|18)\.\d+\.\d+", version)
    if not match:
        raise ValueError("Expected a stable 13.x.x, 17.x.x or 18.x.x version")
    major = int(match[1])
    expected_range = f"[{major}.0.0,{major + 1}.0.0)"
    package_id = "Umbraco.Community.GraphMailSender"
    with zipfile.ZipFile(package) as archive:
        nuspec = [name for name in archive.namelist() if name.endswith(".nuspec")]
        if len(nuspec) != 1:
            raise ValueError("Expected exactly one nuspec")
        metadata = ET.fromstring(archive.read(nuspec[0])).find("{*}metadata")
        if metadata is None:
            raise ValueError("Missing package metadata")
        if metadata.findtext("{*}id") != package_id or metadata.findtext("{*}version") != version:
            raise ValueError("Incorrect package identity")
        dependencies = [item for item in metadata.findall(".//{*}dependency") if item.get("id", "").startswith("Umbraco.Cms.")]
        if not dependencies or any(item.get("version", "").replace(" ", "") != expected_range for item in dependencies):
            raise ValueError(f"Umbraco dependencies must use {expected_range}")
        tfm = "net8.0" if major == 13 else "net10.0"
        required = {"README.md", "icon.png", "THIRD-PARTY-NOTICES.txt", f"lib/{tfm}/{package_id}.dll",
                    f"appsettings-schema.{package_id}.json", f"buildTransitive/{package_id}.targets"}
        missing = required.difference(archive.namelist())
        if missing:
            raise ValueError(f"Missing files: {sorted(missing)}")
        json.loads(archive.read(f"appsettings-schema.{package_id}.json"))
        ET.fromstring(archive.read(f"buildTransitive/{package_id}.targets"))
        if metadata.findtext("{*}readme") != "README.md" or metadata.findtext("{*}icon") != "icon.png":
            raise ValueError("Missing readme/icon metadata")
    print(f"Verified {pathlib.Path(package).name}: {tfm}, {expected_range}, required assets present")

if __name__ == "__main__":
    if len(sys.argv) != 3:
        raise SystemExit("Usage: verify-package.py PACKAGE VERSION")
    verify(sys.argv[1], sys.argv[2])
