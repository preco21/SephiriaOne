using SephiriaOne;

int checks = 0;
void Check(bool ok, string message) { if (!ok) throw new Exception(message); checks++; }
var bomb = new PlayTagBomb(3, 0, 30, 100);
Check(bomb.Carrier == 0 && bomb.PreviousCarrier == -1 && bomb.Fuse == 30 && !bomb.AwaitingRespawn && !bomb.Resolved,
    "Initial carrier starts armed without an invented passer");
Check(bomb.Remaining(100) == 30 && bomb.Remaining(129.01) == 1 && !bomb.IsDue(129.999) && bomb.IsDue(130),
    "Initial deadline uses ceiling bubbles and exact expiry");
Check(!bomb.TryPass(1, 2, 101) && !bomb.TryPass(0, 0, 101) && !bomb.TryPass(0, 3, 101) &&
    !bomb.TryPass(0, -1, 101) && !bomb.TryPass(0, 1, double.NaN) && bomb.Fuse == 30 && bomb.Carrier == 0,
    "Invalid/noncarrier/self transfers never mutate the bomb");
Check(bomb.TryPass(0, 1, 101) && bomb.Fuse == 24 && bomb.Carrier == 1 && bomb.PreviousCarrier == 0 && bomb.AwaitingRespawn,
    "Confirmed carrier kill reduces fuse and assigns dead victim");
Check(!bomb.IsDue(1000) && bomb.Remaining(1000) == 24 && !bomb.TryPass(1, 2, 102),
    "Only the assigned victim's successful revival can arm the waiting fuse");
Check(!bomb.ArmAfterRespawn(0, 102) && !bomb.ArmAfterRespawn(1, double.PositiveInfinity) && bomb.AwaitingRespawn,
    "An unrelated/invalid revival does not arm it");
Check(bomb.ArmAfterRespawn(1, 102) && !bomb.AwaitingRespawn && bomb.Remaining(102) == 24 && bomb.IsDue(126),
    "Full new fuse begins at actual revival");
Check(!bomb.ArmAfterRespawn(1, 110) && bomb.IsDue(126), "Later deaths/revivals cannot restart an armed fuse");
Check(!bomb.TryPass(1, 2, 126) && bomb.Carrier == 1, "A kill at the deadline loses to expiry");
Check(bomb.ConsumeExplosion(126) && bomb.Resolved && !bomb.ConsumeExplosion(127) && !bomb.IsDue(127) &&
    !bomb.TryPass(1, 2, 127) && !bomb.ArmAfterRespawn(1, 127) && bomb.Remaining(127) == 0,
    "Consumed explosions cannot be repeated or undone by callbacks");

bomb = new PlayTagBomb(3, 0, 30, 0);
int holder = 0;
double now = 0;
foreach (int nextFuse in new[] { 24, 19, 15, 12, 9, 7, 5, 4, 3 })
{
    int next = (holder + 1) % 3;
    Check(bomb.TryPass(holder, next, now + 0.1) && bomb.Fuse == nextFuse && !bomb.IsDue(now + 0.1),
        "Each transfer floors 80 percent of the previous integer fuse");
    now += 1.1;
    Check(bomb.ArmAfterRespawn(next, now), "Each valid transfer can arm once");
    holder = next;
}

bomb = new PlayTagBomb(2, 0, 3, 0);
Check(bomb.TryPass(0, 1, 0.1) && !bomb.IsDue(0.1) && bomb.ArmAfterRespawn(1, 1.1), "Second player receives first minimum turn");
Check(bomb.TryPass(1, 0, 1.2) && !bomb.IsDue(1.2) && bomb.ArmAfterRespawn(0, 2.2), "Initial carrier receives second minimum turn");
Check(bomb.TryPass(0, 1, 2.3) && !bomb.IsDue(2.3) && bomb.ArmAfterRespawn(1, 3.3), "Second player receives second minimum turn");
Check(bomb.TryPass(1, 0, 3.4) && bomb.Carrier == 0 && bomb.PreviousCarrier == 1 && bomb.IsDue(3.4) &&
    !bomb.AwaitingRespawn && !bomb.ArmAfterRespawn(0, 4.4), "Third minimum receipt explodes immediately while victim remains dead");
Check(bomb.ConsumeExplosion(3.4), "Immediate third-receipt outcome is consumable once");

// Arbitrary switching cannot reset per-entrant counters or run forever at 3s.
var random = new Random(893);
for (int players = 2; players <= 16; players++)
    for (int trial = 0; trial < 50; trial++)
    {
        bomb = new PlayTagBomb(players, random.Next(players), 3, 0); now = 0;
        int transfers = 0;
        while (!bomb.IsDue(now))
        {
            int victim = (bomb.Carrier + 1 + random.Next(players - 1)) % players;
            now += 0.1;
            Check(bomb.TryPass(bomb.Carrier, victim, now), "Random legal pass accepted");
            transfers++;
            if (!bomb.IsDue(now)) { now += 1; Check(bomb.ArmAfterRespawn(victim, now), "Random recipient arms on recovery"); }
            Check(transfers <= 2 * players, "At most two timed minimum holdings per captured entrant");
        }
        Check(bomb.ConsumeExplosion(now), "Every random minimum-time sequence terminates");
    }

foreach (int seconds in new[] { 3, 4, 30, 3600 })
{
    bomb = new PlayTagBomb(2, 1, seconds, 10);
    Check(!bomb.ConsumeExplosion(10) && bomb.ConsumeExplosion(10 + seconds), "No-pass game always expires at configured deadline");
}
Console.WriteLine($"Passed {checks} Play Tag bomb policy checks.");
