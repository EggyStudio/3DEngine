using System.Globalization;

namespace Engine;

/// <summary>
/// How the letters of Arabic join: each character's joining type, the form a letter takes by the
/// letters beside it, and the presentation forms Unicode encodes for those forms, which a font with
/// no substitutions of its own for Arabic is drawn with.
/// </summary>
/// <remarks>
/// The joining types are Unicode 16's (ArabicShaping.txt) for the Arabic script's blocks, U+0600 to
/// U+06FF, U+0750 to U+077F and U+0870 to U+08FF, the zero width joiner joining both ways as the
/// tatweel does, a mark or a format character transparent but for those the file says join
/// nothing, the zero width non-joiner (U+200C), the isolates and Arabic's signs that span digits
/// among them, and every other character joining nothing. The presentation forms are the isolated, final, initial and medial forms of Unicode's
/// two blocks of them, U+FB50 to U+FDFF and U+FE70 to U+FEFF, by their compatibility decompositions,
/// with the eight forms of lam joined to an alef.
/// </remarks>
internal static class ArabicJoining
{
    internal enum Joining : byte { None, Right, Dual, Causing, Transparent }

    /// <summary>The form a letter takes by the letters beside it, each a bit of a plan's mask for the feature that makes it.</summary>
    [Flags]
    internal enum Form : byte { None = 0, Isolated = 1, Final = 2, Medial = 4, Initial = 8 }

    // The characters that join on their right, both ways, or make the letters beside them join, in order.
    private static readonly (int First, int Last, Joining Type)[] Types =
    [
        (0x0600, 0x0605, Joining.None), (0x0620, 0x0620, Joining.Dual), (0x0622, 0x0625, Joining.Right), (0x0626, 0x0626, Joining.Dual),
        (0x0627, 0x0627, Joining.Right), (0x0628, 0x0628, Joining.Dual), (0x0629, 0x0629, Joining.Right),
        (0x062A, 0x062E, Joining.Dual), (0x062F, 0x0632, Joining.Right), (0x0633, 0x063F, Joining.Dual),
        (0x0640, 0x0640, Joining.Causing), (0x0641, 0x0647, Joining.Dual), (0x0648, 0x0648, Joining.Right),
        (0x0649, 0x064A, Joining.Dual), (0x066E, 0x066F, Joining.Dual), (0x0671, 0x0673, Joining.Right),
        (0x0675, 0x0677, Joining.Right), (0x0678, 0x0687, Joining.Dual), (0x0688, 0x0699, Joining.Right),
        (0x069A, 0x06BF, Joining.Dual), (0x06C0, 0x06C0, Joining.Right), (0x06C1, 0x06C2, Joining.Dual),
        (0x06C3, 0x06CB, Joining.Right), (0x06CC, 0x06CC, Joining.Dual), (0x06CD, 0x06CD, Joining.Right),
        (0x06CE, 0x06CE, Joining.Dual), (0x06CF, 0x06CF, Joining.Right), (0x06D0, 0x06D1, Joining.Dual),
        (0x06D2, 0x06D3, Joining.Right), (0x06D5, 0x06D5, Joining.Right), (0x06DD, 0x06DD, Joining.None), (0x06EE, 0x06EF, Joining.Right),
        (0x06FA, 0x06FC, Joining.Dual), (0x06FF, 0x06FF, Joining.Dual), (0x0750, 0x0758, Joining.Dual),
        (0x0759, 0x075B, Joining.Right), (0x075C, 0x076A, Joining.Dual), (0x076B, 0x076C, Joining.Right),
        (0x076D, 0x0770, Joining.Dual), (0x0771, 0x0771, Joining.Right), (0x0772, 0x0772, Joining.Dual),
        (0x0773, 0x0774, Joining.Right), (0x0775, 0x0777, Joining.Dual), (0x0778, 0x0779, Joining.Right),
        (0x077A, 0x077F, Joining.Dual), (0x0870, 0x0882, Joining.Right), (0x0883, 0x0885, Joining.Causing),
        (0x0886, 0x0886, Joining.Dual), (0x0889, 0x088D, Joining.Dual), (0x088E, 0x088E, Joining.Right),
        (0x0890, 0x0891, Joining.None), (0x08A0, 0x08A9, Joining.Dual), (0x08AA, 0x08AC, Joining.Right), (0x08AE, 0x08AE, Joining.Right),
        (0x08AF, 0x08B0, Joining.Dual), (0x08B1, 0x08B2, Joining.Right), (0x08B3, 0x08B8, Joining.Dual),
        (0x08B9, 0x08B9, Joining.Right), (0x08BA, 0x08C8, Joining.Dual), (0x08E2, 0x08E2, Joining.None),
    ];

