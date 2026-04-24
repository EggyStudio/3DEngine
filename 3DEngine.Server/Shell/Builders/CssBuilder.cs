namespace Editor.Shell;


// ── Enums ───────────────────────────────────────────────────────────────

/// <summary>Flex/grid alignment values mapped to Tailwind <c>items-*</c> and <c>self-*</c> utilities.</summary>
public enum Align {
    /// <summary>Align to the start of the cross axis (<c>items-start</c>).</summary>
    Start,
    /// <summary>Center on the cross axis (<c>items-center</c>).</summary>
    Center,
    /// <summary>Align to the end of the cross axis (<c>items-end</c>).</summary>
    End,
    /// <summary>Stretch to fill the container (<c>items-stretch</c>).</summary>
    Stretch,
    /// <summary>Align baselines of flex items (<c>items-baseline</c>).</summary>
    Baseline
}

/// <summary>Flex/grid justification values mapped to Tailwind <c>justify-*</c> utilities.</summary>
public enum Justify {
    /// <summary>Pack items toward the start (<c>justify-start</c>).</summary>
    Start,
    /// <summary>Center items on the main axis (<c>justify-center</c>).</summary>
    Center,
    /// <summary>Pack items toward the end (<c>justify-end</c>).</summary>
    End,
    /// <summary>Distribute with equal space between items (<c>justify-between</c>).</summary>
    Between,
    /// <summary>Distribute with equal space around items (<c>justify-around</c>).</summary>
    Around,
    /// <summary>Distribute with equal space between and around items (<c>justify-evenly</c>).</summary>
    Evenly
}

// ── Static entry point ──────────────────────────────────────────────────

/// <summary>
/// Static entry point for the fluent CSS builder. Each method creates a new
/// <see cref="CssBuilder"/> and adds the first class. Chain more classes with dot notation.
/// </summary>
/// <example><code>
/// css: Css.Flex().Column().Items(Align.Center).Gap(2)
/// css: Css.TextCenter().SpaceY(4)
/// css: Css.Container().PaddingY(10)
///
/// // Raw string escape hatch:
/// css: "custom-class another-class"
/// </code></example>
public static class Css
{
    /// <summary>No CSS classes - use as the first argument when no styling is needed.</summary>
    public static readonly string? Default = null;

    // ── Display ─────────────────────────────────────────────────────
    /// <summary>Starts a builder with <c>flex</c>.</summary>
    public static CssBuilder Flex() => new CssBuilder().Flex();
    /// <summary>Starts a builder with <c>inline-flex</c>.</summary>
    public static CssBuilder InlineFlex() => new CssBuilder().InlineFlex();
    /// <summary>Starts a builder with <c>grid</c>.</summary>
    public static CssBuilder Grid() => new CssBuilder().Grid();
    /// <summary>Starts a builder with <c>block</c>.</summary>
    public static CssBuilder Block() => new CssBuilder().Block();
    /// <summary>Starts a builder with <c>inline-block</c>.</summary>
    public static CssBuilder InlineBlock() => new CssBuilder().InlineBlock();
    /// <summary>Starts a builder with <c>hidden</c>.</summary>
    public static CssBuilder Hidden() => new CssBuilder().Hidden();
    /// <summary>Starts a builder with <c>container</c>.</summary>
    public static CssBuilder Container() => new CssBuilder().Container();

    // ── Spacing starters ────────────────────────────────────────────
    /// <summary>Starts a builder with <c>space-x-{size}</c>.</summary>
    public static CssBuilder SpaceX(int size) => new CssBuilder().SpaceX(size);
    /// <summary>Starts a builder with <c>space-y-{size}</c>.</summary>
    public static CssBuilder SpaceY(int size) => new CssBuilder().SpaceY(size);
    /// <summary>Starts a builder with <c>gap-{size}</c>.</summary>
    public static CssBuilder Gap(int size) => new CssBuilder().Gap(size);

