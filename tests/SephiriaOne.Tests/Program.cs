using SephiriaOne;

const string BlueAlice = "<color=#0000FF>Alice</color>";
const string BlueBob = "<color=#0000FF>Bob</color>";
int checks = 0;

void Equal(string? expected, string? actual, string scenario)
{
    if (expected != actual)
    {
        throw new Exception($"{scenario}: expected '{expected ?? "<no request>"}', got '{actual ?? "<no request>"}'.");
    }

    checks++;
}

var state = new NetworkNameState();
Equal(null, state.Next("Alice", "Alice", false), "Single player does not publish formatting");
Equal(BlueAlice, state.Next("Alice", "Alice", true), "Joining multiplayer publishes blue");
Equal(null, state.Next("Alice", "Alice", true), "Waiting for server acknowledgment does not spam commands");
Equal(null, state.Next(BlueAlice, "Alice", true), "Server acknowledgment needs no new command");
Equal(BlueAlice, state.Next("Alice", "Alice", true), "A later server reset republishes formatting");
Equal("Alice", state.Next("Alice", "Alice", false), "Leaving before acknowledgment queues a compensating plain name");
Equal(null, state.Next(BlueAlice, "Alice", false), "Late colored acknowledgment does not duplicate pending restoration");
Equal(null, state.Next("Alice", "Alice", false), "Plain acknowledgment completes restoration");
Equal(BlueAlice, state.Next("Alice", "Alice", true), "Rejoining publishes again");
Equal(BlueBob, state.Next("Alice", "Bob", true), "A new profile name supersedes an in-flight request");
Equal(null, state.Next(BlueAlice, "Bob", true), "Old acknowledgment cannot roll back the new name");
Equal(null, state.Next(BlueBob, "Bob", true), "Latest acknowledgment completes the new name");
Equal("Bob", state.Next(BlueBob, "Bob", false), "Leaving restores the latest plain name");
state.Reset();
Equal(BlueBob, state.Next("Bob", "Bob", true), "New avatar resets pending requests");
Equal("<color=#0000FF>프레코 🐇</color>", NetworkNameState.Blue("프레코 🐇"), "Unicode name is preserved");
Equal(BlueAlice, NetworkNameState.Blue(BlueAlice), "Our color wrapper is not nested");
Equal("Alice", NetworkNameState.Plain(BlueAlice), "Our wrapper is removable");
Equal("Alice", NetworkNameState.Plain("<color=#0000FF>" + BlueAlice + "</color>"), "Repeated own wrappers normalize");
Equal("<b>Alice</b>", NetworkNameState.Plain("<b>Alice</b>"), "Unrelated name formatting is preserved");
Equal(null, state.Next("", "Bob", true), "Uninitialized network name waits for game setup");
Equal(null, state.Next("Bob", "", true), "Missing profile name does not rename the player");

Console.WriteLine($"Passed {checks} multiplayer name synchronization checks.");
Console.WriteLine($"Passed {FountainCommandTests.Run()} Fountain command checks.");
Console.WriteLine($"Passed {FountainResetTests.Run()} Fountain reset checks.");
Console.WriteLine($"Passed {ChoiceCommandTests.Run()} candidate command checks.");
Console.WriteLine($"Passed {ChoiceTranspilerTests.Run()} candidate generation guard checks.");
if (args.Length == 2) GameChoiceCompatibilityTests.Run(args[0], args[1]);
else if (args.Length != 0) throw new ArgumentException("Optional arguments: <game Managed directory> <built addon DLL>");
