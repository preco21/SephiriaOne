using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

internal static class GamePlayTagExplosionTests
{
    internal static void Run(Assembly game, Assembly addon)
    {
        void Require(bool value, string why) { if (!value) throw new Exception("Play Tag Leaf Explosion contract: " + why); }
        Type? adapter = addon.GetType("SephiriaOne.PlayTagExplosion");
        Require(adapter != null, "exact main-explosion adapter is implemented");
        List<CodeInstruction> Code(Type type, string name) => PatchProcessor.GetOriginalInstructions(AccessTools.DeclaredMethod(type, name)).ToList();
        IEnumerable<MethodInfo> Calls(IEnumerable<CodeInstruction> code) => code.Select(i => i.operand).OfType<MethodInfo>();
        var bullet = game.GetType("Bullet", true)!;
        var mirror = bullet.BaseType!.BaseType!.Assembly;
        var server = mirror.GetType("Mirror.NetworkServer", true)!;
        var client = mirror.GetType("Mirror.NetworkClient", true)!;
        Require((bool)AccessTools.DeclaredMethod(adapter, "ValidateNativeCode").Invoke(null, null)!, "audited game and Mirror fingerprints");
        var hash = AccessTools.DeclaredMethod(adapter, "MatchesAssembly");
        Require(!(bool)hash.Invoke(null, new object[] { game, new string('0', 64) })!, "changed assembly fails closed");

        var update = Code(bullet, "Update");
        int gate = update.FindIndex(i => i.operand is MethodInfo m && m.Name == "get_isServer");
        Require(gate >= 0 && update[gate + 1].Branches(out Label? target) && target.HasValue, "server-only collision branch exists");
        Label collisionEnd = (Label)update[gate + 1].operand;
        int end = update.FindIndex(i => i.labels.Contains(collisionEnd));
        Require(end > gate && update.Select((i, n) => (i, n)).Where(x => x.i.operand is MethodInfo m &&
            (m.Name == "AttackOnServer" || m.Name == "Move" || m.Name == "Overlap" || m.Name == "ColliderCastNonAlloc"))
            .All(x => x.n > gate && x.n < end), "all bullet collision, damage and movement queries are server gated");
        Require(!Calls(update.Skip(end)).Any(m => m.Name is "ApplyDamage" or "AttackOnServer" or "OnSpawn"), "guest tail contains no combat or spawn callback");
        var movement = game.GetType("BulletMoveModule", true)!;
        Require(Code(movement, "ClientMove").All(i => i.opcode == OpCodes.Nop || i.opcode == OpCodes.Ret), "stock blink movement has an empty client operation");
        Require(game.GetType("BulletMoveModule_BlinkToTargetPosition", true)!.GetMethod("ClientMove")!.DeclaringType == movement,
            "blink module inherits the empty client operation");
        foreach (string name in new[] { "MeleeCollision", "BulletBreaker" })
        {
            var code = Code(game.GetType(name, true)!, "Update");
            Require(Calls(code).First().DeclaringType == server && Calls(code).First().Name == "get_active", name + " starts with the native server gate");
        }
        var rigidbody = game.GetType("TopdownRigidbody", true)!;
        var masks = Calls(Code(rigidbody, "Update")).Where(m => m.Name.EndsWith("LayerMask")).Select(m => m.Name).Distinct().Order().ToArray();
        Require(masks.SequenceEqual(new[] { "get_CliffLayerMask", "get_PitLayerMask", "get_TileLayerMask" }),
            "guest player movement filters terrain rather than Default-layer cosmetic colliders");
        Require(!Calls(Code(client, "SpawnPrefab")).Any(m => m.Name is "OnSpawn" or "Initialize"), "stock network spawn does not invoke weapon/pool initialization or camera shake");
        Require(Calls(Code(client, "SpawnPrefab")).Any(m => m.Name == "Instantiate"), "stock network registration instantiates the selected native prefab");
        var manager = mirror.GetType("Mirror.NetworkManager", true)!;
        Require(Calls(Code(manager, "FinishStartHost")).Any(m => m.Name == "RegisterClientMessages") &&
            Calls(Code(manager, "RegisterClientMessages")).Any(m => m.Name == "RegisterPrefab"), "host startup registers the serialized stock prefab collection");
        var serialized = Code(bullet, "SerializeSyncVars").Select(i => i.operand).OfType<FieldInfo>().Where(f => f.DeclaringType == bullet).Select(f => f.Name).Distinct().Order().ToArray();
        Require(serialized.SequenceEqual(new[] { "canBeTransparentOnMultiplayer", "height" }) &&
            Calls(Code(bullet, "SerializeSyncVars")).Any(m => m.Name == "get_NetworkOwner"), "guest collision safety does not pretend host collider flags are replicated");

        var show = Code(adapter!, "Show");
        int isolate = show.FindIndex(i => i.operand is MethodInfo m && m.Name == "CreateCosmeticInstance");
        int expose = show.FindIndex(i => i.operand is MethodInfo m && m.DeclaringType == server && m.Name == "Spawn");
        Require(isolate >= 0 && expose > isolate, "server isolation completes before network exposure");
        var create = Code(adapter!, "CreateCosmeticInstance");
        Require(Calls(create).Any(m => m.Name == "set_enabled") && Calls(create).Any(m => m.Name == "ClearTargetFaction") &&
            Calls(create).Any(m => m.Name == "SetOwner") && Calls(create).Any(m => m.Name == "SetDamage"), "owned instance disables physical/combat paths and clears ownership, factions and damage");
        var adapterCode = adapter!.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            .SelectMany(m => PatchProcessor.GetOriginalInstructions(m)).ToList();
        Require(!Calls(adapterCode).Any(m => m.Name is "OnSpawn" or "OnSpawnFinalized" or "ApplyDamage" or "AttackOnServer" or "BroadcastAttackBullet" or "SubAttackButtonUp" or "PlayOneShot" or "CreateInstance"),
            "adapter cannot spend leaves, fire hits, trigger spawn shake or duplicate embedded sound");
        Require(Calls(Code(adapter, "DestroyOwned")).Any(m => m.DeclaringType == server && m.Name == "Destroy"), "explicit cleanup sends native network destruction");
        Require(Calls(Code(adapter, "Tick")).Any(m => m.Name == "DestroyOwned") && Calls(Code(adapter, "Reset")).Any(m => m.Name == "DestroyOwned"),
            "natural fallback and interruptions share deterministic owned-instance cleanup");
        Require(!Calls(Code(adapter, "ValidatePrefab")).Any(m => m.Name == "get_netIdentity"), "loaded prefabs are checked without Awake-only NetworkBehaviour caches");
        foreach (string name in new[] { "MoveModule", "DestroyModule" }) Require(bullet.GetField(name) != null, name + " is a serialized prefab field");
        Require(game.GetType("Animator2D_Basic", true)!.GetField("currentSet") != null, "animation set is a serialized prefab field");
        ValidateEvents(game, adapter);
        Console.WriteLine("Verified exact Leaf Explosion adapter isolation, stock spawn/client collision boundaries, native-code fingerprints and cleanup contracts (not live guest rendering/audio).");
    }

