using System.Globalization;
using System.Text;

namespace Engine;

/// <summary>
/// The order a line of text is shown in, by a reduced Unicode bidirectional algorithm (UAX #9). A
/// run of letters read right to left, Hebrew's or Arabic's, is shown from right to left among the
/// rest, a number in it keeps its own order, and a bracket in it is turned to face the right way.
/// </summary>
/// <remarks>
/// <para>
/// Each line is a paragraph of its own, its direction that of its first strong character (rules P2
/// and P3). The weak types are resolved by W1 to W7, the neutrals by N1 and N2 and the levels by
/// I1 and I2, a line's trailing white space is put back at the paragraph's level (L1), and the runs
/// are reversed from the highest level down (L2). The explicit embeddings, overrides and isolates,
/// U+202A to U+202E and U+2066 to U+2069, are passed over as the characters the rules ignore are,
/// and paired brackets are resolved as the other neutrals are, so N0 is not made.
/// </para>
/// <para>
/// A character's type is read from its block and its general category, which gives the scripts
/// written right to left and their numbers their own types and the rest neutral or left to right.
/// A line is reordered by its grapheme clusters, as <see cref="StringInfo"/> finds them, so a
/// letter's marks stay after it and a sequence of emoji joined into one, a flag or a keycap keeps
/// its own order, as the text's drawing expects (L3).
/// </para>
/// </remarks>
internal static class TextDirection
{
    internal enum Class : byte { L, R, AL, EN, ES, ET, AN, CS, NSM, BN, B, S, WS, ON }

    /// <summary>Whether a character of the text is read right to left, which a text of none shows as it is stored.</summary>
    public static bool HasRightToLeft(string text)
    {
        foreach (var rune in text.EnumerateRunes())
            if (Classify(rune.Value) is Class.R or Class.AL or Class.AN) return true;
        return false;
    }

    /// <summary>
    /// The line as it is shown from left to right, each grapheme cluster's characters in their own
    /// order, and a mirrored character, a bracket, turned where it is read right to left (L4).
    /// </summary>
    public static string Visual(string line)
    {
        if (line.Length == 0) return line;
        var starts = new List<int>();
        for (int i = 0; i < line.Length; i += StringInfo.GetNextTextElementLength(line, i)) starts.Add(i);
        int End(int cluster) => cluster + 1 < starts.Count ? starts[cluster + 1] : line.Length;

        var codepoints = new List<int>(line.Length);
        var first = new int[starts.Count];
        for (int c = 0; c < starts.Count; c++)
        {
            first[c] = codepoints.Count;
            foreach (var rune in line.AsSpan(starts[c], End(c) - starts[c]).EnumerateRunes()) codepoints.Add(rune.Value);
        }
        var levels = Levels([.. codepoints], out _);
        var clusterLevels = first.Select(i => levels[i]).ToArray();

        var shown = new StringBuilder(line.Length);
        foreach (var c in Order(clusterLevels))
        {
            var cluster = line.AsSpan(starts[c], End(c) - starts[c]);
            var head = codepoints[first[c]];
            if ((clusterLevels[c] & 1) == 1 && Mirrored(head) is { } turned)
            {
                shown.Append(char.ConvertFromUtf32(turned));
                cluster = cluster[char.ConvertFromUtf32(head).Length..];
            }
            shown.Append(cluster);
        }
        return shown.ToString();
    }