    // ── Margin starters ─────────────────────────────────────────────
    /// <summary>Starts a builder with <c>mx-auto</c>.</summary>
    public static CssBuilder MarginXAuto() => new CssBuilder().MarginXAuto();

    // ── Text starters ───────────────────────────────────────────────
    /// <summary>Starts a builder with <c>text-center</c>.</summary>
    public static CssBuilder TextCenter() => new CssBuilder().TextCenter();
    /// <summary>Starts a builder with <c>text-left</c>.</summary>
    public static CssBuilder TextLeft() => new CssBuilder().TextLeft();
    /// <summary>Starts a builder with <c>text-right</c>.</summary>
    public static CssBuilder TextRight() => new CssBuilder().TextRight();

    // ── Text size starters ──────────────────────────────────────────
    /// <summary>Starts a builder with <c>text-xs</c>.</summary>
    public static CssBuilder TextXs() => new CssBuilder().TextXs();
    /// <summary>Starts a builder with <c>text-sm</c>.</summary>
    public static CssBuilder TextSm() => new CssBuilder().TextSm();
    /// <summary>Starts a builder with <c>text-base</c>.</summary>
    public static CssBuilder TextBase() => new CssBuilder().TextBase();
    /// <summary>Starts a builder with <c>text-lg</c>.</summary>
    public static CssBuilder TextLg() => new CssBuilder().TextLg();
    /// <summary>Starts a builder with <c>text-xl</c>.</summary>
    public static CssBuilder TextXl() => new CssBuilder().TextXl();

    // ── Position starters ───────────────────────────────────────────
    /// <summary>Starts a builder with <c>relative</c>.</summary>
    public static CssBuilder Relative() => new CssBuilder().Relative();
    /// <summary>Starts a builder with <c>absolute</c>.</summary>
    public static CssBuilder Absolute() => new CssBuilder().Absolute();
    /// <summary>Starts a builder with <c>fixed</c>.</summary>
    public static CssBuilder Fixed() => new CssBuilder().Fixed();
    /// <summary>Starts a builder with <c>sticky</c>.</summary>
    public static CssBuilder Sticky() => new CssBuilder().Sticky();

    // ── Sizing starters ─────────────────────────────────────────────
    /// <summary>Starts a builder with <c>w-full</c>.</summary>
    public static CssBuilder WidthFull() => new CssBuilder().WidthFull();
    /// <summary>Starts a builder with <c>h-full</c>.</summary>
    public static CssBuilder HeightFull() => new CssBuilder().HeightFull();

    // ── Padding starters ────────────────────────────────────────────
    /// <summary>Starts a builder with <c>p-{size}</c>.</summary>
    public static CssBuilder Padding(int size) => new CssBuilder().Padding(size);
    /// <summary>Starts a builder with <c>px-{size}</c>.</summary>
    public static CssBuilder PaddingX(int size) => new CssBuilder().PaddingX(size);
    /// <summary>Starts a builder with <c>py-{size}</c>.</summary>
    public static CssBuilder PaddingY(int size) => new CssBuilder().PaddingY(size);

    // ── Margin starters ─────────────────────────────────────────────
    /// <summary>Starts a builder with <c>my-{size}</c>.</summary>
    public static CssBuilder MarginY(int size) => new CssBuilder().MarginY(size);
    /// <summary>Starts a builder with <c>mx-{size}</c>.</summary>
    public static CssBuilder MarginX(int size) => new CssBuilder().MarginX(size);
    /// <summary>Starts a builder with <c>mt-{size}</c>.</summary>
    public static CssBuilder MarginTop(int size) => new CssBuilder().MarginTop(size);

    // ── Text color / font weight starters ───────────────────────────
    /// <summary>Starts a builder with <c>text-{color}</c>.</summary>
    public static CssBuilder TextColor(string color) => new CssBuilder().TextColor(color);
    /// <summary>Starts a builder with <c>font-bold</c>.</summary>
    public static CssBuilder FontBold() => new CssBuilder().FontBold();
    /// <summary>Starts a builder with <c>font-semibold</c>.</summary>
    public static CssBuilder FontSemibold() => new CssBuilder().FontSemibold();
    /// <summary>Starts a builder with <c>font-medium</c>.</summary>
    public static CssBuilder FontMedium() => new CssBuilder().FontMedium();
}

