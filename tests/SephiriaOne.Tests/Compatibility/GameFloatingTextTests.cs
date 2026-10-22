using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

internal static class GameFloatingTextTests
{
    internal static void Run(Assembly game, Assembly addon)
    {
        void Require(bool value, string why) { if (!value) throw new Exception("Floating text contract: " + why); }
        Type? adapter = addon.GetType("SephiriaOne.NativePlayerFloatingText");
        Require(adapter != null, "optional native floating-text adapter exists");
        Type unit = game.GetType("UnitAvatar", true)!;
        List<CodeInstruction> Code(MethodBase method) => PatchProcessor.GetOriginalInstructions(method).ToList();
        var sender = AccessTools.DeclaredMethod(unit, "RpcShowDamageParticle");
        var receiver = AccessTools.DeclaredMethod(unit, "InvokeUserCode_RpcShowDamageParticle__Vector2__String__Color__Int32__Boolean__UnitAvatar__UnitAvatar");
        var body = AccessTools.DeclaredMethod(unit, "UserCode_RpcShowDamageParticle__Vector2__String__Color__Int32__Boolean__UnitAvatar__UnitAvatar");
        Require((bool)AccessTools.DeclaredMethod(adapter, "Validate").Invoke(null, null)!,
            "native floating-text transport\n" + string.Join("\n", Code(sender).Concat(Code(receiver)).Concat(Code(body))));
        var broadcast = Delegate.CreateDelegate(AccessTools.Field(adapter, "send").FieldType, sender);
        Require(broadcast.Target == null && broadcast.Method == sender, "protected native sender binds as a cached open delegate without execution");
        foreach (string mutation in new[] { "name", "hash", "channel", "owner", "payload", "reader", "gameplay", "log", "write", "style" })
        {
            var send = Code(sender); var read = Code(receiver); var visual = Code(body);
            int dispatch = send.FindIndex(i => i.operand is MethodInfo m && m.Name == "SendRPCInternal");
            switch (mutation)
            {
                case "name": send.First(i => i.opcode == OpCodes.Ldstr).operand = "changed"; break;
                case "hash": send.First(i => i.LoadsConstant(-1687250273)).operand = 1; break;
                case "channel": send[dispatch - 2].opcode = OpCodes.Ldc_I4_0; break;
                case "owner": send[dispatch - 1].opcode = OpCodes.Ldc_I4_0; break;
                case "payload": send.RemoveAt(send.FindIndex(i => i.operand is MethodInfo m && m.Name == "WriteString")); break;
                case "reader": read.RemoveAt(read.FindIndex(i => i.operand is MethodInfo m && m.Name == "ReadVector2")); break;
                case "gameplay": visual.Add(new CodeInstruction(OpCodes.Call, AccessTools.Method(unit, "ForceDie"))); break;
                case "log": visual.Add(new CodeInstruction(OpCodes.Call, AccessTools.Method(game.GetType("GameLogWriter"), "WriteLog"))); break;
                case "write": visual.Add(new CodeInstruction(OpCodes.Stsfld)); break;
                case "style": visual.First(i => i.operand is MethodInfo m && m.Name == "Spawn").operand = AccessTools.Method(unit, "ForceDie"); break;
            }
            Require(!(bool)AccessTools.DeclaredMethod(adapter, "ValidateCode").Invoke(null, new object[] { send, read, visual })!,
                "changed " + mutation + " fails closed");
        }
        Require(Code(unit.TypeInitializer!).Any(i => Equals(i.operand, receiver)), "stock clients register the cosmetic receiver");
        Require(!Code(body).Any(i => i.operand is MethodInfo m && new[] { "WriteLog", "ApplyDamage", "SetHp", "Die" }.Contains(m.Name)),
            "native receiver has no chat/damage/death side effects");
        var show = Code(AccessTools.DeclaredMethod(adapter, "Show"));
        int sanitize = show.FindIndex(i => i.operand is MethodInfo m && m.Name == "Sanitize");
        int emphasis = show.FindIndex(i => i.opcode == OpCodes.Ldstr && Equals(i.operand, "<b><size=125%>"));
        Require(sanitize >= 0 && emphasis > sanitize && show.Any(i => i.opcode == OpCodes.Ldstr && Equals(i.operand, "</size></b>")),
            "trusted balanced emphasis tags are added after plain-text sanitization");
        var setText = Code(AccessTools.DeclaredMethod(game.GetType("UI_DamageParticle", true), "SetDamage"));
        Require(setText.Where(i => i.operand is MethodInfo).Select(i => ((MethodInfo)i.operand).Name)
            .SequenceEqual(new[] { "set_text", "set_color" }), "native particle forwards rich text directly to TMP without gameplay or chat");
        int invoke = show.FindLastIndex(i => i.operand is MethodInfo m && m.Name == "Invoke");
        Require(invoke >= 4 && show[invoke - 4].LoadsConstant(0) && show[invoke - 3].LoadsConstant(0) &&
            show[invoke - 2].opcode == OpCodes.Ldnull && show[invoke - 1].opcode == OpCodes.Ldnull,
            "payload selects normal colored text, no damage-visibility filter, no victim or attacker metadata");
        Require(!show.Any(i => i.operand is MethodInfo m && new[] { "Spawn", "ShowDamageParticle", "ApplyDamage", "Die", "WriteLog", "Chat" }.Contains(m.Name)),
            "adapter uses only one native broadcast, never local duplication or gameplay");
        Require(show.Any(i => i.operand is MethodInfo m && m.Name == "Sanitize") &&
            !show.Any(i => i.operand is FieldInfo f && f.Name == "IsDead"),
            "plain bounded messages work on dead avatars");
        var runtime = addon.GetType("SephiriaOne.CombatMatchRuntime", true)!;
        var notice = Code(AccessTools.DeclaredMethod(runtime, "BroadcastCountdown"));
        int chat = notice.FindIndex(i => i.operand is MethodInfo m && m.Name == "Broadcast");
        int floating = notice.FindIndex(i => i.operand is MethodInfo m && m.DeclaringType == adapter && m.Name == "Show");
        Require(chat >= 0 && floating > chat && notice.Skip(chat + 1).Take(floating - chat - 1)
            .Any(i => i.operand is MethodInfo m && m.Name == "IsCurrent"),
            "chat stays first and participant lifetime is rechecked before optional floating output");
        foreach (string key in new[] { "Respawn in: {0}s", "Bomb: {0}s" })
        {
            foreach (string lang in new[] { "en", "ko" })
            {
                using var stream = addon.GetManifestResourceStream("SephiriaOne.Localization." + lang + ".json")!;
                using var reader = new StreamReader(stream);
                using var json = System.Text.Json.JsonDocument.Parse(reader.ReadToEnd());
                Require(json.RootElement.TryGetProperty(key, out var value) && value.GetString()!.Contains("{0}"),
                    lang + " countdown preserves the numeric placeholder");
            }
        }
        Console.WriteLine("Verified stock floating-text payload, cosmetic-only receiver, owner inclusion, chat retention, EN/KO keys and fail-closed mutations (not live rendering).");
    }
}
