using SephiriaOne;

int checks = 0, failures = 0;
void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
    checks++;
}
void Test(string name, Action body)
{
    try { body(); Console.WriteLine("PASS " + name); }
    catch (Exception error) { failures++; Console.WriteLine("FAIL " + name + ": " + error.Message); }
}
void Throws<T>(Action body, string message) where T : Exception
{
    try { body(); }
    catch (T) { checks++; return; }
    throw new Exception(message);
}
(int First, int Second) Next(TournamentSchedule schedule)
{
    Check(schedule.TryNext(out int first, out int second), "Expected another legal pairing");
    Check(first >= 0 && second > first, "Pair indices must be distinct and normalized");
    return (first, second);
}
void Resolve(TournamentSchedule schedule, (int First, int Second) pair, int loser = -1) =>
    schedule.Resolve(pair.First, pair.Second, loser);
TournamentSchedule Schedule(int count, int seed = 0) => new(count, new Random(seed).Next);

Test("Empty and single-entrant schedules finish without calling RNG", () =>
{
    foreach (int count in new[] { 0, 1 })
    {
        var schedule = new TournamentSchedule(count, _ => throw new Exception("No candidate should need RNG"));
        Check(!schedule.TryNext(out int first, out int second), "Fewer than two entrants cannot play");
        Check(first == -1 && second == -1, "No pairing must return sentinel indices");
    }
});

Test("A pending pair remains stable until its result arrives", () =>
{
    int randomCalls = 0;
    var schedule = new TournamentSchedule(4, maximum => { randomCalls++; return maximum - 1; });
    var first = Next(schedule);
    int calls = randomCalls;
    Check(Next(schedule) == first && randomCalls == calls, "Reading the pending pair must not consume RNG");
    Check(schedule.Matches(first.First) == 0 && schedule.Matches(first.Second) == 0, "Scheduling is not a completed match");
    Resolve(schedule, first);
    Check(schedule.Matches(first.First) == 1 && schedule.Matches(first.Second) == 1, "A result records one match per entrant");
});

Test("One defeat eliminates and awards only the surviving entrant a win", () =>
{
    foreach (bool firstLoses in new[] { false, true })
    {
        var schedule = Schedule(2);
        var pair = Next(schedule);
        int loser = firstLoses ? pair.First : pair.Second;
        int winner = firstLoses ? pair.Second : pair.First;
        Resolve(schedule, pair, loser);
        Check(schedule.IsEliminated(loser) && !schedule.IsEliminated(winner), "Only the loser must leave the bracket");
        Check(schedule.Wins(winner) == 1 && schedule.Wins(loser) == 0, "Only the winner receives a win");
        Check(!schedule.TryNext(out _, out _), "One remaining entrant ends the tournament");
    }
});

Test("Two-player draws retain both entrants but exhaust the unordered pair", () =>
{
    var schedule = Schedule(2);
    var pair = Next(schedule);
    Resolve(schedule, pair);
    Check(!schedule.IsEliminated(0) && !schedule.IsEliminated(1), "Draws eliminate neither entrant");
    Check(schedule.Wins(0) == 0 && schedule.Wins(1) == 0, "Draws award no wins");
    Check(Next(schedule) == pair, "A two-player draw permits a second attempt");
    schedule.Resolve(pair.Second, pair.First, -1);
    Check(!schedule.TryNext(out _, out _), "Two draws exhaust both orders of the same pair");
    Check(schedule.Matches(0) == 2 && schedule.Matches(1) == 2, "Exactly two matches were completed");
    Check(!schedule.IsEliminated(0) && !schedule.IsEliminated(1), "Pair exhaustion must not invent a loser");
});

Test("All fresh entrants play before any drawn pair retries", () =>
{
    for (int seed = 0; seed < 80; seed++)
    {
        var schedule = Schedule(6, seed);
        var seen = new HashSet<int>();
        for (int round = 0; round < 3; round++)
        {
            var pair = Next(schedule);
            Check(seen.Add(pair.First) && seen.Add(pair.Second), "A rematch cannot bypass unplayed entrants");
            Resolve(schedule, pair);
        }
        Check(seen.Count == 6, "Every entrant must get an opening match");
    }
});