// ── Fluent builder ──────────────────────────────────────────────────────

/// <summary>
/// Fluent CSS class builder. Chain methods to compose Tailwind utility classes.
/// Implicitly converts to <c>string</c> so it works with any <c>string? css</c> parameter.
/// </summary>
public sealed class CssBuilder
{
    private readonly List<string> _classes = [];

    private CssBuilder Add(string cls) { _classes.Add(cls); return this; }

    // ── Display ─────────────────────────────────────────────────────

    /// <summary>Adds <c>flex</c>.</summary>
    public CssBuilder Flex() => Add("flex");
    /// <summary>Adds <c>inline-flex</c>.</summary>
    public CssBuilder InlineFlex() => Add("inline-flex");
    /// <summary>Adds <c>grid</c>.</summary>
    public CssBuilder Grid() => Add("grid");
    /// <summary>Adds <c>block</c>.</summary>
    public CssBuilder Block() => Add("block");
    /// <summary>Adds <c>inline-block</c>.</summary>
    public CssBuilder InlineBlock() => Add("inline-block");
    /// <summary>Adds <c>hidden</c>.</summary>
    public CssBuilder Hidden() => Add("hidden");
    /// <summary>Adds <c>container</c>.</summary>
    public CssBuilder Container() => Add("container");

    // ── Flex direction & wrap ───────────────────────────────────────

    /// <summary>Adds <c>flex-col</c> (vertical flex direction).</summary>
    public CssBuilder Column() => Add("flex-col");
    /// <summary>Adds <c>flex-row</c> (horizontal flex direction).</summary>
    public CssBuilder Row() => Add("flex-row");
    /// <summary>Adds <c>flex-col-reverse</c>.</summary>
    public CssBuilder ColumnReverse() => Add("flex-col-reverse");
    /// <summary>Adds <c>flex-row-reverse</c>.</summary>
    public CssBuilder RowReverse() => Add("flex-row-reverse");
    /// <summary>Adds <c>flex-wrap</c>.</summary>
    public CssBuilder Wrap() => Add("flex-wrap");
    /// <summary>Adds <c>flex-nowrap</c>.</summary>
    public CssBuilder NoWrap() => Add("flex-nowrap");
    /// <summary>Adds <c>flex-1</c> (flex: 1 1 0%).</summary>
    public CssBuilder Flex1() => Add("flex-1");
    /// <summary>Adds <c>grow</c> (flex-grow: 1).</summary>
    public CssBuilder Grow() => Add("grow");
    /// <summary>Adds <c>shrink</c> (flex-shrink: 1).</summary>
    public CssBuilder Shrink() => Add("shrink");
    /// <summary>Adds <c>shrink-0</c> (flex-shrink: 0).</summary>
    public CssBuilder Shrink0() => Add("shrink-0");

    // ── Alignment ───────────────────────────────────────────────────

    /// <summary>Adds <c>items-{align}</c> for cross-axis alignment.</summary>
    public CssBuilder Items(Align align) => Add($"items-{ToValue(align)}");
    /// <summary>Adds <c>justify-{justify}</c> for main-axis alignment.</summary>
    public CssBuilder Justify(Shell.Justify justify) => Add($"justify-{ToValue(justify)}");
    /// <summary>Adds <c>self-{align}</c> for individual item cross-axis alignment.</summary>
    public CssBuilder Self(Align align) => Add($"self-{ToValue(align)}");

    // ── Gap ─────────────────────────────────────────────────────────

    /// <summary>Adds <c>gap-{size}</c>.</summary>
    public CssBuilder Gap(int size) => Add($"gap-{size}");
    /// <summary>Adds <c>gap-x-{size}</c>.</summary>
    public CssBuilder GapX(int size) => Add($"gap-x-{size}");
    /// <summary>Adds <c>gap-y-{size}</c>.</summary>
    public CssBuilder GapY(int size) => Add($"gap-y-{size}");

