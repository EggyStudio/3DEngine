namespace Engine;

/// <summary>
/// A key by its place on the keyboard rather than by what it types. The values are USB HID usage
/// codes, the same as SDL's scancodes, so none is converted.
/// </summary>
/// <remarks>
/// A member is named for what a US layout types there, so <see cref="W"/> is the key above
/// <see cref="S"/> on every layout, which types Z on a French one. That keeps movement keys in
/// place for every player, and text a player types is read with <c>GetCharPressed</c> instead.
/// </remarks>
public enum Key
{
    /// <summary>No key, which a key the engine does not know reports and <see cref="Engine3D.SetExitKey"/> takes for none.</summary>
    Null = 0,
    /// <summary>The key where a US layout has A.</summary>
    A = 4,
    /// <summary>The key where a US layout has B.</summary>
    B = 5,
    /// <summary>The key where a US layout has C.</summary>
    C = 6,
    /// <summary>The key where a US layout has D.</summary>
    D = 7,
    /// <summary>The key where a US layout has E.</summary>
    E = 8,
    /// <summary>The key where a US layout has F.</summary>
    F = 9,
    /// <summary>The key where a US layout has G.</summary>
    G = 10,
    /// <summary>The key where a US layout has H.</summary>
    H = 11,
    /// <summary>The key where a US layout has I.</summary>
    I = 12,
    /// <summary>The key where a US layout has J.</summary>
    J = 13,
    /// <summary>The key where a US layout has K.</summary>
    K = 14,
    /// <summary>The key where a US layout has L.</summary>
    L = 15,
    /// <summary>The key where a US layout has M.</summary>
    M = 16,
    /// <summary>The key where a US layout has N.</summary>
    N = 17,
    /// <summary>The key where a US layout has O.</summary>
    O = 18,
    /// <summary>The key where a US layout has P.</summary>
    P = 19,
    /// <summary>The key where a US layout has Q.</summary>
    Q = 20,
    /// <summary>The key where a US layout has R.</summary>
    R = 21,
    /// <summary>The key where a US layout has S.</summary>
    S = 22,
    /// <summary>The key where a US layout has T.</summary>
    T = 23,
    /// <summary>The key where a US layout has U.</summary>
    U = 24,
    /// <summary>The key where a US layout has V.</summary>
    V = 25,
    /// <summary>The key where a US layout has W.</summary>
    W = 26,
    /// <summary>The key where a US layout has X.</summary>
    X = 27,
    /// <summary>The key where a US layout has Y.</summary>
    Y = 28,
    /// <summary>The key where a US layout has Z.</summary>
    Z = 29,
    /// <summary>The key where a US layout has 1, on the row above the letters.</summary>
    One = 30,
    /// <summary>The key where a US layout has 2, on the row above the letters.</summary>
    Two = 31,
    /// <summary>The key where a US layout has 3, on the row above the letters.</summary>
    Three = 32,
    /// <summary>The key where a US layout has 4, on the row above the letters.</summary>
    Four = 33,
    /// <summary>The key where a US layout has 5, on the row above the letters.</summary>
    Five = 34,
    /// <summary>The key where a US layout has 6, on the row above the letters.</summary>
    Six = 35,
    /// <summary>The key where a US layout has 7, on the row above the letters.</summary>
    Seven = 36,
    /// <summary>The key where a US layout has 8, on the row above the letters.</summary>
    Eight = 37,
    /// <summary>The key where a US layout has 9, on the row above the letters.</summary>
    Nine = 38,
    /// <summary>The key where a US layout has 0, on the row above the letters.</summary>
    Zero = 39,
    /// <summary>Return, the Enter key beside the letters.</summary>
    Return = 40,
    /// <summary>The Return key by raylib's name for it.</summary>
    Enter = Return,
    /// <summary>Escape.</summary>
    Escape = 41,
    /// <summary>Backspace.</summary>
    Backspace = 42,
    /// <summary>Tab.</summary>
    Tab = 43,
    /// <summary>The space bar.</summary>
    Space = 44,
    /// <summary>The key where a US layout has the minus sign.</summary>
    Minus = 45,
    /// <summary>The key where a US layout has the equals sign.</summary>
    Equal = 46,
    /// <summary>The key where a US layout has the left square bracket.</summary>
    Leftbracket = 47,
    /// <summary>The key where a US layout has the right square bracket.</summary>
    Rightbracket = 48,