Test("Odd brackets pair their remaining fresh entrant before earlier players retry", () =>
{
    for (int seed = 0; seed < 80; seed++)
    {
        var schedule = Schedule(5, seed);
        var seen = new HashSet<int>();
        for (int round = 0; round < 2; round++)
        {
            var pair = Next(schedule);
            Check(seen.Add(pair.First) && seen.Add(pair.Second), "Fresh pairs precede earlier entrants");
            Resolve(schedule, pair);
        }
        int fresh = Enumerable.Range(0, 5).Single(index => !seen.Contains(index));
        var next = Next(schedule);
        Check(next.First == fresh || next.Second == fresh, "The odd fresh entrant must not wait behind a rematch");
    }
});

Test("Unresolved entrants retry before previous winners return", () =>
{
    for (int seed = 0; seed < 80; seed++)
    {
        var schedule = Schedule(4, seed);
        var won = Next(schedule);
        Resolve(schedule, won, won.Second);
        var drawn = Next(schedule);
        Check(drawn.First != won.First && drawn.Second != won.First, "Fresh entrants precede the prior winner");
        Resolve(schedule, drawn);
        Check(Next(schedule) == drawn, "A winless rematch precedes a pairing against a previous winner");
        Resolve(schedule, drawn);
        var afterExhaustion = Next(schedule);
        Check(afterExhaustion.First == won.First || afterExhaustion.Second == won.First,
            "After the unresolved pair is exhausted, the winner can return");
    }
});

Test("A fresh odd entrant prefers a draw survivor over a previous winner", () =>
{
    var schedule = new TournamentSchedule(5, _ => 0);
    var won = Next(schedule);
    Resolve(schedule, won, won.Second);
    var drawn = Next(schedule);
    Resolve(schedule, drawn);
    var next = Next(schedule);
    Check(next.First == 4 || next.Second == 4, "Last fresh entrant must play");
    Check(next.First != won.First && next.Second != won.First, "Their opponent should still have no wins");
});

Test("Within the winless tier fewer matches take priority", () =>
{
    var schedule = new TournamentSchedule(3, _ => 0);
    Resolve(schedule, Next(schedule)); // 0/1 have played, 2 is fresh.
    Resolve(schedule, Next(schedule)); // 0/2 play; only 0 has two matches.
    var next = Next(schedule);
    Check(schedule.Matches(next.First) == 1 && schedule.Matches(next.Second) == 1,
        "The two least-used winless entrants must play together");
});

Test("An untried pair precedes a rematch at equal participation priority", () =>
{
    for (int seed = 0; seed < 80; seed++)
    {
        var schedule = Schedule(4, seed);
        var one = Next(schedule);
        Resolve(schedule, one);
        var two = Next(schedule);
        Resolve(schedule, two);
        var next = Next(schedule);
        Check(next != one && next != two, "Equally rested entrants should meet a new opponent before retrying a draw");
    }
});

Test("Equal-priority opening pairs can all be selected by the injected RNG", () =>
{
    var selected = new HashSet<(int, int)>();
    for (int seed = 0; seed < 240; seed++) selected.Add(Next(Schedule(4, seed)));
    Check(selected.Count == 6, "Every unordered opening pair must be reachable, not just neighboring indices");
    var low = Next(new TournamentSchedule(4, _ => 0));
    var high = Next(new TournamentSchedule(4, maximum => maximum - 1));
    Check(low != high, "Tied pair selection must actually use injected randomness");
});

Test("Withdrawal excludes an entrant and safely clears their pending pair", () =>
{
    var schedule = new TournamentSchedule(4, _ => 0);
    var pair = Next(schedule);
    schedule.Withdraw(pair.First);
    schedule.Withdraw(pair.First);
    Check(schedule.IsEliminated(pair.First), "Withdrawals permanently leave the bracket");
    Check(schedule.Matches(pair.Second) == 0 && schedule.Wins(pair.Second) == 0,
        "Canceling an uncompleted match gives the remaining entrant no result");
    var next = Next(schedule);
    Check(next.First != pair.First && next.Second != pair.First, "A withdrawn entrant cannot be scheduled again");
    int other = Enumerable.Range(0, 4).Single(index => index != pair.First && index != next.First && index != next.Second);
    schedule.Withdraw(other);
    Check(Next(schedule) == next, "An unrelated withdrawal cannot reroll the current pair");
    Resolve(schedule, next, next.Second);
    Check(!schedule.TryNext(out _, out _), "Withdrawals and losses leave one survivor");
});