    // ── Space between ───────────────────────────────────────────────

    /// <summary>Adds <c>space-x-{size}</c>.</summary>
    public CssBuilder SpaceX(int size) => Add($"space-x-{size}");
    /// <summary>Adds <c>space-y-{size}</c>.</summary>
    public CssBuilder SpaceY(int size) => Add($"space-y-{size}");

    // ── Padding ─────────────────────────────────────────────────────

    /// <summary>Adds <c>p-{size}</c> (all sides).</summary>
    public CssBuilder Padding(int size) => Add($"p-{size}");
    /// <summary>Adds <c>px-{size}</c> (horizontal).</summary>
    public CssBuilder PaddingX(int size) => Add($"px-{size}");
    /// <summary>Adds <c>py-{size}</c> (vertical).</summary>
    public CssBuilder PaddingY(int size) => Add($"py-{size}");
    /// <summary>Adds <c>pt-{size}</c>.</summary>
    public CssBuilder PaddingTop(int size) => Add($"pt-{size}");
    /// <summary>Adds <c>pb-{size}</c>.</summary>
    public CssBuilder PaddingBottom(int size) => Add($"pb-{size}");
    /// <summary>Adds <c>pl-{size}</c>.</summary>
    public CssBuilder PaddingLeft(int size) => Add($"pl-{size}");
    /// <summary>Adds <c>pr-{size}</c>.</summary>
    public CssBuilder PaddingRight(int size) => Add($"pr-{size}");

    // ── Margin ──────────────────────────────────────────────────────

    /// <summary>Adds <c>m-{size}</c> (all sides).</summary>
    public CssBuilder Margin(int size) => Add($"m-{size}");
    /// <summary>Adds <c>mx-{size}</c> (horizontal).</summary>
    public CssBuilder MarginX(int size) => Add($"mx-{size}");
    /// <summary>Adds <c>my-{size}</c> (vertical).</summary>
    public CssBuilder MarginY(int size) => Add($"my-{size}");
    /// <summary>Adds <c>mt-{size}</c>.</summary>
    public CssBuilder MarginTop(int size) => Add($"mt-{size}");
    /// <summary>Adds <c>mb-{size}</c>.</summary>
    public CssBuilder MarginBottom(int size) => Add($"mb-{size}");
    /// <summary>Adds <c>ml-{size}</c>.</summary>
    public CssBuilder MarginLeft(int size) => Add($"ml-{size}");
    /// <summary>Adds <c>mr-{size}</c>.</summary>
    public CssBuilder MarginRight(int size) => Add($"mr-{size}");
    /// <summary>Adds <c>mx-auto</c> (center horizontally).</summary>
    public CssBuilder MarginXAuto() => Add("mx-auto");
    /// <summary>Adds <c>my-auto</c> (center vertically).</summary>
    public CssBuilder MarginYAuto() => Add("my-auto");
    /// <summary>Adds <c>m-auto</c> (center all directions).</summary>
    public CssBuilder MarginAuto() => Add("m-auto");

    // ── Sizing ──────────────────────────────────────────────────────

    /// <summary>Adds <c>w-{value}</c>.</summary>
    public CssBuilder Width(string value) => Add($"w-{value}");
    /// <summary>Adds <c>h-{value}</c>.</summary>
    public CssBuilder Height(string value) => Add($"h-{value}");
    /// <summary>Adds <c>w-full</c>.</summary>
    public CssBuilder WidthFull() => Add("w-full");
    /// <summary>Adds <c>h-full</c>.</summary>
    public CssBuilder HeightFull() => Add("h-full");
    /// <summary>Adds <c>min-w-{value}</c>.</summary>
    public CssBuilder MinWidth(string value) => Add($"min-w-{value}");
    /// <summary>Adds <c>max-w-{value}</c>.</summary>
    public CssBuilder MaxWidth(string value) => Add($"max-w-{value}");
    /// <summary>Adds <c>min-h-{value}</c>.</summary>
    public CssBuilder MinHeight(string value) => Add($"min-h-{value}");
    /// <summary>Adds <c>max-h-{value}</c>.</summary>
    public CssBuilder MaxHeight(string value) => Add($"max-h-{value}");

