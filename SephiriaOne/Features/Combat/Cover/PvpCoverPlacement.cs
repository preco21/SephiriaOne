using System;
using System.Collections.Generic;

namespace SephiriaOne
{
    // Bounded half-unit sampling; reserve the compact audited prop envelope.
    // Native geometry and player displacement are handled by the Unity adapter.
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
        internal void Release(float x, float y) => accepted.Remove((x, y));
        // One candidate per call; the caller separately bounds search and spawns.
        internal bool TryNext(Func<float, float, bool> safe, out float x, out float y)
        {
            x = y = 0;
            if (Exhausted) return false;
            attempts++;
            var room = rooms[nextRoom++ % rooms.Count];
            if (room.Right - room.Left < 2 || room.Top - room.Bottom < 2) return false;
            x = room.Left + 1f + random.Next((int)((room.Right - room.Left - 2) * 2) + 1) * .5f;
            y = room.Bottom + 1f + random.Next((int)((room.Top - room.Bottom - 2) * 2) + 1) * .5f;
            foreach (var previous in accepted)
                if (Math.Abs(previous.X - x) < 1.55f && Math.Abs(previous.Y - y) < 1.2f) return false;
            if (!safe(x, y)) return false;
            accepted.Add((x, y)); return true;
        }
    }
}
