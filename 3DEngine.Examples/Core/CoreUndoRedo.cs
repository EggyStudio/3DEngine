// raylib's core_undo_redo example, Copyright (c) 2025 Ramon Santamaria (@raysan5), under the zlib
// license, written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class CoreUndoRedo
{
    private const int MAX_UNDO_STATES = 26;

    private const int GRID_CELL_SIZE = 24;
    private const int MAX_GRID_CELLS_X = 30;
    private const int MAX_GRID_CELLS_Y = 13;

    // A cell of the grid, as Vector2 in whole numbers.
    private record struct Point(int X, int Y);

    // All of the player that undo and redo bring back, compared and copied as a value.
    private record struct PlayerState(Point Cell, Color Color);

    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        // Each change of the player is kept, checked for every two frames, which costs more than
        // keeping the actions that made it, as raylib's own automation does, and is simple.
        InitWindow(screenWidth, screenHeight, "[core] undo redo");

        int currentUndoIndex = 0;
        int firstUndoIndex = 0;
        int lastUndoIndex = 0;
        int undoFrameCounter = 0;
        Vector2 undoInfoPos = new(110, 400);

        var player = new PlayerState(new Point(10, 10), Color.Red);

        // A ring of states, every one the player to begin with.
        var states = new PlayerState[MAX_UNDO_STATES];
        for (int i = 0; i < MAX_UNDO_STATES; i++) states[i] = player;

        Vector2 gridPosition = new(40, 60);

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            var cell = player.Cell;
            if (IsKeyPressed(Key.Right)) cell.X++;
            else if (IsKeyPressed(Key.Left)) cell.X--;
            else if (IsKeyPressed(Key.Up)) cell.Y--;
            else if (IsKeyPressed(Key.Down)) cell.Y++;

            if (cell.X < 0) cell.X = 0;
            else if (cell.X >= MAX_GRID_CELLS_X) cell.X = MAX_GRID_CELLS_X - 1;
            if (cell.Y < 0) cell.Y = 0;
            else if (cell.Y >= MAX_GRID_CELLS_Y) cell.Y = MAX_GRID_CELLS_Y - 1;
            player = player with { Cell = cell };

            if (IsKeyPressed(Key.Space))
            {
                player = player with { Color = player.Color with
                {
                    R = (byte)GetRandomValue(20, 255),
                    G = (byte)GetRandomValue(20, 220),
                    B = (byte)GetRandomValue(20, 240),
                } };
            }

            undoFrameCounter++;

            if (undoFrameCounter >= 2)
            {
                if (states[currentUndoIndex] != player)
                {
                    currentUndoIndex++;
                    if (currentUndoIndex >= MAX_UNDO_STATES) currentUndoIndex = 0;
                    if (currentUndoIndex == firstUndoIndex) firstUndoIndex++;
                    if (firstUndoIndex >= MAX_UNDO_STATES) firstUndoIndex = 0;

                    states[currentUndoIndex] = player;
                    lastUndoIndex = currentUndoIndex;
                }

                undoFrameCounter = 0;
            }

            if (IsKeyDown(Key.LeftControl) && IsKeyPressed(Key.Z))
            {
                if (currentUndoIndex != firstUndoIndex)
                {
                    currentUndoIndex--;
                    if (currentUndoIndex < 0) currentUndoIndex = MAX_UNDO_STATES - 1;

                    player = states[currentUndoIndex];
                }
            }

            if (IsKeyDown(Key.LeftControl) && IsKeyPressed(Key.Y))
            {
                if (currentUndoIndex != lastUndoIndex)
                {
                    int nextUndoIndex = currentUndoIndex + 1;
                    if (nextUndoIndex >= MAX_UNDO_STATES) nextUndoIndex = 0;

                    if (nextUndoIndex != firstUndoIndex)
                    {
                        currentUndoIndex = nextUndoIndex;
                        player = states[currentUndoIndex];
                    }
                }
            }

            BeginDrawing();
                ClearBackground(Color.RayWhite);

                DrawText("[ARROWS] MOVE PLAYER - [SPACE] CHANGE PLAYER COLOR", 40, 20, 20, Color.DarkGray);

                // The cells the undo states visited, the ring running past its end and on from its start.
                if (lastUndoIndex > firstUndoIndex)
                {
                    for (int i = firstUndoIndex; i < currentUndoIndex; i++)
                        DrawRectangleRec(new Rectangle(gridPosition.X + states[i].Cell.X*GRID_CELL_SIZE, gridPosition.Y + states[i].Cell.Y*GRID_CELL_SIZE,
                            GRID_CELL_SIZE, GRID_CELL_SIZE), Color.LightGray);
                }
                else if (firstUndoIndex > lastUndoIndex)
                {
                    if ((currentUndoIndex < MAX_UNDO_STATES) && (currentUndoIndex > lastUndoIndex))
                    {
                        for (int i = firstUndoIndex; i < currentUndoIndex; i++)
                            DrawRectangleRec(new Rectangle(gridPosition.X + states[i].Cell.X*GRID_CELL_SIZE, gridPosition.Y + states[i].Cell.Y*GRID_CELL_SIZE,
                                GRID_CELL_SIZE, GRID_CELL_SIZE), Color.LightGray);
                    }
                    else
                    {
                        for (int i = firstUndoIndex; i < MAX_UNDO_STATES; i++)
                            DrawRectangle((int)gridPosition.X + states[i].Cell.X*GRID_CELL_SIZE, (int)gridPosition.Y + states[i].Cell.Y*GRID_CELL_SIZE,
                                GRID_CELL_SIZE, GRID_CELL_SIZE, Color.LightGray);
                        for (int i = 0; i < currentUndoIndex; i++)
                            DrawRectangle((int)gridPosition.X + states[i].Cell.X*GRID_CELL_SIZE, (int)gridPosition.Y + states[i].Cell.Y*GRID_CELL_SIZE,
                                GRID_CELL_SIZE, GRID_CELL_SIZE, Color.LightGray);
                    }
                }

                for (int y = 0; y <= MAX_GRID_CELLS_Y; y++)
                    DrawLine((int)gridPosition.X, (int)gridPosition.Y + y*GRID_CELL_SIZE,
                        (int)gridPosition.X + MAX_GRID_CELLS_X*GRID_CELL_SIZE, (int)gridPosition.Y + y*GRID_CELL_SIZE, Color.Gray);
                for (int x = 0; x <= MAX_GRID_CELLS_X; x++)
                    DrawLine((int)gridPosition.X + x*GRID_CELL_SIZE, (int)gridPosition.Y,
                        (int)gridPosition.X + x*GRID_CELL_SIZE, (int)gridPosition.Y + MAX_GRID_CELLS_Y*GRID_CELL_SIZE, Color.Gray);

                DrawRectangle((int)gridPosition.X + player.Cell.X*GRID_CELL_SIZE, (int)gridPosition.Y + player.Cell.Y*GRID_CELL_SIZE,
                    GRID_CELL_SIZE + 1, GRID_CELL_SIZE + 1, player.Color);

                DrawText("UNDO STATES:", (int)undoInfoPos.X - 85, (int)undoInfoPos.Y + 9, 10, Color.DarkGray);
                DrawUndoBuffer(undoInfoPos, firstUndoIndex, lastUndoIndex, currentUndoIndex, 24);

            EndDrawing();
        }

        CloseWindow();
    }

    // The ring of states, each square a state that can be kept.
    private static void DrawUndoBuffer(Vector2 position, int firstUndoIndex, int lastUndoIndex, int currentUndoIndex, int slotSize)
    {
        DrawRectangle((int)position.X + 8 + slotSize*currentUndoIndex, (int)position.Y - 10, 8, 8, Color.Red);
        DrawRectangleLines((int)position.X + 2 + slotSize*firstUndoIndex, (int)position.Y + 27, 8, 8, Color.Black);
        DrawRectangle((int)position.X + 14 + slotSize*lastUndoIndex, (int)position.Y + 27, 8, 8, Color.Black);

        for (int i = 0; i < MAX_UNDO_STATES; i++)
        {
            DrawRectangle((int)position.X + slotSize*i, (int)position.Y, slotSize, slotSize, Color.LightGray);
            DrawRectangleLines((int)position.X + slotSize*i, (int)position.Y, slotSize, slotSize, Color.Gray);
        }

        void Slot(int i, Color fill, Color line)
        {
            DrawRectangle((int)position.X + slotSize*i, (int)position.Y, slotSize, slotSize, fill);
            DrawRectangleLines((int)position.X + slotSize*i, (int)position.Y, slotSize, slotSize, line);
        }

        if (firstUndoIndex <= lastUndoIndex)
        {
            for (int i = firstUndoIndex; i < lastUndoIndex + 1; i++) Slot(i, Color.SkyBlue, Color.Blue);
        }
        else if (lastUndoIndex < firstUndoIndex)
        {
            for (int i = firstUndoIndex; i < MAX_UNDO_STATES; i++) Slot(i, Color.SkyBlue, Color.Blue);
            for (int i = 0; i < lastUndoIndex + 1; i++) Slot(i, Color.SkyBlue, Color.Blue);
        }

        if (firstUndoIndex < currentUndoIndex)
        {
            for (int i = firstUndoIndex; i < currentUndoIndex; i++) Slot(i, Color.Green, Color.Lime);
        }
        else if (currentUndoIndex < firstUndoIndex)
        {
            for (int i = firstUndoIndex; i < MAX_UNDO_STATES; i++) Slot(i, Color.Green, Color.Lime);
            for (int i = 0; i < currentUndoIndex; i++) Slot(i, Color.Green, Color.Lime);
        }

        Slot(currentUndoIndex, Color.Gold, Color.Orange);
    }
}