    // ── Text alignment ──────────────────────────────────────────────

    /// <summary>Adds <c>text-center</c>.</summary>
    public CssBuilder TextCenter() => Add("text-center");
    /// <summary>Adds <c>text-left</c>.</summary>
    public CssBuilder TextLeft() => Add("text-left");
    /// <summary>Adds <c>text-right</c>.</summary>
    public CssBuilder TextRight() => Add("text-right");
    /// <summary>Adds <c>text-justify</c>.</summary>
    public CssBuilder TextJustify() => Add("text-justify");

    // ── Text size ───────────────────────────────────────────────────

    /// <summary>Adds <c>text-xs</c> (0.75rem).</summary>
    public CssBuilder TextXs() => Add("text-xs");
    /// <summary>Adds <c>text-sm</c> (0.875rem).</summary>
    public CssBuilder TextSm() => Add("text-sm");
    /// <summary>Adds <c>text-base</c> (1rem).</summary>
    public CssBuilder TextBase() => Add("text-base");
    /// <summary>Adds <c>text-lg</c> (1.125rem).</summary>
    public CssBuilder TextLg() => Add("text-lg");
    /// <summary>Adds <c>text-xl</c> (1.25rem).</summary>
    public CssBuilder TextXl() => Add("text-xl");
    /// <summary>Adds <c>text-2xl</c> (1.5rem).</summary>
    public CssBuilder Text2xl() => Add("text-2xl");
    /// <summary>Adds <c>text-3xl</c> (1.875rem).</summary>
    public CssBuilder Text3xl() => Add("text-3xl");
    /// <summary>Adds <c>text-4xl</c> (2.25rem).</summary>
    public CssBuilder Text4xl() => Add("text-4xl");

    // ── Text color ──────────────────────────────────────────────────

    /// <summary>Adds <c>text-{color}</c>, e.g. <c>TextColor("muted-foreground")</c>.</summary>
    public CssBuilder TextColor(string color) => Add($"text-{color}");

    // ── Font weight ─────────────────────────────────────────────────

    /// <summary>Adds <c>font-thin</c>.</summary>
    public CssBuilder FontThin() => Add("font-thin");
    /// <summary>Adds <c>font-light</c>.</summary>
    public CssBuilder FontLight() => Add("font-light");
    /// <summary>Adds <c>font-normal</c>.</summary>
    public CssBuilder FontNormal() => Add("font-normal");
    /// <summary>Adds <c>font-medium</c>.</summary>
    public CssBuilder FontMedium() => Add("font-medium");
    /// <summary>Adds <c>font-semibold</c>.</summary>
    public CssBuilder FontSemibold() => Add("font-semibold");
    /// <summary>Adds <c>font-bold</c>.</summary>
    public CssBuilder FontBold() => Add("font-bold");
    /// <summary>Adds <c>font-extrabold</c>.</summary>
    public CssBuilder FontExtrabold() => Add("font-extrabold");

    // ── Border & rounding ───────────────────────────────────────────

