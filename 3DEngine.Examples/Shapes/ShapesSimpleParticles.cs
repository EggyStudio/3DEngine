// raylib's shapes_simple_particles example, Copyright (c) 2025 Jordi Santonja (@JordSant), under the zlib
// license, written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class ShapesSimpleParticles
{
    private const int MAX_PARTICLES = 3000;

    private enum ParticleType
    {
        WATER = 0,
        SMOKE,
        FIRE,
    }

    private static readonly string[] particleTypeNames = ["WATER", "SMOKE", "FIRE"];

    private struct Particle
    {
        public ParticleType type;
        public Vector2 position;
        public Vector2 velocity;
        public float radius;
        public Color color;
        public float lifeTime;
        public bool alive;
    }

    private sealed class CircularBuffer(Particle[] buffer)
    {
        public int head;
        public int tail;
        public readonly Particle[] buffer = buffer;
    }

    private const float DEG2RAD = MathF.PI/180.0f;

    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[shapes] simple particles");

        Particle[] particles = new Particle[MAX_PARTICLES];
        CircularBuffer circularBuffer = new(particles);

        int emissionRate = -2;
        ParticleType currentType = ParticleType.WATER;
        Vector2 emitterPosition = new(screenWidth/2.0f, screenHeight/2.0f);

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            if (emissionRate < 0)
            {
                if (Random.Shared.Next()%(-emissionRate) == 0) EmitParticle(circularBuffer, emitterPosition, currentType);
            }
            else
            {
                for (int i = 0; i <= emissionRate; i++) EmitParticle(circularBuffer, emitterPosition, currentType);
            }

            UpdateParticles(circularBuffer, screenWidth, screenHeight);

            UpdateCircularBuffer(circularBuffer);

            if (IsKeyPressed(Key.Up)) emissionRate++;
            if (IsKeyPressed(Key.Down)) emissionRate--;

            if (IsKeyPressed(Key.Right)) currentType = (currentType == ParticleType.FIRE) ? ParticleType.WATER : currentType + 1;
            if (IsKeyPressed(Key.Left)) currentType = (currentType == ParticleType.WATER) ? ParticleType.FIRE : currentType - 1;

            if (IsMouseButtonDown(MouseButton.Left)) emitterPosition = GetMousePosition();

            BeginDrawing();

                ClearBackground(Color.RayWhite);

                DrawParticles(circularBuffer);

                DrawRectangle(5, 5, 315, 75, Fade(Color.SkyBlue, 0.5f));
                DrawRectangleLines(5, 5, 315, 75, Color.Blue);

                DrawText("CONTROLS:", 15, 15, 10, Color.Black);
                DrawText("UP/DOWN: Change Particle Emission Rate", 15, 35, 10, Color.Black);
                DrawText("LEFT/RIGHT: Change Particle Type (Water, Smoke, Fire)", 15, 55, 10, Color.Black);

                if (emissionRate < 0) DrawText($"Particles every {-emissionRate} frames | Type: {particleTypeNames[(int)currentType]}", 15, 95, 10, Color.DarkGray);
                else DrawText($"{emissionRate + 1} Particles per frame | Type: {particleTypeNames[(int)currentType]}", 15, 95, 10, Color.DarkGray);

                DrawFPS(screenWidth - 80, 10);

            EndDrawing();
        }

        CloseWindow();
    }

    private static void EmitParticle(CircularBuffer circularBuffer, Vector2 emitterPosition, ParticleType type)
    {
        int index = AddToCircularBuffer(circularBuffer);

        // A full buffer gives no particle.
        if (index >= 0)
        {
            ref Particle newParticle = ref circularBuffer.buffer[index];

            newParticle.position = emitterPosition;
            newParticle.alive = true;
            newParticle.lifeTime = 0.0f;
            newParticle.type = type;
            float speed = (float)(Random.Shared.Next()%10)/5.0f;
            switch (type)
            {
                case ParticleType.WATER:
                    newParticle.radius = 5.0f;
                    newParticle.color = Color.Blue;
                    break;
                case ParticleType.SMOKE:
                    newParticle.radius = 7.0f;
                    newParticle.color = Color.Gray;
                    break;
                case ParticleType.FIRE:
                    newParticle.radius = 10.0f;
                    newParticle.color = Color.Yellow;
                    speed /= 10.0f;
                    break;
                default: break;
            }

            float direction = (float)(Random.Shared.Next()%360);
            newParticle.velocity = new Vector2(speed*MathF.Cos(direction*DEG2RAD), speed*MathF.Sin(direction*DEG2RAD));
        }
    }

    // The index of the particle added at the head, or -1 where the buffer is full.
    private static int AddToCircularBuffer(CircularBuffer circularBuffer)
    {
        int particle = -1;

        if (((circularBuffer.head + 1)%MAX_PARTICLES) != circularBuffer.tail)
        {
            particle = circularBuffer.head;
            circularBuffer.head = (circularBuffer.head + 1)%MAX_PARTICLES;
        }

        return particle;
    }

    private static void UpdateParticles(CircularBuffer circularBuffer, int screenWidth, int screenHeight)
    {
        for (int i = circularBuffer.tail; i != circularBuffer.head; i = (i + 1)%MAX_PARTICLES)
        {
            ref Particle particle = ref circularBuffer.buffer[i];

            particle.lifeTime += 1.0f/60.0f;

            switch (particle.type)
            {
                case ParticleType.WATER:
                    particle.position.X += particle.velocity.X;
                    particle.velocity.Y += 0.2f;
                    particle.position.Y += particle.velocity.Y;
                    break;
                case ParticleType.SMOKE:
                    particle.position.X += particle.velocity.X;
                    particle.velocity.Y -= 0.05f;
                    particle.position.Y += particle.velocity.Y;
                    particle.radius += 0.5f;
                    particle.color = particle.color with { A = (byte)(particle.color.A - 4) };

                    if (particle.color.A < 4) particle.alive = false;
                    break;
                case ParticleType.FIRE:
                    particle.position.X += particle.velocity.X + MathF.Cos(particle.lifeTime*215.0f);
                    particle.velocity.Y -= 0.05f;
                    particle.position.Y += particle.velocity.Y;
                    particle.radius -= 0.15f;
                    // A byte as C's unsigned char, which wraps below zero.
                    particle.color = particle.color with { G = unchecked((byte)(particle.color.G - 3)) };

                    if (particle.radius <= 0.02f) particle.alive = false;
                    break;
                default: break;
            }

            Vector2 center = particle.position;
            float radius = particle.radius;

            if ((center.X < -radius) || (center.X > (screenWidth + radius)) ||
                (center.Y < -radius) || (center.Y > (screenHeight + radius)))
            {
                particle.alive = false;
            }
        }
    }

    private static void UpdateCircularBuffer(CircularBuffer circularBuffer)
    {
        while ((circularBuffer.tail != circularBuffer.head) && !circularBuffer.buffer[circularBuffer.tail].alive)
        {
            circularBuffer.tail = (circularBuffer.tail + 1)%MAX_PARTICLES;
        }
    }

    private static void DrawParticles(CircularBuffer circularBuffer)
    {
        for (int i = circularBuffer.tail; i != circularBuffer.head; i = (i + 1)%MAX_PARTICLES)
        {
            if (circularBuffer.buffer[i].alive)
            {
                DrawCircleV(circularBuffer.buffer[i].position,
                            circularBuffer.buffer[i].radius,
                            circularBuffer.buffer[i].color);
            }
        }
    }
}