    // A letter and its isolated, final, initial and medial presentation forms, 0 for a form it has not.
    private static readonly Dictionary<int, (int Isolated, int Final, int Initial, int Medial)> Presented = new (int Letter, int Isolated, int Final, int Initial, int Medial)[]
    {
        (0x0621, 0xFE80, 0x0000, 0x0000, 0x0000), (0x0622, 0xFE81, 0xFE82, 0x0000, 0x0000),
        (0x0623, 0xFE83, 0xFE84, 0x0000, 0x0000), (0x0624, 0xFE85, 0xFE86, 0x0000, 0x0000),
        (0x0625, 0xFE87, 0xFE88, 0x0000, 0x0000), (0x0626, 0xFE89, 0xFE8A, 0xFE8B, 0xFE8C),
        (0x0627, 0xFE8D, 0xFE8E, 0x0000, 0x0000), (0x0628, 0xFE8F, 0xFE90, 0xFE91, 0xFE92),
        (0x0629, 0xFE93, 0xFE94, 0x0000, 0x0000), (0x062A, 0xFE95, 0xFE96, 0xFE97, 0xFE98),
        (0x062B, 0xFE99, 0xFE9A, 0xFE9B, 0xFE9C), (0x062C, 0xFE9D, 0xFE9E, 0xFE9F, 0xFEA0),
        (0x062D, 0xFEA1, 0xFEA2, 0xFEA3, 0xFEA4), (0x062E, 0xFEA5, 0xFEA6, 0xFEA7, 0xFEA8),
        (0x062F, 0xFEA9, 0xFEAA, 0x0000, 0x0000), (0x0630, 0xFEAB, 0xFEAC, 0x0000, 0x0000),
        (0x0631, 0xFEAD, 0xFEAE, 0x0000, 0x0000), (0x0632, 0xFEAF, 0xFEB0, 0x0000, 0x0000),
        (0x0633, 0xFEB1, 0xFEB2, 0xFEB3, 0xFEB4), (0x0634, 0xFEB5, 0xFEB6, 0xFEB7, 0xFEB8),
        (0x0635, 0xFEB9, 0xFEBA, 0xFEBB, 0xFEBC), (0x0636, 0xFEBD, 0xFEBE, 0xFEBF, 0xFEC0),
        (0x0637, 0xFEC1, 0xFEC2, 0xFEC3, 0xFEC4), (0x0638, 0xFEC5, 0xFEC6, 0xFEC7, 0xFEC8),
        (0x0639, 0xFEC9, 0xFECA, 0xFECB, 0xFECC), (0x063A, 0xFECD, 0xFECE, 0xFECF, 0xFED0),
        (0x0641, 0xFED1, 0xFED2, 0xFED3, 0xFED4), (0x0642, 0xFED5, 0xFED6, 0xFED7, 0xFED8),
        (0x0643, 0xFED9, 0xFEDA, 0xFEDB, 0xFEDC), (0x0644, 0xFEDD, 0xFEDE, 0xFEDF, 0xFEE0),
        (0x0645, 0xFEE1, 0xFEE2, 0xFEE3, 0xFEE4), (0x0646, 0xFEE5, 0xFEE6, 0xFEE7, 0xFEE8),
        (0x0647, 0xFEE9, 0xFEEA, 0xFEEB, 0xFEEC), (0x0648, 0xFEED, 0xFEEE, 0x0000, 0x0000),
        (0x0649, 0xFEEF, 0xFEF0, 0xFBE8, 0xFBE9), (0x064A, 0xFEF1, 0xFEF2, 0xFEF3, 0xFEF4),
        (0x0671, 0xFB50, 0xFB51, 0x0000, 0x0000), (0x0677, 0xFBDD, 0x0000, 0x0000, 0x0000),
        (0x0679, 0xFB66, 0xFB67, 0xFB68, 0xFB69), (0x067A, 0xFB5E, 0xFB5F, 0xFB60, 0xFB61),
        (0x067B, 0xFB52, 0xFB53, 0xFB54, 0xFB55), (0x067E, 0xFB56, 0xFB57, 0xFB58, 0xFB59),
        (0x067F, 0xFB62, 0xFB63, 0xFB64, 0xFB65), (0x0680, 0xFB5A, 0xFB5B, 0xFB5C, 0xFB5D),
        (0x0683, 0xFB76, 0xFB77, 0xFB78, 0xFB79), (0x0684, 0xFB72, 0xFB73, 0xFB74, 0xFB75),
        (0x0686, 0xFB7A, 0xFB7B, 0xFB7C, 0xFB7D), (0x0687, 0xFB7E, 0xFB7F, 0xFB80, 0xFB81),
        (0x0688, 0xFB88, 0xFB89, 0x0000, 0x0000), (0x068C, 0xFB84, 0xFB85, 0x0000, 0x0000),
        (0x068D, 0xFB82, 0xFB83, 0x0000, 0x0000), (0x068E, 0xFB86, 0xFB87, 0x0000, 0x0000),
        (0x0691, 0xFB8C, 0xFB8D, 0x0000, 0x0000), (0x0698, 0xFB8A, 0xFB8B, 0x0000, 0x0000),
        (0x06A4, 0xFB6A, 0xFB6B, 0xFB6C, 0xFB6D), (0x06A6, 0xFB6E, 0xFB6F, 0xFB70, 0xFB71),
        (0x06A9, 0xFB8E, 0xFB8F, 0xFB90, 0xFB91), (0x06AD, 0xFBD3, 0xFBD4, 0xFBD5, 0xFBD6),
        (0x06AF, 0xFB92, 0xFB93, 0xFB94, 0xFB95), (0x06B1, 0xFB9A, 0xFB9B, 0xFB9C, 0xFB9D),
        (0x06B3, 0xFB96, 0xFB97, 0xFB98, 0xFB99), (0x06BA, 0xFB9E, 0xFB9F, 0x0000, 0x0000),
        (0x06BB, 0xFBA0, 0xFBA1, 0xFBA2, 0xFBA3), (0x06BE, 0xFBAA, 0xFBAB, 0xFBAC, 0xFBAD),
        (0x06C0, 0xFBA4, 0xFBA5, 0x0000, 0x0000), (0x06C1, 0xFBA6, 0xFBA7, 0xFBA8, 0xFBA9),
        (0x06C5, 0xFBE0, 0xFBE1, 0x0000, 0x0000), (0x06C6, 0xFBD9, 0xFBDA, 0x0000, 0x0000),
        (0x06C7, 0xFBD7, 0xFBD8, 0x0000, 0x0000), (0x06C8, 0xFBDB, 0xFBDC, 0x0000, 0x0000),
        (0x06C9, 0xFBE2, 0xFBE3, 0x0000, 0x0000), (0x06CB, 0xFBDE, 0xFBDF, 0x0000, 0x0000),
        (0x06CC, 0xFBFC, 0xFBFD, 0xFBFE, 0xFBFF), (0x06D0, 0xFBE4, 0xFBE5, 0xFBE6, 0xFBE7),
        (0x06D2, 0xFBAE, 0xFBAF, 0x0000, 0x0000), (0x06D3, 0xFBB0, 0xFBB1, 0x0000, 0x0000),
    }.ToDictionary(f => f.Letter, f => (f.Isolated, f.Final, f.Initial, f.Medial));

