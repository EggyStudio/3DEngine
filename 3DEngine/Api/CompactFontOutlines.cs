using System.Buffers.Binary;
using System.Numerics;

namespace Engine;

/// <summary>
/// A font's outlines in CFF, as an OpenType font of PostScript outlines holds them (Noto Sans CJK,
/// Source Han, the OpenType builds of many fonts), read from their Type 2 charstrings as contours
/// of straight segments, each cubic curve cut in eight.
/// </summary>
/// <remarks>
/// A charstring's subroutines, local and global, its hints, which are skipped, and its flex curves
/// are read, and a CID-keyed font's glyphs take the local subroutines of the font dictionary their
/// range of glyphs names. The arithmetic operators a few old fonts use are not, and a glyph that
/// uses one is drawn without its outline from there on. CFF2, a variable font's, is not read.
/// </remarks>
internal sealed class CompactFontOutlines
{
    private readonly byte[] _data;
    private readonly (int Start, int Count, int OffSize, int Data) _charStrings, _globals;
    private readonly (int Start, int Count, int OffSize, int Data)[] _locals;
    private readonly Func<int, int> _localsOf;

    private CompactFontOutlines(byte[] data, (int, int, int, int) charStrings, (int, int, int, int) globals,
        (int, int, int, int)[] locals, Func<int, int> localsOf)
    {
        (_data, _charStrings, _globals, _locals, _localsOf) = (data, charStrings, globals, locals, localsOf);
    }

    /// <summary>The outlines of the CFF table at <paramref name="cff"/>, or null where it cannot be read.</summary>
    public static CompactFontOutlines? Read(byte[] data, int cff)
    {
        try
        {
            var names = Index(data, cff + data[cff + 2]);
            var tops = Index(data, End(data, names));
            var strings = Index(data, End(data, tops));
            var globals = Index(data, End(data, strings));
            if (tops.Count == 0) return null;
            var top = Dict(data, Item(data, tops, 0));
            if (!top.TryGetValue(17, out var charStringsAt)) return null;
            var charStrings = Index(data, cff + (int)charStringsAt[0]);

            // A CID-keyed font's dictionaries, each with its own private one and local subroutines,
            // and which of them each glyph takes; an ordinary font has its one private dictionary.
            if (top.TryGetValue(1236, out var fdArrayAt) && top.TryGetValue(1237, out var fdSelectAt))
            {
                var fonts = Index(data, cff + (int)fdArrayAt[0]);
                var locals = new (int, int, int, int)[fonts.Count];
                for (int i = 0; i < fonts.Count; i++) locals[i] = LocalSubrs(data, cff, Dict(data, Item(data, fonts, i)));
                var select = cff + (int)fdSelectAt[0];
                return new CompactFontOutlines(data, charStrings, globals, locals, glyph => FontOf(data, select, glyph, charStrings.Count));
            }
            return new CompactFontOutlines(data, charStrings, globals, [LocalSubrs(data, cff, top)], _ => 0);
        }
        catch (Exception error) when (error is ArgumentOutOfRangeException or IndexOutOfRangeException or KeyNotFoundException)
        {
            return null;
        }
    }

    // The local subroutines a dictionary's private dictionary names, or none.
    private static (int, int, int, int) LocalSubrs(byte[] data, int cff, Dictionary<int, double[]> dict)
    {
        if (!dict.TryGetValue(18, out var privateAt) || privateAt.Length < 2) return (0, 0, 0, 0);
        int start = cff + (int)privateAt[1];
        var priv = Dict(data, (start, start + (int)privateAt[0]));
        return priv.TryGetValue(19, out var subrs) ? Index(data, start + (int)subrs[0]) : (0, 0, 0, 0);
    }