    /// <summary>
    /// Located at the lower left of the return key on ISO keyboards and at the right end
    /// of the QWERTY row on ANSI keyboards. Produces backslash / vertical line in US layout.
    /// </summary>
    Backslash = 49,

    /// <summary>
    /// ISO keyboards use this code instead of 49 for the same key. Most OSes treat both
    /// alike, so <see cref="Backslash"/> is the one to use unless a keyboard sends both.
    /// </summary>
    NonUshash = 50,
    /// <summary>The key where a US layout has the semicolon.</summary>
    Semicolon = 51,
    /// <summary>The key where a US layout has the apostrophe.</summary>
    Apostrophe = 52,

    /// <summary>
    /// Top left corner key. Produces grave accent / tilde in US layout,
    /// section sign on ISO keyboards, etc.
    /// </summary>
    Grave = 53,
    /// <summary>The key where a US layout has the comma.</summary>
    Comma = 54,
    /// <summary>The key where a US layout has the full stop.</summary>
    Period = 55,
    /// <summary>The key where a US layout has the slash.</summary>
    Slash = 56,
    /// <summary>Caps Lock.</summary>
    Capslock = 57,
    /// <summary>F1.</summary>
    F1 = 58,
    /// <summary>F2.</summary>
    F2 = 59,
    /// <summary>F3.</summary>
    F3 = 60,
    /// <summary>F4.</summary>
    F4 = 61,
    /// <summary>F5.</summary>
    F5 = 62,
    /// <summary>F6.</summary>
    F6 = 63,
    /// <summary>F7.</summary>
    F7 = 64,
    /// <summary>F8.</summary>
    F8 = 65,
    /// <summary>F9.</summary>
    F9 = 66,
    /// <summary>F10.</summary>
    F10 = 67,
    /// <summary>F11.</summary>
    F11 = 68,
    /// <summary>F12.</summary>
    F12 = 69,
    /// <summary>Print Screen.</summary>
    Printscreen = 70,
    /// <summary>Scroll Lock.</summary>
    Scrolllock = 71,
    /// <summary>Pause, Break with Control held.</summary>
    Pause = 72,

    /// <summary>Insert on PC, Help on some Mac keyboards.</summary>
    Insert = 73,
    /// <summary>Home.</summary>
    Home = 74,
    /// <summary>Page Up.</summary>
    Pageup = 75,
    /// <summary>Delete, which removes what is after the cursor.</summary>
    Delete = 76,
    /// <summary>End.</summary>
    End = 77,
    /// <summary>Page Down.</summary>
    Pagedown = 78,
    /// <summary>The right arrow.</summary>
    Right = 79,
    /// <summary>The left arrow.</summary>
    Left = 80,
    /// <summary>The down arrow.</summary>
    Down = 81,
    /// <summary>The up arrow.</summary>
    Up = 82,