    /// <summary>A character's joining type.</summary>
    public static Joining TypeOf(int c)
    {
        if (c == 0x200D) return Joining.Causing;
        if (c is 0x200C or (>= 0x2066 and <= 0x2069)) return Joining.None;
        int lo = 0, hi = Types.Length - 1;
        while (lo <= hi)
        {
            var mid = (lo + hi) / 2;
            if (c < Types[mid].First) hi = mid - 1;
            else if (c > Types[mid].Last) lo = mid + 1;
            else return Types[mid].Type;
        }
        if (c > 0x10FFFF) return Joining.None;
        return CharUnicodeInfo.GetUnicodeCategory(c) is UnicodeCategory.NonSpacingMark or UnicodeCategory.EnclosingMark or UnicodeCategory.Format
            ? Joining.Transparent : Joining.None;
    }

    /// <summary>Whether a character is a letter that joins the letters beside it, one at least on its right.</summary>
    public static bool Joins(int c) => TypeOf(c) is Joining.Right or Joining.Dual;

    /// <summary>Whether a character is of the Arabic script's blocks or of its presentation forms.</summary>
    public static bool IsArabic(int c) => c is (>= 0x0600 and <= 0x06FF) or (>= 0x0750 and <= 0x077F) or (>= 0x0870 and <= 0x08FF)
        or (>= 0xFB50 and <= 0xFDFF) or (>= 0xFE70 and <= 0xFEFF);