    private static void ValidateEvents(Assembly game, Type adapter)
    {
        void Require(bool value, string why) { if (!value) throw new Exception("Play Tag Leaf Explosion event contract: " + why); }
        object New(string nested) => Activator.CreateInstance(game.GetType("AnimationSet+StateInfo+" + nested, true)!)!;
        void Set(object value, string name, object field) => AccessTools.Field(value.GetType(), name).SetValue(value, field);
        var frame = New("FrameEvent"); Set(frame, "frame", 16);
        var ev = New("Event"); Set(ev, "componentName", "Bullet"); Set(ev, "methodName", "Destroy");
        ((System.Collections.IList)AccessTools.Field(frame.GetType(), "events").GetValue(frame)!).Add(ev);
        var checkFrame = AccessTools.DeclaredMethod(adapter, "FrameEvent");
        bool AcceptFrame() => (bool)checkFrame.Invoke(null, new[] { frame, (object)16, "Destroy" })!;
        Require(AcceptFrame(), "audited natural destruction event accepted");
        Set(ev, "methodName", "ActivateCollision"); Require(!AcceptFrame(), "collision activation rejected"); Set(ev, "methodName", "Destroy");
        Set(ev, "componentName", "WeaponSimple_GreatSword"); Require(!AcceptFrame(), "weapon callback rejected");

        object Sound(bool explosion)
        {
            var sound = New("SoundEvent");
            FieldInfo pathField = AccessTools.Field(sound.GetType(), "path");
            object path = Activator.CreateInstance(pathField.FieldType)!;
            var guidField = AccessTools.Field(pathField.FieldType, "Guid");
            object guid = Activator.CreateInstance(guidField.FieldType)!;
            int[] values = explosion ? new[] { -1489583142, 1090527874, 1827617978, 739549238 } : new[] { -1768456097, 1214824152, -1036708714, -221910872 };
            for (int i = 0; i < values.Length; i++) Set(guid, "Data" + (i + 1), values[i]);
            guidField.SetValue(path, guid); pathField.SetValue(sound, path); return sound;
        }
        var checkSound = AccessTools.DeclaredMethod(adapter, "SoundFrame");
        foreach (int index in new[] { 0, 2, 5 })
        {
            var soundFrame = New("FrameSoundEvent"); Set(soundFrame, "frame", index);
            var sounds = (System.Collections.IList)AccessTools.Field(soundFrame.GetType(), "events").GetValue(soundFrame)!;
            if (index == 0) sounds.Add(Sound(true)); sounds.Add(Sound(false));
            bool AcceptSound() => (bool)checkSound.Invoke(null, new[] { soundFrame, (object)index, index == 0 })!;
            Require(AcceptSound(), "native embedded sound layer at frame " + index);
            sounds.Add(Sound(false)); Require(!AcceptSound(), "duplicate sound rejected at frame " + index); sounds.RemoveAt(sounds.Count - 1);
            Set(sounds[0]!, "attachToPerformer", false); Require(!AcceptSound(), "changed sound attachment rejected");
        }
    }
}
