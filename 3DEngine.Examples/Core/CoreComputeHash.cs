// raylib's core_compute_hash example, Copyright (c) 2025 Ramon Santamaria (@raysan5), under the zlib
// license, written again for the flat API.

using System.Buffers.Binary;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using ImGuiNET;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class CoreComputeHash
{
    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[core] compute hash");

        // raygui's default style is light, and ImGui's light one keeps the labels readable on raylib's white.
        ImGui.StyleColorsLight();

        string textInput = "The quick brown fox jumps over the lazy dog.";
        bool btnComputeHashes = false;

        // raylib's hashes are words, each written as eight hex digits. Its MD5 words are the
        // digest's bytes read four at a time from the low end, and its SHA words from the high,
        // so its SHA text is the digest's usual hex and its MD5 text has each word's bytes turned.
        uint[] hashCRC32 = [0];
        uint[]? hashMD5 = null;
        uint[]? hashSHA1 = null;
        uint[]? hashSHA256 = null;

        string base64Text = "";

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            if (btnComputeHashes)
            {
                byte[] data = Encoding.UTF8.GetBytes(textInput);

                // raylib's EncodeDataBase64, ComputeCRC32, ComputeMD5, ComputeSHA1 and ComputeSHA256
                // are C#'s Convert and System.Security.Cryptography here, and a CRC-32 of its own.
                base64Text = Convert.ToBase64String(data);

                hashCRC32 = [ComputeCRC32(data)];
                hashMD5 = Words(MD5.HashData(data), littleEndian: true);
                hashSHA1 = Words(SHA1.HashData(data), littleEndian: false);
                hashSHA256 = Words(SHA256.HashData(data), littleEndian: false);
            }

            BeginDrawing();

                ClearBackground(Color.RayWhite);

                // raygui's controls, as ImGui's, where raygui places them, the headings at raygui's
                // text size of 20 and the rest at ImGui's own.
                ImGui.SetNextWindowPos(Vector2.Zero);
                ImGui.SetNextWindowSize(new Vector2(screenWidth, screenHeight));
                ImGui.Begin("##controls", ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoBringToFrontOnFocus);

                ImGui.SetWindowFontScale(20.0f/13.0f);
                Label(new Rectangle(40, 26, 720, 32), "INPUT DATA (TEXT):");
                ImGui.SetWindowFontScale(1);

                TextBox(new Rectangle(40, 64, 720, 32), "##input", ref textInput, 95, ImGuiInputTextFlags.None);

                ImGui.SetCursorScreenPos(new Vector2(40, 64 + 40));
                btnComputeHashes = ImGui.Button("COMPUTE INPUT DATA HASHES", new Vector2(720, 32));

                ImGui.SetWindowFontScale(20.0f/13.0f);
                Label(new Rectangle(40, 160, 720, 32), "INPUT DATA HASH VALUES:");
                ImGui.SetWindowFontScale(1);

                Label(new Rectangle(40, 200, 120, 32), "CRC32 [32 bit]:");
                ReadOnlyBox(new Rectangle(40 + 120, 200, 720 - 120, 32), "##crc32", GetDataAsHexText(hashCRC32, 1));
                Label(new Rectangle(40, 200 + 36, 120, 32), "MD5 [128 bit]:");
                ReadOnlyBox(new Rectangle(40 + 120, 200 + 36, 720 - 120, 32), "##md5", GetDataAsHexText(hashMD5, 4));
                Label(new Rectangle(40, 200 + 36*2, 120, 32), "SHA1 [160 bit]:");
                ReadOnlyBox(new Rectangle(40 + 120, 200 + 36*2, 720 - 120, 32), "##sha1", GetDataAsHexText(hashSHA1, 5));
                Label(new Rectangle(40, 200 + 36*3, 120, 32), "SHA256 [256 bit]:");
                ReadOnlyBox(new Rectangle(40 + 120, 200 + 36*3, 720 - 120, 32), "##sha256", GetDataAsHexText(hashSHA256, 8));

                // raygui draws this label as a focused control, in its accent.
                ImGui.PushStyleColor(ImGuiCol.Text, ImGui.GetColorU32(ImGuiCol.ButtonActive));
                Label(new Rectangle(40, 200 + 36*5 - 30, 320, 32), "BONUS - BAS64 ENCODED STRING:");
                ImGui.PopStyleColor();
                Label(new Rectangle(40, 200 + 36*5, 120, 32), "BASE64 ENCODING:");
                ReadOnlyBox(new Rectangle(40 + 120, 200 + 36*5, 720 - 120, 32), "##base64", base64Text);

                ImGui.End();

            EndDrawing();
        }

        CloseWindow();
    }

    // raygui's label, its text centered on the rectangle's height.
    private static void Label(Rectangle bounds, string text)
    {
        ImGui.SetCursorScreenPos(new Vector2(bounds.X, bounds.Y + (bounds.Height - ImGui.GetTextLineHeight())/2));
        ImGui.TextUnformatted(text);
    }

    // raygui's text box, as tall as its rectangle.
    private static void TextBox(Rectangle bounds, string id, ref string text, uint maxLength, ImGuiInputTextFlags flags)
    {
        ImGui.SetCursorScreenPos(new Vector2(bounds.X, bounds.Y));
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(4, (bounds.Height - ImGui.GetFontSize())/2));
        ImGui.SetNextItemWidth(bounds.Width);
        ImGui.InputText(id, ref text, maxLength, flags);
        ImGui.PopStyleVar();
    }

    private static void ReadOnlyBox(Rectangle bounds, string id, string text) =>
        TextBox(bounds, id, ref text, 120, ImGuiInputTextFlags.ReadOnly);

    private static string GetDataAsHexText(uint[]? data, int dataSize)
    {
        if ((data != null) && (dataSize > 0) && (dataSize < ((128/8) - 1)))
            return string.Concat(data.Take(dataSize).Select(word => word.ToString("X8")));

        return "00000000";
    }

    private static uint[] Words(byte[] digest, bool littleEndian)
    {
        var words = new uint[digest.Length/4];
        for (int i = 0; i < words.Length; i++)
        {
            var bytes = digest.AsSpan(i*4, 4);
            words[i] = littleEndian ? BinaryPrimitives.ReadUInt32LittleEndian(bytes) : BinaryPrimitives.ReadUInt32BigEndian(bytes);
        }
        return words;
    }

    // CRC-32 as raylib computes it, the reflected polynomial 0xEDB88320, which .NET keeps in a
    // package of its own rather than in the runtime.
    private static uint ComputeCRC32(byte[] data)
    {
        uint crc = 0xFFFFFFFF;
        foreach (byte b in data)
        {
            crc ^= b;
            for (int k = 0; k < 8; k++) crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320 : crc >> 1;
        }
        return ~crc;
    }
}