    /// <summary>Num Lock on PC, Clear on Mac keyboards.</summary>
    NumLock = 83,
    /// <summary>The keypad's divide key.</summary>
    KpDivide = 84,
    /// <summary>The keypad's multiply key.</summary>
    KpMultiply = 85,
    /// <summary>The keypad's minus key.</summary>
    KpSubtract = 86,
    /// <summary>The keypad's plus key.</summary>
    KpAdd = 87,
    /// <summary>The keypad's Enter.</summary>
    KpEnter = 88,
    /// <summary>The keypad's 1.</summary>
    Kp1 = 89,
    /// <summary>The keypad's 2.</summary>
    Kp2 = 90,
    /// <summary>The keypad's 3.</summary>
    Kp3 = 91,
    /// <summary>The keypad's 4.</summary>
    Kp4 = 92,
    /// <summary>The keypad's 5.</summary>
    Kp5 = 93,
    /// <summary>The keypad's 6.</summary>
    Kp6 = 94,
    /// <summary>The keypad's 7.</summary>
    Kp7 = 95,
    /// <summary>The keypad's 8.</summary>
    Kp8 = 96,
    /// <summary>The keypad's 9.</summary>
    Kp9 = 97,
    /// <summary>The keypad's 0.</summary>
    Kp0 = 98,
    /// <summary>The keypad's decimal point, Delete with Num Lock off.</summary>
    KpPeriod = 99,

    /// <summary>
    /// Additional key on ISO keyboards between left shift and Z.
    /// Produces backslash in US/UK layout, less-than sign in German/French layout.
    /// </summary>
    NonUsBackSlash = 100,

    /// <summary>Windows contextual menu / Compose key.</summary>
    KbMenu = 101,

    /// <summary>Power key (status flag on USB spec, physical key on some Mac keyboards).</summary>
    Power = 102,
    /// <summary>The keypad's equals sign, on Mac keyboards.</summary>
    KpEqual = 103,
    /// <summary>F13, on keyboards with more than twelve function keys.</summary>
    F13 = 104,
    /// <summary>F14, on keyboards with more than twelve function keys.</summary>
    F14 = 105,
    /// <summary>F15, on keyboards with more than twelve function keys.</summary>
    F15 = 106,
    /// <summary>F16, on keyboards with more than twelve function keys.</summary>
    F16 = 107,
    /// <summary>F17, on keyboards with more than twelve function keys.</summary>
    F17 = 108,
    /// <summary>F18, on keyboards with more than twelve function keys.</summary>
    F18 = 109,
    /// <summary>F19, on keyboards with more than twelve function keys.</summary>
    F19 = 110,
    /// <summary>F20, on keyboards with more than twelve function keys.</summary>
    F20 = 111,
    /// <summary>F21, on keyboards with more than twelve function keys.</summary>
    F21 = 112,
    /// <summary>F22, on keyboards with more than twelve function keys.</summary>
    F22 = 113,
    /// <summary>F23, on keyboards with more than twelve function keys.</summary>
    F23 = 114,
    /// <summary>F24, on keyboards with more than twelve function keys.</summary>
    F24 = 115,
    /// <summary>Execute, a key of the USB standard that few keyboards have.</summary>
    Execute = 116,

    /// <summary>AL Integrated Help Center.</summary>
    Help = 117,

    /// <summary>Show menu.</summary>
    Menu = 118,
    /// <summary>Select, a key of the USB standard that few keyboards have.</summary>
    Select = 119,

    /// <summary>AC Stop.</summary>
    Stop = 120,

    /// <summary>AC Redo / Repeat.</summary>
    Again = 121,

    /// <summary>AC Undo.</summary>
    Undo = 122,

    /// <summary>AC Cut.</summary>
    Cut = 123,

    /// <summary>AC Copy.</summary>
    Copy = 124,

    /// <summary>AC Paste.</summary>
    Paste = 125,

    /// <summary>AC Find.</summary>
    Find = 126,
    /// <summary>Mutes the sound, on keyboards with media keys.</summary>
    Mute = 127,
    /// <summary>Turns the sound up, on keyboards with media keys.</summary>
    VolumeUp = 128,
    /// <summary>Turns the sound down, on keyboards with media keys.</summary>
    VolumeDown = 129,
    /// <summary>The keypad's comma, on Brazilian and some other keyboards.</summary>
    KpComma = 133,
    /// <summary>The keypad's equals sign on AS/400 keyboards.</summary>
    KpEqualsAs400 = 134,