    // The font dictionary a glyph of a CID-keyed font takes, from FDSelect in format 0 or 3.
    private static int FontOf(byte[] data, int select, int glyph, int glyphs)
    {
        if (data[select] == 0) return data[select + 1 + glyph];
        int ranges = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(select + 1));
        for (int r = ranges - 1; r >= 0; r--)
            if (glyph >= BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(select + 3 + r * 3))) return data[select + 3 + r * 3 + 2];
        return 0;
    }

    // An INDEX at a place: its count, the size of its offsets, and where its data begins.
    private static (int Start, int Count, int OffSize, int Data) Index(byte[] data, int at)
    {
        int count = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(at));
        if (count == 0) return (at, 0, 0, at + 2);
        int offSize = data[at + 2];
        return (at, count, offSize, at + 3 + (count + 1) * offSize - 1);
    }

    private static int Offset(byte[] data, (int Start, int Count, int OffSize, int Data) index, int i)
    {
        int at = index.Start + 3 + i * index.OffSize, value = 0;
        for (int b = 0; b < index.OffSize; b++) value = value << 8 | data[at + b];
        return value;
    }

    // Where an INDEX ends, the next one's start.
    private static int End(byte[] data, (int Start, int Count, int OffSize, int Data) index) =>
        index.Count == 0 ? index.Start + 2 : index.Data + Offset(data, index, index.Count);

    // An item of an INDEX, from where it starts to where the next does.
    private static (int From, int To) Item(byte[] data, (int Start, int Count, int OffSize, int Data) index, int i) =>
        (index.Data + Offset(data, index, i), index.Data + Offset(data, index, i + 1));

    // A DICT's operators and their operands, a two-byte operator keyed 1200 and its second byte.
    private static Dictionary<int, double[]> Dict(byte[] data, (int From, int To) range)
    {
        var dict = new Dictionary<int, double[]>();
        var operands = new List<double>();
        for (int at = range.From; at < range.To;)
        {
            int b = data[at];
            if (b <= 21)
            {
                var key = b == 12 ? 1200 + data[at + 1] : b;
                at += b == 12 ? 2 : 1;
                dict[key] = [.. operands];
                operands.Clear();
            }
            else if (b == 28) { operands.Add(BinaryPrimitives.ReadInt16BigEndian(data.AsSpan(at + 1))); at += 3; }
            else if (b == 29) { operands.Add(BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(at + 1))); at += 5; }
            else if (b == 30)
            {
                // A real number in nibbles, which no offset is, kept as zero.
                at++;
                while (at < range.To && (data[at] & 0x0F) != 0x0F && (data[at] & 0xF0) != 0xF0) at++;
                at++;
                operands.Add(0);
            }
            else if (b <= 246) { operands.Add(b - 139); at++; }
            else if (b <= 250) { operands.Add((b - 247) * 256 + data[at + 1] + 108); at += 2; }
            else if (b <= 254) { operands.Add(-(b - 251) * 256 - data[at + 1] - 108); at += 2; }
            else at++;
        }
        return dict;
    }

    private static int Bias(int count) => count < 1240 ? 107 : count < 33900 ? 1131 : 32768;

    /// <summary>A glyph's contours as straight segments in the font's units, y up.</summary>
    public List<(Vector2 A, Vector2 B)> Segments(int glyph)
    {
        var lines = new List<(Vector2, Vector2)>();
        if (glyph < 0 || glyph >= _charStrings.Count) return lines;
        var state = new Charstring(lines, _locals.Length == 0 ? default : _locals[Math.Clamp(_localsOf(glyph), 0, _locals.Length - 1)]);
        try
        {
            Run(state, Item(_data, _charStrings, glyph), 0);
        }
        catch (Exception error) when (error is ArgumentOutOfRangeException or IndexOutOfRangeException or InvalidOperationException)
        {
            // A charstring that runs past its data keeps the outline drawn so far.
        }
        state.Close();
        return lines;
    }

    // A charstring's running state: its stack, the pen, the contour it draws and the stems counted.
    private sealed class Charstring(List<(Vector2, Vector2)> lines, (int Start, int Count, int OffSize, int Data) locals)
    {
        public readonly List<(Vector2 A, Vector2 B)> Lines = lines;
        public readonly (int Start, int Count, int OffSize, int Data) Locals = locals;
        public readonly List<float> Stack = [];
        public Vector2 Pen, Start;
        public bool Open, Ended, WidthTaken;
        public int Stems;

        public void MoveTo(Vector2 to)
        {
            Close();
            (Pen, Start, Open) = (to, to, true);
        }

        public void LineTo(Vector2 to)
        {
            Lines.Add((Pen, to));
            Pen = to;
        }

        public void CurveTo(Vector2 c1, Vector2 c2, Vector2 to)
        {
            var from = Pen;
            for (int i = 1; i <= 8; i++)
            {
                float t = i / 8f, u = 1 - t;
                LineTo(from * (u * u * u) + c1 * (3 * u * u * t) + c2 * (3 * u * t * t) + to * (t * t * t));
            }
        }

        public void Close()
        {
            if (Open && Pen != Start) Lines.Add((Pen, Start));
            Open = false;
        }

        // The width an operator's first operand is where the stack holds one more than it takes.
        public void TakeWidth(bool extra)
        {
            if (!WidthTaken && extra && Stack.Count > 0) Stack.RemoveAt(0);
            WidthTaken = true;
        }
    }

    private void Run(Charstring s, (int From, int To) code, int depth)
    {
        if (depth > 10) throw new InvalidOperationException("subroutines nested too deep");
        var st = s.Stack;
        for (int at = code.From; at < code.To && !s.Ended;)
        {
            int b = _data[at++];
            if (b >= 32 || b == 28)
            {
                if (b == 28) { st.Add(BinaryPrimitives.ReadInt16BigEndian(_data.AsSpan(at))); at += 2; }
                else if (b <= 246) st.Add(b - 139);
                else if (b <= 250) st.Add((b - 247) * 256 + _data[at++] + 108);
                else if (b <= 254) st.Add(-(b - 251) * 256 - _data[at++] - 108);
                else { st.Add(BinaryPrimitives.ReadInt32BigEndian(_data.AsSpan(at)) / 65536f); at += 4; }
                continue;
            }
            switch (b)
            {
                case 1 or 3 or 18 or 23:
                    s.TakeWidth(st.Count % 2 == 1);
                    s.Stems += st.Count / 2;
                    st.Clear();
                    break;
                case 19 or 20:
                    s.TakeWidth(st.Count % 2 == 1);
                    s.Stems += st.Count / 2;
                    st.Clear();
                    at += (s.Stems + 7) / 8;
                    break;
                case 21:
                    s.TakeWidth(st.Count > 2);
                    s.MoveTo(s.Pen + new Vector2(st[^2], st[^1]));
                    st.Clear();
                    break;
                case 22:
                    s.TakeWidth(st.Count > 1);
                    s.MoveTo(s.Pen + new Vector2(st[^1], 0));
                    st.Clear();
                    break;
                case 4:
                    s.TakeWidth(st.Count > 1);
                    s.MoveTo(s.Pen + new Vector2(0, st[^1]));
                    st.Clear();
                    break;
                case 5:
                    for (int i = 0; i + 1 < st.Count; i += 2) s.LineTo(s.Pen + new Vector2(st[i], st[i + 1]));
                    st.Clear();
                    break;
                case 6 or 7:
                {
                    var horizontal = b == 6;
                    foreach (var d in st)
                    {
                        s.LineTo(s.Pen + (horizontal ? new Vector2(d, 0) : new Vector2(0, d)));
                        horizontal = !horizontal;
                    }
                    st.Clear();
                    break;
                }
                case 8:
                    for (int i = 0; i + 5 < st.Count; i += 6) Curve(s, st[i], st[i + 1], st[i + 2], st[i + 3], st[i + 4], st[i + 5]);
                    st.Clear();
                    break;
                case 24:
                {
                    int i = 0;
                    for (; i + 5 < st.Count - 2; i += 6) Curve(s, st[i], st[i + 1], st[i + 2], st[i + 3], st[i + 4], st[i + 5]);
                    if (i + 1 < st.Count) s.LineTo(s.Pen + new Vector2(st[i], st[i + 1]));
                    st.Clear();
                    break;
                }
                case 25:
                {
                    int i = 0;
                    for (; i + 1 < st.Count - 6; i += 2) s.LineTo(s.Pen + new Vector2(st[i], st[i + 1]));
                    if (i + 5 < st.Count) Curve(s, st[i], st[i + 1], st[i + 2], st[i + 3], st[i + 4], st[i + 5]);
                    st.Clear();
                    break;
                }
                case 26:
                {
                    int i = 0;
                    float dx1 = st.Count % 2 == 1 ? st[i++] : 0;
                    for (; i + 3 < st.Count; i += 4, dx1 = 0) Curve(s, dx1, st[i], st[i + 1], st[i + 2], 0, st[i + 3]);
                    st.Clear();
                    break;
                }
                case 27:
                {
                    int i = 0;
                    float dy1 = st.Count % 2 == 1 ? st[i++] : 0;
                    for (; i + 3 < st.Count; i += 4, dy1 = 0) Curve(s, st[i], dy1, st[i + 1], st[i + 2], st[i + 3], 0);
                    st.Clear();
                    break;
                }
                case 30 or 31:
                {
                    // Curves that start vertical and horizontal by turns, the last one's end given
                    // its other coordinate where one number is left over.
                    var vertical = b == 30;
                    for (int i = 0; i + 3 < st.Count; i += 4)
                    {
                        float last = st.Count - i == 5 ? st[i + 4] : 0;
                        if (vertical) Curve(s, 0, st[i], st[i + 1], st[i + 2], st[i + 3], last);
                        else Curve(s, st[i], 0, st[i + 1], st[i + 2], last, st[i + 3]);
                        vertical = !vertical;
                    }
                    st.Clear();
                    break;
                }
                case 10 or 29:
                {
                    var subrs = b == 10 ? s.Locals : _globals;
                    var index = (int)st[^1] + Bias(subrs.Count);
                    st.RemoveAt(st.Count - 1);
                    if (subrs.Count == 0 || index < 0 || index >= subrs.Count) throw new InvalidOperationException("a subroutine past its index");
                    Run(s, Item(_data, subrs, index), depth + 1);
                    break;
                }
                case 11:
                    return;
                case 14:
                    s.TakeWidth(st.Count == 1 || st.Count == 5);
                    s.Close();
                    s.Ended = true;
                    return;
                case 12:
                    Flex(s, _data[at++]);
                    st.Clear();
                    break;
                default:
                    st.Clear();
                    break;
            }
        }
    }

    // A cubic curve by three relative steps from the pen.
    private static void Curve(Charstring s, float dxa, float dya, float dxb, float dyb, float dxc, float dyc)
    {
        var c1 = s.Pen + new Vector2(dxa, dya);
        var c2 = c1 + new Vector2(dxb, dyb);
        s.CurveTo(c1, c2, c2 + new Vector2(dxc, dyc));
    }

    // The flex operators, two curves each, which a renderer may draw as a line at small sizes and
    // which are drawn as curves here.
    private static void Flex(Charstring s, int op)
    {
        var a = s.Stack;
        switch (op)
        {
            case 35 when a.Count >= 12:
                Curve(s, a[0], a[1], a[2], a[3], a[4], a[5]);
                Curve(s, a[6], a[7], a[8], a[9], a[10], a[11]);
                break;
            case 34 when a.Count >= 7:
                Curve(s, a[0], 0, a[1], a[2], a[3], 0);
                Curve(s, a[4], 0, a[5], -a[2], a[6], 0);
                break;
            case 36 when a.Count >= 9:
                Curve(s, a[0], a[1], a[2], a[3], a[4], 0);
                Curve(s, a[5], 0, a[6], a[7], a[8], -(a[1] + a[3] + a[7]));
                break;
            case 37 when a.Count >= 11:
            {
                // The last point's other coordinate brings it back level with the start, along
                // whichever axis the flex moved less.
                float dx = a[0] + a[2] + a[4] + a[6] + a[8], dy = a[1] + a[3] + a[5] + a[7] + a[9];
                Curve(s, a[0], a[1], a[2], a[3], a[4], a[5]);
                var (lastX, lastY) = MathF.Abs(dx) > MathF.Abs(dy) ? (a[10], -dy) : (-dx, a[10]);
                Curve(s, a[6], a[7], a[8], a[9], lastX, lastY);
                break;
            }
        }
    }
}
