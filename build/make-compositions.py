#!/usr/bin/env python3
"""Writes 3DEngine/Api/UnicodeCompositions.cs, Unicode's canonical compositions of two characters
into one, as NFC composes them, from the character data of the Python that runs it, so the engine
composes a letter and its mark into the one character for both with no help from the platform,
whose string.Normalize hands text back unchanged in a program published with invariant
globalization. A pair is a character and a mark NFC makes one character of, so the decompositions
Unicode excludes from composition are left out. Hangul's syllables are composed by Unicode's
formula, in the file's code, and not listed."""
import os, unicodedata

composes, decompositions = [], {}
for c in range(0x110000):
    d = unicodedata.decomposition(chr(c))
    if not d or d.startswith("<"): continue
    decompositions[c] = [int(x, 16) for x in d.split()]
    parts = decompositions[c]
    if len(parts) == 2 and unicodedata.normalize("NFC", chr(parts[0]) + chr(parts[1])) == chr(c):
        composes.append((parts[0], parts[1], c))
# Every character a composition starts from or makes, after it every mark one adds and every
# character that is only those marks' canonical spelling, as U+0340 is U+0300's and U+0344 the
# diaeresis and acute, each pair kept where NFC makes it one character, which takes in a mark
# stored before one that canonically comes first, as a circumflex before a dot below.
seconds = {b for _, b, _ in composes}
marks = seconds | {c for c, parts in decompositions.items() if all(p in seconds for p in parts)}
starts = {a for a, _, _ in composes} | {c for _, _, c in composes}
pairs = set()
for a in starts:
    for mark in marks:
        both = unicodedata.normalize("NFC", chr(a) + chr(mark))
        if len(both) == 1: pairs.add((a << 42) | (mark << 21) | ord(both))
pairs = sorted(pairs)
rows = [", ".join(f"0x{p:016X}" for p in pairs[i:i + 5]) + "," for i in range(0, len(pairs), 5)]
table = "\n".join("        " + row for row in rows)

path = os.path.join(os.path.dirname(__file__), "..", "3DEngine", "Api", "UnicodeCompositions.cs")
with open(path, "w", newline="\n") as f:
    f.write(f"""// Written by build/make-compositions.py from Unicode {unicodedata.unidata_version}'s character data, and written
// again by it rather than edited.
namespace Engine;

/// <summary>
/// Unicode's canonical compositions of a character and the mark after it into the one character
/// for both, as NFC composes them: e and a combining acute into é, and Hangul's letters into their
/// syllables.
/// </summary>
internal static class UnicodeCompositions
{{
    /// <summary>The character <paramref name="first"/> and <paramref name="second"/> compose into, or null where they compose into none.</summary>
    public static int? Of(int first, int second)
    {{
        // Hangul's syllables, by Unicode's formula: a leading consonant and a vowel, then that
        // syllable and a trailing consonant.
        if (first is >= 0x1100 and < 0x1113 && second is >= 0x1161 and < 0x1176)
            return 0xAC00 + ((first - 0x1100) * 21 + second - 0x1161) * 28;
        if (first is >= 0xAC00 and < 0xD7A4 && (first - 0xAC00) % 28 == 0 && second is > 0x11A7 and < 0x11C3)
            return first + second - 0x11A7;

        var key = (ulong)first << 21 | (uint)second;
        int lo = 0, hi = Pairs.Length - 1;
        while (lo <= hi)
        {{
            var mid = (lo + hi) >>> 1;
            var at = Pairs[mid] >> 21;
            if (at < key) lo = mid + 1;
            else if (at > key) hi = mid - 1;
            else return (int)(Pairs[mid] & 0x1FFFFF);
        }}
        return null;
    }}

    // Each pair and the character it composes into, 21 bits each from the high end, sorted.
    private static ReadOnlySpan<ulong> Pairs =>
    [
{table}
    ];
}}
""")
print(path, len(pairs), "pairs")