    /// <summary>Used on Asian keyboards.</summary>
    International1 = 135,
    /// <summary>International key 2 of the USB standard, on Japanese and other Asian layouts.</summary>
    International2 = 136,

    /// <summary>Yen key.</summary>
    International3 = 137,
    /// <summary>International key 4 of the USB standard, on Japanese and other Asian layouts.</summary>
    International4 = 138,
    /// <summary>International key 5 of the USB standard, on Japanese and other Asian layouts.</summary>
    International5 = 139,
    /// <summary>International key 6 of the USB standard, on Japanese and other Asian layouts.</summary>
    International6 = 140,
    /// <summary>International key 7 of the USB standard, on Japanese and other Asian layouts.</summary>
    International7 = 141,
    /// <summary>International key 8 of the USB standard, on Japanese and other Asian layouts.</summary>
    International8 = 142,
    /// <summary>International key 9 of the USB standard, on Japanese and other Asian layouts.</summary>
    International9 = 143,

    /// <summary>Hangul / English toggle.</summary>
    Lang1 = 144,

    /// <summary>Hanja conversion.</summary>
    Lang2 = 145,

    /// <summary>Katakana.</summary>
    Lang3 = 146,

    /// <summary>Hiragana.</summary>
    Lang4 = 147,

    /// <summary>Zenkaku / Hankaku.</summary>
    Lang5 = 148,
    /// <summary>Language key 6 of the USB standard, reserved for layouts that need it.</summary>
    Lang6 = 149,
    /// <summary>Language key 7 of the USB standard, reserved for layouts that need it.</summary>
    Lang7 = 150,
    /// <summary>Language key 8 of the USB standard, reserved for layouts that need it.</summary>
    Lang8 = 151,
    /// <summary>Language key 9 of the USB standard, reserved for layouts that need it.</summary>
    Lang9 = 152,

    /// <summary>Erase-Eaze.</summary>
    AltErase = 153,
    /// <summary>System Request, Print Screen with Alt held on a PC keyboard.</summary>
    SysReq = 154,

