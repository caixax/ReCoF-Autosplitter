"""Write src/Presets.cs from the LiveSplit split files of the Cry of Fear categories.

usage: python tools/make_presets.py <folder with the .lss files>

Each preset takes its settings from the file's cryoffear.asl settings (the script's defaults, all on, when the
file has none) and lists the file's segments, so the component shows what will split without anyone typing maps.
"""
import os
import re
import sys
import xml.etree.ElementTree as ET

# file name prefix, preset name, in the order the list shows them
PRESETS = [
    ("Cry_of_Fear_Any_a4f204a4", "New Game / New Game+ (map splits)"),
    ("Cry_of_Fear_Any_Chapters", "New Game / New Game+ (chapter splits)"),
    ("Cry_of_Fear_All_Key_Items_NMG", "All Key Items + NMG"),
    ("Cry_of_Fear_-_The_Doctors_Story", "The Doctor's Story"),
    ("Cry_of_Fear_-_Any_Co-op", "Co-op"),
    ("Cry_of_Fear_-_Manhunt", "Manhunt"),
    ("Cry_of_Fear_Category_Extensions_-_Memories", "Memories"),
    ("Cry_of_Fear_Category_Extensions_-_Halloween_Collaboration", "Halloween Collaboration"),
    ("Cry_of_Fear_Category_Extensions_-_Community_Collaboration", "Community Collaboration"),
    ("Cry_of_Fear_Category_Extensions_-_The_Stairway", "The Stairway"),
]

DEFAULTS = {"Split on map change": True, "Split on chapter change": True, "Undo when loading the savefile": True, "Water glitch": True}


def cs(text):
    return '"' + text.replace("\\", "\\\\").replace('"', '\\"') + '"'


def segment_label(name):
    # "-c_start" is a subsplit, "{Chapter 1}c_apartmentsick" ends the group "Chapter 1"
    m = re.match(r"^\{([^}]*)\}(.*)$", name)
    if m:
        return "%s  (end of %s)" % (m.group(2), m.group(1))
    return name[1:] if name.startswith("-") else name


def main():
    folder = sys.argv[1]
    files = os.listdir(folder)
    out = []
    for prefix, title in PRESETS:
        match = [f for f in files if f.startswith(prefix) and f.endswith(".lss")]
        if len(match) != 1:
            sys.exit("%s: %d files" % (prefix, len(match)))
        root = ET.parse(os.path.join(folder, match[0])).getroot()
        settings = dict(DEFAULTS)
        asl = root.find("AutoSplitterSettings")
        if asl is not None:
            for s in asl.iter("Setting"):
                if s.get("id") in settings:
                    settings[s.get("id")] = s.text == "True"
        segments = [segment_label(s.findtext("Name")) for s in root.find("Segments")]
        out.append((title, match[0], settings, segments))

    lines = [
        "// Written by tools/make_presets.py from the split files of the Cry of Fear categories; do not edit.",
        "",
        "namespace LiveSplit.ReCoFSplit",
        "{",
        "    public sealed class Preset",
        "    {",
        "        public string Name;",
        "        public string SplitFile;",
        "        public bool MapChange, ChapterChange, WaterGlitch, UndoOnLoad;",
        "        public string[] Segments;",
        "    }",
        "",
        "    public static class Presets",
        "    {",
        "        public const string Custom = \"Custom\";",
        "",
        "        public static readonly Preset[] All =",
        "        {",
    ]
    for title, fname, st, segments in out:
        lines.append("            new Preset")
        lines.append("            {")
        lines.append("                Name = %s," % cs(title))
        lines.append("                SplitFile = %s," % cs(fname))
        lines.append("                MapChange = %s, ChapterChange = %s, WaterGlitch = %s, UndoOnLoad = %s," % tuple(
            "true" if st[k] else "false" for k in ("Split on map change", "Split on chapter change", "Water glitch", "Undo when loading the savefile")))
        lines.append("                Segments = new[]")
        lines.append("                {")
        for seg in segments:
            lines.append("                    %s," % cs(seg))
        lines.append("                },")
        lines.append("            },")
    lines += [
        "        };",
        "",
        "        public static Preset Find(string name)",
        "        {",
        "            foreach (Preset p in All)",
        "            {",
        "                if (p.Name == name)",
        "                    return p;",
        "            }",
        "",
        "            return null;",
        "        }",
        "    }",
        "}",
        "",
    ]
    path = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "src", "Presets.cs")
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(lines))
    for title, fname, st, segments in out:
        print("%-40s %3d segments  %s" % (title, len(segments), fname))


if __name__ == "__main__":
    main()
