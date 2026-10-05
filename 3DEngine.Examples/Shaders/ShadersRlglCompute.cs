// raylib's shaders_rlgl_compute example, Copyright (c) 2021-2025 Teddy Astie (@tsnake41), under the
// zlib license, written again for the flat API, with its compute functions in rlgl's place.

using System.Numerics;
using System.Runtime.CompilerServices;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class ShadersRlglCompute
{
    // IMPORTANT: This must match gol*.slang GOL_WIDTH constant
    // This must be a multiple of 16 (check golLogic compute dispatch)
    private const int GOL_WIDTH = 768;

    // Maximum amount of queued draw commands (squares draw from mouse down events)
    private const int MAX_BUFFERED_TRANSFERTS = 48;

    // Game Of Life Update Command
    private struct GolUpdateCmd
    {
        public uint x;          // x coordinate of the gol command
        public uint y;          // y coordinate of the gol command
        public uint w;          // width of the filled zone
        public uint enabled;    // whether to enable or disable zone
    }

    [InlineArray(MAX_BUFFERED_TRANSFERTS)]
    private struct GolUpdateCmds
    {
        private GolUpdateCmd _command;
    }

    // Game Of Life Update Commands SSBO
    private struct GolUpdateSSBO
    {
        public uint count;
        public GolUpdateCmds commands;
    }

    public static void Run()
    {
        const int screenWidth = GOL_WIDTH;
        const int screenHeight = GOL_WIDTH;

        InitWindow(screenWidth, screenHeight, "[shaders] rlgl compute");

        Vector2 resolution = new((float)screenWidth, (float)screenHeight);
        uint brushSize = 8;

        // Game of Life logic compute shader, raylib's gol.glsl written in Slang
        Shader golLogicProgram = LoadComputeShader("resources/shaders/slang/gol.slang");

        // Game of Life logic render shader
        Shader golRenderShader = LoadShader("resources/shaders/slang/gol_render.slang");
        int resUniformLoc = GetShaderLocation(golRenderShader, "resolution");

        // Game of Life transfert shader (CPU<->GPU download and upload)
        Shader golTransfertProgram = LoadComputeShader("resources/shaders/slang/gol_transfert.slang");

        // Shader storage buffers, which raylib binds by number and each shader here names
        ShaderBuffer ssboA = LoadShaderBuffer(GOL_WIDTH*GOL_WIDTH*sizeof(uint));
        ShaderBuffer ssboB = LoadShaderBuffer(GOL_WIDTH*GOL_WIDTH*sizeof(uint));
        ShaderBuffer ssboTransfert = LoadShaderBuffer(Unsafe.SizeOf<GolUpdateSSBO>());

        GolUpdateSSBO transfertBuffer = default;

        // Create a white texture of the size of the window to update
        // each pixel of the window using the fragment shader: golRenderShader
        Image whiteImage = GenImageColor(GOL_WIDTH, GOL_WIDTH, Color.White);
        Texture2D whiteTex = LoadTextureFromImage(whiteImage);
        UnloadImage(whiteImage);

        SetTargetFPS(0);                    // Set our game to run with an uncapped framerate

        while (!WindowShouldClose())
        {
            brushSize = unchecked(brushSize + (uint)(int)GetMouseWheelMove());

            if ((IsMouseButtonDown(MouseButton.Left) || IsMouseButtonDown(MouseButton.Right))
                && (transfertBuffer.count < MAX_BUFFERED_TRANSFERTS))
            {
                // Buffer a new command
                ref GolUpdateCmd command = ref transfertBuffer.commands[(int)transfertBuffer.count];
                command.x = unchecked((uint)GetMouseX() - brushSize/2);
                command.y = unchecked((uint)GetMouseY() - brushSize/2);
                command.w = brushSize;
                command.enabled = IsMouseButtonDown(MouseButton.Left) ? 1u : 0u;
                transfertBuffer.count++;
            }
            else if (transfertBuffer.count > 0)  // Process transfert buffer
            {
                // Send SSBO buffer to GPU
                UpdateShaderBuffer<GolUpdateSSBO>(ssboTransfert, [transfertBuffer], 0);

                // Process SSBO commands on GPU
                SetShaderValueBuffer(golTransfertProgram, GetShaderLocation(golTransfertProgram, "golBuffer"), ssboA);
                SetShaderValueBuffer(golTransfertProgram, GetShaderLocation(golTransfertProgram, "golUpdate"), ssboTransfert);
                ComputeShaderDispatch(golTransfertProgram, (int)transfertBuffer.count, 1, 1); // Each GPU unit processes a command

                transfertBuffer.count = 0;
            }
            else
            {
                // Process game of life logic
                SetShaderValueBuffer(golLogicProgram, GetShaderLocation(golLogicProgram, "golBuffer"), ssboA);
                SetShaderValueBuffer(golLogicProgram, GetShaderLocation(golLogicProgram, "golBufferDest"), ssboB);
                ComputeShaderDispatch(golLogicProgram, GOL_WIDTH/16, GOL_WIDTH/16, 1);

                // ssboA <-> ssboB
                (ssboA, ssboB) = (ssboB, ssboA);
            }

            SetShaderValueBuffer(golRenderShader, GetShaderLocation(golRenderShader, "golBuffer"), ssboA);
            SetShaderValue(golRenderShader, resUniformLoc, resolution);

            BeginDrawing();

                ClearBackground(Color.Blank);

                BeginShaderMode(golRenderShader);
                    DrawTexture(whiteTex, 0, 0, Color.White);
                EndShaderMode();

                DrawRectangleLines(GetMouseX() - (int)brushSize/2, GetMouseY() - (int)brushSize/2, (int)brushSize, (int)brushSize, Color.Red);

                DrawText("Use Mouse wheel to increase/decrease brush size", 10, 10, 20, Color.White);
                DrawFPS(GetScreenWidth() - 100, 10);

            EndDrawing();
        }

        // Unload shader buffers objects
        UnloadShaderBuffer(ssboA);
        UnloadShaderBuffer(ssboB);
        UnloadShaderBuffer(ssboTransfert);

        // Unload compute shaders
        UnloadShader(golTransfertProgram);
        UnloadShader(golLogicProgram);

        UnloadTexture(whiteTex);            // Unload white texture
        UnloadShader(golRenderShader);      // Unload rendering fragment shader

        CloseWindow();
    }
}