    /// <summary>AC Cancel.</summary>
    Cancel = 155,
    /// <summary>Clear, on some Mac and terminal keyboards.</summary>
    Clear = 156,
    /// <summary>Prior, a key of the USB standard that few keyboards have.</summary>
    Prior = 157,
    /// <summary>A second Return, a key of the USB standard that few keyboards have.</summary>
    Return2 = 158,
    /// <summary>Separator, a key of the USB standard that few keyboards have.</summary>
    Separator = 159,
    /// <summary>Out, a key of the USB standard that few keyboards have.</summary>
    Out = 160,
    /// <summary>Oper, a key of the USB standard that few keyboards have.</summary>
    Oper = 161,
    /// <summary>Clear/Again, a key of the USB standard that few keyboards have.</summary>
    ClearAgain = 162,
    /// <summary>CrSel/Props, a key of the USB standard that few keyboards have.</summary>
    CrSel = 163,
    /// <summary>ExSel, a key of the USB standard that few keyboards have.</summary>
    ExSel = 164,
    /// <summary>The keypad's 00 key, on some accounting keyboards.</summary>
    Kp00 = 176,
    /// <summary>The keypad's 000 key, on some accounting keyboards.</summary>
    Kp000 = 177,
    /// <summary>The thousands separator of the USB standard, which few keyboards have.</summary>
    ThousandsSeparator = 178,
    /// <summary>The decimal separator of the USB standard, which few keyboards have.</summary>
    DecimalSeparator = 179,
    /// <summary>The currency unit of the USB standard, which few keyboards have.</summary>
    CurrencyUnit = 180,
    /// <summary>The currency subunit of the USB standard, which few keyboards have.</summary>
    CurrencySubunit = 181,
    /// <summary>The keypad's left parenthesis, on keypads that have one.</summary>
    KpLeftParen = 182,
    /// <summary>The keypad's right parenthesis, on keypads that have one.</summary>
    KpRightParen = 183,
    /// <summary>The keypad's left brace, on keypads that have one.</summary>
    KpLeftBrace = 184,
    /// <summary>The keypad's right brace, on keypads that have one.</summary>
    KpRightBrace = 185,
    /// <summary>The keypad's Tab, on keypads that have one.</summary>
    KpTab = 186,
    /// <summary>The keypad's Backspace, on keypads that have one.</summary>
    KpBackspace = 187,
    /// <summary>The keypad's hexadecimal A, on programmers' keypads.</summary>
    KpA = 188,
    /// <summary>The keypad's hexadecimal B, on programmers' keypads.</summary>
    KpB = 189,
    /// <summary>The keypad's hexadecimal C, on programmers' keypads.</summary>
    KpC = 190,
    /// <summary>The keypad's hexadecimal D, on programmers' keypads.</summary>
    KpD = 191,
    /// <summary>The keypad's hexadecimal E, on programmers' keypads.</summary>
    KpE = 192,
    /// <summary>The keypad's hexadecimal F, on programmers' keypads.</summary>
    KpF = 193,
    /// <summary>The keypad's exclusive or, on keypads that have one.</summary>
    KpXor = 194,
    /// <summary>The keypad's power key, on keypads that have one.</summary>
    KpPower = 195,
    /// <summary>The keypad's percent sign, on keypads that have one.</summary>
    KpPercent = 196,
    /// <summary>The keypad's less-than sign, on keypads that have one.</summary>
    KpLess = 197,
    /// <summary>The keypad's greater-than sign, on keypads that have one.</summary>
    KpGreater = 198,
    /// <summary>The keypad's ampersand, on keypads that have one.</summary>
    KpAmpersand = 199,
    /// <summary>The keypad's double ampersand, on keypads that have one.</summary>
    KpDblAmpersand = 200,
    /// <summary>The keypad's vertical bar, on keypads that have one.</summary>
    KpVerticalBar = 201,
    /// <summary>The keypad's double vertical bar, on keypads that have one.</summary>
    KpDblVerticalBar = 202,
    /// <summary>The keypad's colon, on keypads that have one.</summary>
    KpColon = 203,
    /// <summary>The keypad's hash sign, on keypads that have one.</summary>
    KpHash = 204,
    /// <summary>The keypad's space, on keypads that have one.</summary>
    KpSpace = 205,
    /// <summary>The keypad's at sign, on keypads that have one.</summary>
    KpAt = 206,
    /// <summary>The keypad's exclamation mark, on keypads that have one.</summary>
    KpExClam = 207,
    /// <summary>The keypad's memory store, on keypads that have one.</summary>
    KpMemStore = 208,
    /// <summary>The keypad's memory recall, on keypads that have one.</summary>
    KpMemRecall = 209,
    /// <summary>The keypad's memory clear, on keypads that have one.</summary>
    KpMemClear = 210,
    /// <summary>The keypad's memory add, on keypads that have one.</summary>
    KpMemAdd = 211,
    /// <summary>The keypad's memory subtract, on keypads that have one.</summary>
    KpMemSubtract = 212,
    /// <summary>The keypad's memory multiply, on keypads that have one.</summary>
    KpMemMultiply = 213,
    /// <summary>The keypad's memory divide, on keypads that have one.</summary>
    KpMemDivide = 214,
    /// <summary>The keypad's plus-minus key, on keypads that have one.</summary>
    KpPlusMinus = 215,
    /// <summary>The keypad's Clear, on keypads that have one.</summary>
    KpClear = 216,
    /// <summary>The keypad's Clear Entry, on keypads that have one.</summary>
    KpClearEntry = 217,
    /// <summary>The keypad's binary key, on keypads that have one.</summary>
    KpBinary = 218,
    /// <summary>The keypad's octal key, on keypads that have one.</summary>
    KpOctal = 219,
    /// <summary>The keypad's decimal key, on keypads that have one.</summary>
    KpDecimal = 220,
    /// <summary>The keypad's hexadecimal key, on keypads that have one.</summary>
    KpHexadecimal = 221,
    /// <summary>The left Control key.</summary>
    LeftControl = 224,
    /// <summary>The left Shift key.</summary>
    LeftShift = 225,

