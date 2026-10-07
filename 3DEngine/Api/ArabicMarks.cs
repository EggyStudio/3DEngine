namespace Engine;

/// <summary>
/// The order Arabic's marks are shaped in after the letter they follow, which decides which mark is
/// put on which. A run of marks is sorted by their combining classes, as Unicode's canonical order
/// sorts them, with HarfBuzz's change to Arabic's classes that puts shadda before the vowels, so a
/// vowel written with it is put on it, and the marks that modify a letter, as hamza above does, are
/// then moved to the run's start, as Unicode's Arabic Mark Transient Reordering Algorithm (UTR #53)
/// moves them.
/// </summary>
/// <remarks>
/// The classes are Unicode 16's for the marks of the Arabic script's blocks, U+0600 to U+06FF and
/// U+0870 to U+08FF, and any other character has none, so a mark of another script ends a run of
/// marks and stays where it is.
/// </remarks>
internal static class ArabicMarks
{
    /// <summary>
    /// A character's combining class, Arabic's vowels moved one up so shadda comes first at 27, and
    /// 0 for one that is no mark of Arabic.
    /// </summary>
    public static int CombiningClass(int c) => c switch
    {
        0x0651 => 27,
        >= 0x064B and <= 0x0650 => 28 + c - 0x064B,
        >= 0x08F0 and <= 0x08F2 => 28 + c - 0x08F0,
        >= 0x0618 and <= 0x061A => 31 + c - 0x0618,
        0x0652 => 34,
        0x0670 => 35,
        0x0655 or 0x0656 or 0x065C or 0x065F or 0x06E3 or 0x06EA or 0x06ED or (>= 0x0899 and <= 0x089B) or (>= 0x08CF and <= 0x08D3)
            or 0x08E3 or 0x08E6 or 0x08E9 or (>= 0x08ED and <= 0x08EF) or 0x08F6 or 0x08F9 or 0x08FA => 220,
        (>= 0x0610 and <= 0x0617) or (>= 0x0653 and <= 0x065F) or (>= 0x06D6 and <= 0x06DC) or (>= 0x06DF and <= 0x06E4) or 0x06E7 or 0x06E8
            or 0x06EB or 0x06EC or 0x0897 or 0x0898 or (>= 0x089C and <= 0x089F) or (>= 0x08CA and <= 0x08E1) or (>= 0x08E3 and <= 0x08FF) => 230,
        _ => 0,
    };

    // The marks that modify the letter they follow, which UTR #53 puts before the marks over and
    // under it.
    private static bool Modifies(int c) => c is 0x0654 or 0x0655 or 0x0658 or 0x06DC or 0x06E3 or 0x06E7 or 0x06E8
        or 0x08CA or 0x08CB or 0x08CD or 0x08CE or 0x08CF or 0x08D3 or 0x08F3;

    /// <summary>
    /// Puts each run of marks in <paramref name="codepoints"/> in the order they are shaped in,
    /// sorting the run by class, keeping the order of marks of one class, then moving the modifying
    /// marks that begin the marks under the letter, and those that begin the marks over it, to the
    /// run's start, in that order.
    /// </summary>
    public static void Reorder(Span<int> codepoints)
    {
        for (int start = 0; start < codepoints.Length;)
        {
            var end = start;
            while (end < codepoints.Length && CombiningClass(codepoints[end]) != 0) end++;
            if (end - start > 1) ReorderRun(codepoints[start..end]);
            start = end + 1;
        }
    }

    private static void ReorderRun(Span<int> marks)
    {
        // An insertion sort, which keeps marks of one class in order, over the few marks a letter has.
        for (int i = 1; i < marks.Length; i++)
        {
            var mark = marks[i];
            var j = i;
            for (; j > 0 && CombiningClass(marks[j - 1]) > CombiningClass(mark); j--) marks[j] = marks[j - 1];
            marks[j] = mark;
        }
        var front = 0;
        foreach (var kind in (ReadOnlySpan<int>)[220, 230])
        {
            var first = front;
            while (first < marks.Length && CombiningClass(marks[first]) < kind) first++;
            var last = first;
            while (last < marks.Length && CombiningClass(marks[last]) == kind && Modifies(marks[last])) last++;
            if (last == first) continue;
            // The modifying marks go to the front, the marks they passed moving after them.
            int[] moved = [.. marks[first..last]];
            marks[front..first].CopyTo(marks[(front + last - first)..]);
            moved.CopyTo(marks[front..]);
            front += moved.Length;
        }
    }
}