    /// <summary>The embedding level of each character of a line, even for left to right and odd for right to left, and the paragraph's in <paramref name="paragraph"/>.</summary>
    public static byte[] Levels(ReadOnlySpan<int> codepoints, out byte paragraph)
    {
        var n = codepoints.Length;
        var original = new Class[n];
        for (int i = 0; i < n; i++) original[i] = Classify(codepoints[i]);

        // P2 and P3: the paragraph's direction is its first strong character's.
        paragraph = 0;
        foreach (var type in original)
        {
            if (type == Class.L) break;
            if (type is Class.R or Class.AL)
            {
                paragraph = 1;
                break;
            }
        }
        var sos = paragraph == 1 ? Class.R : Class.L;

        // The characters the rules read, those they ignore passed over (X9).
        var live = Enumerable.Range(0, n).Where(i => original[i] != Class.BN).ToArray();
        var t = live.Select(i => original[i]).ToArray();
        var m = t.Length;

        // W1: a mark takes the type of what it follows.
        for (int k = 0; k < m; k++)
            if (t[k] == Class.NSM) t[k] = k == 0 ? sos : t[k - 1];
        // W2: a European number after Arabic letters is an Arabic number.
        var strong = sos;
        for (int k = 0; k < m; k++)
        {
            if (t[k] is Class.L or Class.R or Class.AL) strong = t[k];
            else if (t[k] == Class.EN && strong == Class.AL) t[k] = Class.AN;
        }
        // W3: an Arabic letter is read as any letter read right to left.
        for (int k = 0; k < m; k++)
            if (t[k] == Class.AL) t[k] = Class.R;
        // W4: a single separator between two numbers of a kind joins them.
        for (int k = 1; k < m - 1; k++)
        {
            if (t[k] == Class.ES && t[k - 1] == Class.EN && t[k + 1] == Class.EN) t[k] = Class.EN;
            else if (t[k] == Class.CS && t[k - 1] == t[k + 1] && t[k - 1] is Class.EN or Class.AN) t[k] = t[k - 1];
        }
        // W5: terminators beside a European number, as a currency or a percent sign, join it.
        for (int k = 0; k < m;)
        {
            if (t[k] != Class.ET)
            {
                k++;
                continue;
            }
            var end = k;
            while (end < m && t[end] == Class.ET) end++;
            if (k > 0 && t[k - 1] == Class.EN || end < m && t[end] == Class.EN)
                for (int j = k; j < end; j++) t[j] = Class.EN;
            k = end;
        }
        // W6: the separators and terminators left are neutral.
        for (int k = 0; k < m; k++)
            if (t[k] is Class.ES or Class.ET or Class.CS) t[k] = Class.ON;
        // W7: a European number in left to right text is read as its letters are.
        strong = sos;
        for (int k = 0; k < m; k++)
        {
            if (t[k] is Class.L or Class.R) strong = t[k];
            else if (t[k] == Class.EN && strong == Class.L) t[k] = Class.L;
        }
        // N1 and N2: neutrals between two of one direction take it, and the rest the paragraph's.
        static Class Strong(Class type) => type == Class.L ? Class.L : Class.R;
        var embedding = paragraph == 1 ? Class.R : Class.L;
        for (int k = 0; k < m;)
        {
            if (t[k] is not (Class.B or Class.S or Class.WS or Class.ON))
            {
                k++;
                continue;
            }
            var end = k;
            while (end < m && t[end] is Class.B or Class.S or Class.WS or Class.ON) end++;
            var before = k == 0 ? sos : Strong(t[k - 1]);
            var after = end == m ? sos : Strong(t[end]);
            var direction = before == after ? before : embedding;
            for (int j = k; j < end; j++) t[j] = direction;
            k = end;
        }

        // I1 and I2: the levels.
        var levels = new byte[n];
        for (int k = 0; k < m; k++)
            levels[live[k]] = (byte)(paragraph + (paragraph == 0
                ? t[k] switch { Class.R => 1, Class.AN or Class.EN => 2, _ => 0 }
                : t[k] is Class.L or Class.EN or Class.AN ? 1 : 0));
        // An ignored character is at the level of the one before it.
        for (int i = 0; i < n; i++)
            if (original[i] == Class.BN) levels[i] = i == 0 ? paragraph : levels[i - 1];
        // L1: separators, and white space before one or at the line's end, are at the paragraph's level.
        var trailing = true;
        for (int i = n - 1; i >= 0; i--)
        {
            if (original[i] is Class.S or Class.B)
            {
                levels[i] = paragraph;
                trailing = true;
            }
            else if (trailing && original[i] is Class.WS or Class.BN) levels[i] = paragraph;
            else trailing = false;
        }
        return levels;
    }