    /// <summary>Left Alt / Option.</summary>
    LeftAlt = 226,

    /// <summary>Left GUI, the Windows, Command (Apple) or Meta key.</summary>
    LeftSuper = 227,
    /// <summary>The right Control key.</summary>
    RightControl = 228,
    /// <summary>The right Shift key.</summary>
    RightShift = 229,

    /// <summary>Right Alt / AltGr / Option.</summary>
    RightAlt = 230,

    /// <summary>Right GUI, the Windows, Command (Apple) or Meta key.</summary>
    RightSuper = 231,

    /// <summary>Mode key (SDL_KMOD_MODE).</summary>
    Mode = 257,

    /// <summary>Sleep.</summary>
    Sleep = 258,

    /// <summary>Wake.</summary>
    Wake = 259,

    /// <summary>Channel Increment.</summary>
    ChannelIncrement = 260,

    /// <summary>Channel Decrement.</summary>
    ChannelDecrement = 261,

    /// <summary>Media Play.</summary>
    MediaPlay = 262,

    /// <summary>Media Pause.</summary>
    MediaPause = 263,

    /// <summary>Media Record.</summary>
    MediaRecord = 264,

    /// <summary>Media Fast Forward.</summary>
    MediaFastForward = 265,

    /// <summary>Media Rewind.</summary>
    MediaRewind = 266,

    /// <summary>Media Next Track.</summary>
    MediaNextTrack = 267,

    /// <summary>Media Previous Track.</summary>
    MediaPreviousTrack = 268,

    /// <summary>Media Stop.</summary>
    MediaStop = 269,

    /// <summary>Media Eject.</summary>
    MediaEject = 270,

    /// <summary>Media Play / Pause toggle.</summary>
    MediaPlayPause = 271,

    /// <summary>Media Select.</summary>
    MediaSelect = 272,

    /// <summary>AC New.</summary>
    ACNew = 273,

    /// <summary>AC Open.</summary>
    ACOpen = 274,

    /// <summary>AC Close.</summary>
    ACClose = 275,

    /// <summary>AC Exit.</summary>
    ACExit = 276,

    /// <summary>AC Save.</summary>
    ACSave = 277,

    /// <summary>AC Print.</summary>
    ACPrint = 278,

    /// <summary>AC Properties.</summary>
    ACProperties = 279,

    /// <summary>AC Search.</summary>
    ACSearch = 280,

    /// <summary>AC Home.</summary>
    ACHome = 281,

    /// <summary>AC Back.</summary>
    Back = 282,

    /// <summary>AC Forward.</summary>
    ACForward = 283,

    /// <summary>AC Stop.</summary>
    ACStop = 284,

    /// <summary>AC Refresh.</summary>
    ACRefresh = 285,

    /// <summary>AC Bookmarks.</summary>
    ACBookmarks = 286,

    /// <summary>Soft-key left (phones).</summary>
    SoftLeft = 287,

    /// <summary>Soft-key right (phones).</summary>
    SoftRight = 288,

    /// <summary>Accept phone call.</summary>
    Call = 289,

    /// <summary>Reject phone call.</summary>
    EndCall = 290,

    /// <summary>400 to 500 are reserved for dynamic keycodes.</summary>
    Reserved = 400,

    /// <summary>Not a key. It marks the upper bound of the scancode array.</summary>
    Count = 512,
}