    /// <summary>
    /// The form of each character of a run in stored order. A letter joins the one before it where
    /// both join that way, and the one after likewise, and a transparent character, a mark, is
    /// passed over and given none, as a character that joins nothing is.
    /// </summary>
    public static Form[] Forms(ReadOnlySpan<int> run)
    {
        var types = new Joining[run.Length];
        for (int i = 0; i < run.Length; i++) types[i] = TypeOf(run[i]);
        var forms = new Form[run.Length];
        var previous = -1;
        for (int i = 0; i < run.Length; i++)
        {
            if (types[i] == Joining.Transparent) continue;
            var next = i + 1;
            while (next < run.Length && types[next] == Joining.Transparent) next++;
            var before = types[i] is Joining.Right or Joining.Dual or Joining.Causing && previous >= 0 && types[previous] is Joining.Dual or Joining.Causing;
            var after = types[i] is Joining.Dual or Joining.Causing && next < run.Length && types[next] is Joining.Right or Joining.Dual or Joining.Causing;
            if (types[i] is Joining.Right or Joining.Dual)
                forms[i] = before && after ? Form.Medial : before ? Form.Final : after ? Form.Initial : Form.Isolated;
            previous = i;
        }
        return forms;
    }

    /// <summary>The presentation form Unicode encodes for a letter in a form, or null where it has none.</summary>
    public static int? PresentationForm(int letter, Form form)
    {
        if (!Presented.TryGetValue(letter, out var forms)) return null;
        var c = form switch
        {
            Form.Isolated => forms.Isolated,
            Form.Final => forms.Final,
            Form.Initial => forms.Initial,
            Form.Medial => forms.Medial,
            _ => 0,
        };
        return c == 0 ? null : c;
    }

    /// <summary>
    /// The presentation form of lam joined to the alef after it, isolated or, where the lam joins the
    /// letter before it, final, or null for a character that is not an alef lam joins.
    /// </summary>
    public static int? LamAlef(int alef, bool final) => alef switch
    {
        0x0622 => final ? 0xFEF6 : 0xFEF5,
        0x0623 => final ? 0xFEF8 : 0xFEF7,
        0x0625 => final ? 0xFEFA : 0xFEF9,
        0x0627 => final ? 0xFEFC : 0xFEFB,
        _ => null,
    };

    /// <summary>Every presentation form a font may need for the letters given, lam joined to an alef among them.</summary>
    public static IEnumerable<int> PresentationForms(IEnumerable<int> letters)
    {
        var asked = letters.ToHashSet();
        foreach (var letter in asked)
            if (Presented.TryGetValue(letter, out var forms))
                foreach (var c in new[] { forms.Isolated, forms.Final, forms.Initial, forms.Medial })
                    if (c != 0) yield return c;
        if (asked.Contains(0x0644))
            foreach (var alef in new[] { 0x0622, 0x0623, 0x0625, 0x0627 }.Where(asked.Contains))
            {
                yield return LamAlef(alef, false)!.Value;
                yield return LamAlef(alef, true)!.Value;
            }
    }
}