    /// <summary>The indexes of the items in the order they are shown, every run at a level or above reversed, from the highest level down to the lowest odd one (L2).</summary>
    public static int[] Order(ReadOnlySpan<byte> levels)
    {
        var order = Enumerable.Range(0, levels.Length).ToArray();
        if (levels.Length == 0) return order;
        byte highest = 0, lowestOdd = byte.MaxValue;
        foreach (var level in levels)
        {
            highest = Math.Max(highest, level);
            if ((level & 1) == 1) lowestOdd = Math.Min(lowestOdd, level);
        }
        for (int level = highest; level >= lowestOdd && level > 0; level--)
            for (int i = 0; i < order.Length;)
            {
                if (levels[order[i]] < level)
                {
                    i++;
                    continue;
                }
                var end = i;
                while (end < order.Length && levels[order[end]] >= level) end++;
                Array.Reverse(order, i, end - i);
                i = end;
            }
        return order;
    }

    /// <summary>A character's bidirectional type, from its block where a script written right to left or its numbers have their own, and otherwise from its general category.</summary>
    internal static Class Classify(int c)
    {
        switch (c)
        {
            case >= '0' and <= '9': return Class.EN;
            case '+' or '-': return Class.ES;
            case '#' or '$' or '%': return Class.ET;
            case ',' or '.' or '/' or ':' or 0x00A0 or 0x060C or 0x202F or 0x2044 or 0xFE50 or 0xFE52 or 0xFE55 or 0xFF0C or 0xFF0E or 0xFF0F or 0xFF1A:
                return Class.CS;
            case '\t' or 0x0B or 0x1F: return Class.S;
            case '\n' or '\r' or 0x1C or 0x1D or 0x1E or 0x85 or 0x2029: return Class.B;
            case ' ' or 0x0C or 0x2028: return Class.WS;
            case 0x200E: return Class.L;
            case 0x200F: return Class.R;
            case 0x061C: return Class.AL;
            case 0x00AD or (>= 0x200B and <= 0x200D) or (>= 0x202A and <= 0x202E) or (>= 0x2060 and <= 0x2069) or 0xFEFF: return Class.BN;
            case 0x00A2 or 0x00A3 or 0x00A4 or 0x00A5 or 0x00B0 or 0x00B1 or 0x0609 or 0x060A or 0x066A or (>= 0x2030 and <= 0x2034)
                or (>= 0x20A0 and <= 0x20CF) or 0x212E or 0x2213 or 0xFE5F or 0xFE69 or 0xFE6A or (>= 0xFF03 and <= 0xFF05) or 0xFFE0 or 0xFFE1
                or 0xFFE5 or 0xFFE6:
                return Class.ET;
            case 0x207A or 0x207B or 0x208A or 0x208B or 0x2212 or 0xFB29 or 0xFE62 or 0xFE63 or 0xFF0B or 0xFF0D: return Class.ES;
            case 0x00B2 or 0x00B3 or 0x00B9 or (>= 0x06F0 and <= 0x06F9) or 0x2070 or (>= 0x2074 and <= 0x2079) or (>= 0x2080 and <= 0x2089)
                or (>= 0x2488 and <= 0x249B) or (>= 0xFF10 and <= 0xFF19) or (>= 0x1D7CE and <= 0x1D7FF) or (>= 0x1F100 and <= 0x1F10A):
                return Class.EN;
            case (>= 0x0600 and <= 0x0605) or (>= 0x0660 and <= 0x0669) or 0x066B or 0x066C or 0x06DD or 0x0890 or 0x0891 or 0x08E2
                or (>= 0x10E60 and <= 0x10E7E):
                return Class.AN;
            case < 0x20 or 0x7F: return Class.BN;
        }

        var category = c <= 0x10FFFF ? CharUnicodeInfo.GetUnicodeCategory(c) : UnicodeCategory.OtherNotAssigned;
        // The scripts written right to left, their marks and controls apart: Hebrew, NKo, Samaritan,
        // Mandaic and the historic scripts of the first plane read as Hebrew does, and Arabic,
        // Syriac and Thaana as Arabic does, with their presentation forms.
        var arabic = c is (>= 0x0600 and <= 0x07BF) or (>= 0x0860 and <= 0x08FF) or (>= 0xFB50 and <= 0xFDCF) or (>= 0xFDF0 and <= 0xFDFF)
            or (>= 0xFE70 and <= 0xFEFE) or (>= 0x10D00 and <= 0x10D3F) or (>= 0x10F30 and <= 0x10F6F) or (>= 0x1EC70 and <= 0x1ECBF)
            or (>= 0x1ED00 and <= 0x1ED4F) or (>= 0x1EE00 and <= 0x1EEFF);
        var hebrew = !arabic && c is (>= 0x0590 and <= 0x05FF) or (>= 0x07C0 and <= 0x085F) or (>= 0xFB1D and <= 0xFB4F)
            or (>= 0x10800 and <= 0x10FFF) or (>= 0x1E800 and <= 0x1EFFF);
        switch (category)
        {
            case UnicodeCategory.NonSpacingMark or UnicodeCategory.EnclosingMark: return Class.NSM;
            case UnicodeCategory.Format or UnicodeCategory.Control: return Class.BN;
            case UnicodeCategory.SpaceSeparator: return Class.WS;
        }
        if (arabic) return c is 0x06DE or 0x06E9 or 0xFD3E or 0xFD3F ? Class.ON : Class.AL;
        if (hebrew) return Class.R;
        return category switch
        {
            UnicodeCategory.UppercaseLetter or UnicodeCategory.LowercaseLetter or UnicodeCategory.TitlecaseLetter or UnicodeCategory.ModifierLetter
                or UnicodeCategory.OtherLetter or UnicodeCategory.SpacingCombiningMark or UnicodeCategory.LetterNumber or UnicodeCategory.DecimalDigitNumber
                or UnicodeCategory.PrivateUse or UnicodeCategory.Surrogate or UnicodeCategory.OtherNotAssigned => Class.L,
            _ => Class.ON,
        };
    }