Test("Every all-draw bracket finishes at the exact two-attempt bound", () =>
{
    for (int count = 2; count <= 11; count++)
    for (int seed = 0; seed < 15; seed++)
    {
        var schedule = Schedule(count, seed);
        var attempts = new Dictionary<(int, int), int>();
        int rounds = 0;
        while (schedule.TryNext(out int first, out int second))
        {
            Check(++rounds <= count * (count - 1), "All draws must terminate within twice the unordered pair count");
            attempts.TryGetValue((first, second), out int previous);
            attempts[(first, second)] = previous + 1;
            Check(previous < 2, "No pair may play a third time");
            schedule.Resolve(first, second, -1);
        }
        Check(rounds == count * (count - 1), "The schedule ends only when every remaining pair is exhausted");
        Check(attempts.Values.All(attempt => attempt == 2), "Every possible pair receives exactly two attempts");
        Check(Enumerable.Range(0, count).All(index => !schedule.IsEliminated(index)), "No draw survivor gets eliminated");
        Check(Enumerable.Range(0, count).All(index => schedule.Matches(index) == 2 * (count - 1)),
            "All entrants receive equal total participation after all draws");
    }
});

Test("Random wins and draws never reuse losers or exceed finite pairing capacity", () =>
{
    for (int count = 2; count <= 11; count++)
    for (int seed = 0; seed < 40; seed++)
    {
        var outcomes = new Random(seed + 1000);
        var schedule = Schedule(count, seed);
        var eliminated = new HashSet<int>();
        var attempts = new Dictionary<(int, int), int>();
        int rounds = 0, wins = 0;
        while (schedule.TryNext(out int first, out int second))
        {
            Check(++rounds <= count * (count - 1), "A mixed tournament must terminate within the draw-only bound");
            Check(!eliminated.Contains(first) && !eliminated.Contains(second), "No eliminated entrant may return");
            attempts.TryGetValue((first, second), out int previous);
            attempts[(first, second)] = previous + 1;
            Check(previous < 2, "Random outcomes cannot permit a third pair attempt");
            int outcome = outcomes.Next(3);
            int loser = outcome == 0 ? -1 : outcome == 1 ? first : second;
            schedule.Resolve(first, second, loser);
            if (loser >= 0) { eliminated.Add(loser); wins++; }
        }
        Check(Enumerable.Range(0, count).Sum(schedule.Wins) == wins, "Each defeat creates exactly one winner");
        Check(Enumerable.Range(0, count).Sum(schedule.Matches) == rounds * 2, "Each round counts both entrants once");
        var survivors = Enumerable.Range(0, count).Where(index => !schedule.IsEliminated(index)).ToArray();
        foreach (int first in survivors)
        foreach (int second in survivors.Where(index => index > first))
            Check(attempts.TryGetValue((first, second), out int value) && value == 2,
                "Stopping with several survivors requires every surviving pair to be exhausted");
    }
});

Test("Invalid results cannot mutate pending state", () =>
{
    Throws<ArgumentOutOfRangeException>(() => Schedule(-1), "Negative roster size must be rejected");
    Throws<ArgumentNullException>(() => new TournamentSchedule(2, null), "An RNG must be supplied");
    var schedule = Schedule(4);
    Throws<InvalidOperationException>(() => schedule.Resolve(0, 1, -1), "A result requires a pending pairing");
    var pair = Next(schedule);
    int outside = Enumerable.Range(0, 4).First(index => index != pair.First && index != pair.Second);
    Throws<ArgumentException>(() => schedule.Resolve(pair.First, pair.Second, outside), "A loser must belong to the pending pair");
    Throws<InvalidOperationException>(() => schedule.Resolve(pair.First, outside, -1), "An unrelated pair cannot resolve the match");
    Throws<ArgumentOutOfRangeException>(() => schedule.Withdraw(4), "Withdrawals require a captured roster index");
    Check(Next(schedule) == pair && schedule.Matches(pair.First) == 0, "Invalid calls must leave the pending result untouched");
    Resolve(schedule, pair);
    Throws<InvalidOperationException>(() => Resolve(schedule, pair), "A result cannot be applied twice");
    Check(schedule.Matches(pair.First) == 1, "Duplicate results cannot count twice");
});

Console.WriteLine($"Tournament scheduler: {checks} checks; {failures} failures.");
return failures == 0 ? 0 : 1;
