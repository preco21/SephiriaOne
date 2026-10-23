using SephiriaOne;
internal static class ElementalSettingsTests
{
 internal static void Run(Action<bool,string> check)
 {
  var settings = default(FriendlyFireSettings);
  check(settings.Elements.Enabled && !settings.Elements.HasChanges, "Element limits default on without preset changes");
  check(default(PvpStatLimit).Limit(100) == 87.5f, "Shared default curve");
  check(default(PvpStatLimit).Equals(new PvpStatLimit(50,100)), "Shared explicit default equality");
  check(default(PvpStatLimit).Limit(-20) == -20, "Shared curve preserves penalties");
  check(FriendlyFireCommand.Parse("/one friendlyfire elements ice limits 25.5 75.5", out var command) == FriendlyFireParseResult.Valid, "Element command parses");
  settings = settings.Apply(command);
  check(settings.Elements.GetLimit(PvpElement.Ice).Soft == 25.5m && settings.Elements.GetLimit(PvpElement.Flame).Soft == 50, "Independent atomic element pairs");
  check(settings.WithEnabled(true).Elements.Equals(settings.Elements), "Live overlay copies elements");
  check(FriendlyFireCommand.Parse("/one friendlyfire elements ice limits 80 70", out _) == FriendlyFireParseResult.Invalid, "Inverted pair rejected");
  check(FriendlyFireCommand.Parse("/one friendlyfire elements ice reset", out command) == FriendlyFireParseResult.Valid && !settings.Apply(command).Elements.HasChanges, "Per-element reset");
 }
}