    /// <summary>The character a mirrored one is turned into where it is read right to left, its pair, or null.</summary>
    internal static int? Mirrored(int c) => c switch
    {
        '(' => ')', ')' => '(', '[' => ']', ']' => '[', '{' => '}', '}' => '{', '<' => '>', '>' => '<',
        0x00AB => 0x00BB, 0x00BB => 0x00AB, 0x2039 => 0x203A, 0x203A => 0x2039, 0x2045 => 0x2046, 0x2046 => 0x2045,
        0x207D => 0x207E, 0x207E => 0x207D, 0x208D => 0x208E, 0x208E => 0x208D, 0x2264 => 0x2265, 0x2265 => 0x2264,
        0x2208 => 0x220B, 0x220B => 0x2208, 0x27E8 => 0x27E9, 0x27E9 => 0x27E8, 0x27EA => 0x27EB, 0x27EB => 0x27EA,
        0x3008 => 0x3009, 0x3009 => 0x3008, 0x300A => 0x300B, 0x300B => 0x300A, 0x300C => 0x300D, 0x300D => 0x300C,
        0x300E => 0x300F, 0x300F => 0x300E, 0x3010 => 0x3011, 0x3011 => 0x3010, 0xFF08 => 0xFF09, 0xFF09 => 0xFF08,
        0xFF1C => 0xFF1E, 0xFF1E => 0xFF1C, 0xFF3B => 0xFF3D, 0xFF3D => 0xFF3B, 0xFF5B => 0xFF5D, 0xFF5D => 0xFF5B,
        _ => null,
    };
}
