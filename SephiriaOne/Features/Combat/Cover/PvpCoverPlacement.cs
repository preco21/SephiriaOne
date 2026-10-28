using System;
using System.Collections.Generic;

namespace SephiriaOne
{
    // Pure bounded placement policy. The Unity adapter checks a clear 3x3 bypass
    // square, not just the compact prop footprint: cover cannot seal a narrow route.
    internal sealed class PvpCoverPlacement
    {
        internal readonly struct Room
        {
            internal readonly float Left, Bottom, Right, Top;
            internal Room(float left, float bottom, float right, float top)
            { Left = left; Bottom = bottom; Right = right; Top = top; }
        }
        internal const int MaximumAttempts = 1024;
        private readonly List<Room> rooms;
        private readonly List<(float X, float Y)> accepted = new List<(float, float)>();
        private readonly Random random;
        private int nextRoom, attempts;
        internal int Attempts => attempts;
        internal int RoomCount => rooms.Count;
        internal bool Exhausted => rooms.Count == 0 || attempts >= MaximumAttempts;
        internal PvpCoverPlacement(List<Room> rooms, int seed)
        {
            this.rooms = new List<Room>(rooms); random = new Random(seed);
            for (int i = this.rooms.Count - 1; i > 0; i--)
            { int j = random.Next(i + 1); var swap = this.rooms[i]; this.rooms[i] = this.rooms[j]; this.rooms[j] = swap; }
        }
        // One candidate per call; the caller separately bounds search and spawns.
        internal bool TryNext(Func<float, float, bool> safe, out float x, out float y)
        {
            x = y = 0;
            if (Exhausted) return false;
            attempts++;
            var room = rooms[nextRoom++ % rooms.Count];
            if (room.Right - room.Left < 6 || room.Top - room.Bottom < 6) return false;
            x = room.Left + 2.5f + random.Next((int)(room.Right - room.Left) - 4);
            y = room.Bottom + 2.5f + random.Next((int)(room.Top - room.Bottom) - 4);
            foreach (var previous in accepted)
                if (Math.Abs(previous.X - x) < 3 && Math.Abs(previous.Y - y) < 3) return false;
            if (!safe(x, y)) return false;
            accepted.Add((x, y)); return true;
        }
    }
}