    /// <summary>Adds <c>border</c> (1px solid).</summary>
    public CssBuilder Border() => Add("border");
    /// <summary>Adds <c>border-t</c>.</summary>
    public CssBuilder BorderTop() => Add("border-t");
    /// <summary>Adds <c>border-b</c>.</summary>
    public CssBuilder BorderBottom() => Add("border-b");
    /// <summary>Adds <c>border-l</c>.</summary>
    public CssBuilder BorderLeft() => Add("border-l");
    /// <summary>Adds <c>border-r</c>.</summary>
    public CssBuilder BorderRight() => Add("border-r");
    /// <summary>Adds <c>rounded</c>.</summary>
    public CssBuilder Rounded() => Add("rounded");
    /// <summary>Adds <c>rounded-sm</c>.</summary>
    public CssBuilder RoundedSm() => Add("rounded-sm");
    /// <summary>Adds <c>rounded-md</c>.</summary>
    public CssBuilder RoundedMd() => Add("rounded-md");
    /// <summary>Adds <c>rounded-lg</c>.</summary>
    public CssBuilder RoundedLg() => Add("rounded-lg");
    /// <summary>Adds <c>rounded-xl</c>.</summary>
    public CssBuilder RoundedXl() => Add("rounded-xl");
    /// <summary>Adds <c>rounded-full</c>.</summary>
    public CssBuilder RoundedFull() => Add("rounded-full");

    // ── Position ────────────────────────────────────────────────────

    /// <summary>Adds <c>relative</c>.</summary>
    public CssBuilder Relative() => Add("relative");
    /// <summary>Adds <c>absolute</c>.</summary>
    public CssBuilder Absolute() => Add("absolute");
    /// <summary>Adds <c>fixed</c>.</summary>
    public CssBuilder Fixed() => Add("fixed");
    /// <summary>Adds <c>sticky</c>.</summary>
    public CssBuilder Sticky() => Add("sticky");

    // ── Overflow ────────────────────────────────────────────────────

    /// <summary>Adds <c>overflow-hidden</c>.</summary>
    public CssBuilder OverflowHidden() => Add("overflow-hidden");
    /// <summary>Adds <c>overflow-auto</c>.</summary>
    public CssBuilder OverflowAuto() => Add("overflow-auto");
    /// <summary>Adds <c>overflow-scroll</c>.</summary>
    public CssBuilder OverflowScroll() => Add("overflow-scroll");

    // ── Truncate / whitespace ───────────────────────────────────────

    /// <summary>Adds <c>truncate</c>.</summary>
    public CssBuilder Truncate() => Add("truncate");
    /// <summary>Adds <c>whitespace-nowrap</c>.</summary>
    public CssBuilder WhitespaceNowrap() => Add("whitespace-nowrap");

    // ── Cursor ──────────────────────────────────────────────────────

    /// <summary>Adds <c>cursor-pointer</c>.</summary>
    public CssBuilder CursorPointer() => Add("cursor-pointer");
    /// <summary>Adds <c>cursor-default</c>.</summary>
    public CssBuilder CursorDefault() => Add("cursor-default");

    // ── Opacity ─────────────────────────────────────────────────────

    /// <summary>Adds <c>opacity-{percent}</c>.</summary>
    public CssBuilder Opacity(int percent) => Add($"opacity-{percent}");

    // ── Raw escape hatch ────────────────────────────────────────────

    /// <summary>Append arbitrary CSS classes for edge cases not covered by the builder.</summary>
    public CssBuilder Raw(string classes) => Add(classes);

    // ── Conversion ──────────────────────────────────────────────────

    /// <summary>Joins all accumulated classes into a space-separated string.</summary>
    public override string ToString() => string.Join(" ", _classes);
    /// <summary>Implicit conversion to <see cref="string"/> for use with <c>string? css</c> parameters.</summary>
    public static implicit operator string(CssBuilder builder) => builder.ToString();

    // ── Helpers ─────────────────────────────────────────────────────

    private static string ToValue(Align a) => a switch
    {
        Align.Start => "start",
        Align.Center => "center",
        Align.End => "end",
        Align.Stretch => "stretch",
        Align.Baseline => "baseline",
        _ => "start"
    };

    private static string ToValue(Shell.Justify j) => j switch
    {
        Shell.Justify.Start => "start",
        Shell.Justify.Center => "center",
        Shell.Justify.End => "end",
        Shell.Justify.Between => "between",
        Shell.Justify.Around => "around",
        Shell.Justify.Evenly => "evenly",
        _ => "start"
    };
}

