using SephiriaOne;

string GradientAlice = NameGradient.Format("Alice");
string GradientBob = NameGradient.Format("Bob");
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
Equal(GradientAlice, state.Next("Alice", "Alice", true), "Joining multiplayer publishes gradient");
Equal(null, state.Next("Alice", "Alice", true), "Waiting for server acknowledgment does not spam commands");
Equal(null, state.Next(GradientAlice, "Alice", true), "Server acknowledgment needs no new command");
Equal(GradientAlice, state.Next("Alice", "Alice", true), "A later server reset republishes formatting");
Equal("Alice", state.Next("Alice", "Alice", false), "Leaving before acknowledgment queues a compensating plain name");
Equal(null, state.Next(GradientAlice, "Alice", false), "Late colored acknowledgment does not duplicate pending restoration");
Equal(null, state.Next("Alice", "Alice", false), "Plain acknowledgment completes restoration");
Equal(GradientAlice, state.Next("Alice", "Alice", true), "Rejoining publishes again");
Equal(GradientBob, state.Next("Alice", "Bob", true), "A new profile name supersedes an in-flight request");
Equal(null, state.Next(GradientAlice, "Bob", true), "Old acknowledgment cannot roll back the new name");
Equal(null, state.Next(GradientBob, "Bob", true), "Latest acknowledgment completes the new name");
Equal("Bob", state.Next(GradientBob, "Bob", false), "Leaving restores the latest plain name");
state.Reset();
Equal(GradientBob, state.Next("Bob", "Bob", true), "New avatar resets pending requests");
Equal(NameGradient.Format("프레코 🐇"), NetworkNameState.Gradient("프레코 🐇"), "Unicode name is preserved");
Equal(GradientAlice, NetworkNameState.Gradient(GradientAlice), "Our color wrapper is not nested");
Equal("Alice", NetworkNameState.Plain(GradientAlice), "Our wrapper is removable");
Equal("Alice", NetworkNameState.Plain("<color=#0000FF>" + GradientAlice + "</color>"), "Repeated own wrappers normalize");
Equal("<b>Alice</b>", NetworkNameState.Plain("<b>Alice</b>"), "Unrelated name formatting is preserved");
Equal(null, state.Next("", "Bob", true), "Uninitialized network name waits for game setup");
Equal(null, state.Next("Bob", "", true), "Missing profile name does not rename the player");

Console.WriteLine($"Passed {checks} multiplayer name synchronization checks.");
Console.WriteLine($"Passed {PanelDraftTests.Run()} panel draft isolation checks.");
Console.WriteLine($"Passed {PanelControlLifetimeTests.Run()} panel control lifetime checks.");
Console.WriteLine($"Passed {ReconciliationTests.Run()} shared reconciliation checks.");
Console.WriteLine($"Passed {StateWriteBatchTests.Run()} shared write-batch checks.");
Console.WriteLine($"Passed {NetworkNameRetryTests.Run()} name retry checks.");
Console.WriteLine($"Passed {NameGradientTests.Run()} name gradient checks.");
Console.WriteLine($"Passed {FountainCommandTests.Run()} Fountain command checks.");
Console.WriteLine($"Passed {FountainResetTests.Run()} Fountain reset checks.");
Console.WriteLine($"Passed {StatCommandTests.Run()} character stat command checks.");
Console.WriteLine($"Passed {SessionPolicyTests.Run()} session inheritance checks.");
Console.WriteLine($"Passed {RelativeStatPolicyTests.Run()} relative stat consistency checks.");
Console.WriteLine($"Passed {PenaltyMultiplierTests.Run()} native penalty multiplier checks.");
Console.WriteLine($"Passed {MultiplierCommandTests.Run()} native-baseline multiplier checks.");
Console.WriteLine($"Passed {ResourcePolicyTests.Run()} resource policy checks.");
Console.WriteLine($"Passed {RabbitPotionSettingsTests.Run()} rabbit potion settings checks.");
Console.WriteLine($"Passed {MerchantSettingsTests.Run()} merchant settings checks.");
Console.WriteLine($"Passed {StartingResourceTests.Run()} starting resource checks.");
Console.WriteLine($"Passed {PresetTests.Run()} preset parser/storage checks.");
Console.WriteLine($"Passed {ItemRestrictionTests.Run()} item restriction checks.");
Console.WriteLine($"Passed {FriendlyFirePolicyTests.Run()} friendly-fire policy/preset checks.");
Console.WriteLine($"Passed {ChoiceCommandTests.Run()} candidate command checks.");
Console.WriteLine($"Passed {ChoiceTranspilerTests.Run()} candidate generation guard checks.");
Console.WriteLine($"Passed {LocalizationCatalogTests.Run()} bundled catalog checks.");
string localizationFolder = Path.Combine(Path.GetTempPath(), "SephiriaOne-Features-Language-" + Guid.NewGuid().ToString("N"));
L.Initialize(localizationFolder, warning => throw new Exception(warning));
void SelectLanguage(string language)
{
    if (!L.TrySetLanguage(language, out string error)) throw new Exception(error);
}
Console.WriteLine($"Passed {LocalizationFeatureTests.Run(SelectLanguage)} localized feature checks.");
L.Shutdown();
if (args.Length == 2) GameChoiceCompatibilityTests.Run(args[0], args[1]);
else if (args.Length != 0) throw new ArgumentException("Optional arguments: <game Managed directory> <built addon DLL>");
