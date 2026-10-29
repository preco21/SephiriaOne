using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

internal static class GamePvpCoverTests
{
    internal static void Run(Assembly game, Assembly addon)
    {
        int checks = 0;
        void Check(bool value, string why) { checks++; if (!value) throw new Exception("PvP cover contract: " + why); }
        Type Game(string name) => game.GetType(name, true)!;
        Type Mod(string name) => addon.GetType("SephiriaOne." + name, true)!;
        List<CodeInstruction> Code(Type type, string name) => PatchProcessor.GetOriginalInstructions(AccessTools.DeclaredMethod(type, name)).ToList();
        bool Calls(IEnumerable<CodeInstruction> code, string type, string name) => code.Any(i => i.operand is MethodInfo m && m.DeclaringType?.Name == type && m.Name == name);
        Check((bool)AccessTools.Method(Mod("PvpCoverRooms"), "Validate").Invoke(null, null)!, "Supported room fields match installed game");
        var authored = Game("FullyDesignedFloorGenerator");
        Check(authored.BaseType == Game("FloorGenerator") && !Game("TileFloorGenerator").IsAssignableFrom(authored), "Native waiting-area generator is outside the tile-floor hierarchy");
        var layoutConstructor = AccessTools.Constructor(Mod("PvpCoverRooms"), new[] { Game("FloorGenerator"), typeof(int) });
        var layoutCode = PatchProcessor.GetOriginalInstructions(layoutConstructor).ToList();
        foreach (string field in new[] { "bottomLeft", "topRight", "props", "mapTeleportConnections" })
            Check(layoutCode.Any(i => Equals(i.operand, AccessTools.Field(authored, field))), "Authored layout uses native " + field);
        Check(Calls(layoutCode, "TeleportPoint", "GetTeleportTargetPosition"), "Protect actual native map destinations including their offsets");
        foreach (string path in new[] { "Grid/Ground", "Grid/Pit/Ground", "Grid/WallCollider", "Grid/Upper", "Grid/Ground-Lit", "QQQGrid/QQQGround" })
            Check(layoutCode.Any(i => i.opcode == OpCodes.Ldstr && Equals(i.operand, path)), "Audited authored tilemap path " + path);
        foreach (var (type, field) in new[] { ("TeleportTogether", "offset"), ("BossSpawner", "spawnOffset"), ("BossSpawner", "bossRewardPosition"),
            ("BossEnvironment_QQBoss", "qqqBossPoint"), ("BossEnvironment_QQBoss", "qqqBossDramaticDieReturnPoint") })
            Check(layoutCode.Any(i => Equals(i.operand, AccessTools.Field(Game(type), field))), "Protect native progression anchor " + type + "." + field);
        Check(Calls(Code(Mod("PvpCoverRooms"), "Safe"), "Tilemap", "GetCellCenterWorld"), "Terrain uses native isometric cell centers");
        var runtimeCode = Code(Mod("PvpCoverRuntime"), "Tick");
        Check(!runtimeCode.Any(i => (i.opcode == OpCodes.Isinst || i.opcode == OpCodes.Castclass) && Equals(i.operand, Game("TileFloorGenerator"))), "Readiness no longer rejects authored lobby floors before placement");
        Check(Calls(Code(Mod("PvpCoverRooms"), "Safe"), "Physics2D", "OverlapBox") && Calls(Code(Mod("PvpCoverRooms"), "Safe"), "Tilemap", "GetTile"), "Ground and collision safety remain required");
        Check(Calls(Code(Game("TopdownRigidbody"), "UpdateAirLayer"), "CombatManager", "get_BlockableLayer"), "Blockable includes grounded actors, not just static walls");
        var movement = Code(Game("TopdownRigidbody"), "Update");
        Check(Calls(movement, "CombatManager", "get_TileLayerMask") && Calls(movement, "CombatManager", "get_CliffLayerMask") &&
            Calls(movement, "CombatManager", "get_PitLayerMask") && !Calls(movement, "CombatManager", "get_BlockableLayerMask"),
            "Native movement blocks Tile/Cliff/Pit, not the actor/prop Blockable occupancy layer");
        var breakable = Game("BreakableProp");
        var canBreak = AccessTools.Field(breakable, "CanBeBroken");
        var setter = AccessTools.PropertySetter(breakable, "NetworkCanBeBroken");
        var code = PatchProcessor.GetOriginalInstructions(setter).ToList();
        Check(code.Any(i => i.opcode == OpCodes.Ldflda && Equals(i.operand, canBreak)) && code.Any(i => i.LoadsConstant(8L)), "Native breakability SyncVar uses dirty bit 8");
        Check(Code(breakable, "ApplyDamage").Any(i => i.opcode == OpCodes.Ldfld && Equals(i.operand, canBreak)), "Native damage respects unbreakable flag");
        Check(!Code(breakable, "BreakForce").Any(i => Equals(i.operand, canBreak)), "Force-break needs separate owned-object guard");
        Check(Code(breakable, "SerializeSyncVars").Any(i => Equals(i.operand, canBreak)) && Code(breakable, "DeserializeSyncVars").Any(i => Equals(i.operand, canBreak)), "Stock guests receive unbreakable flag including initial spawn");
        Check(Calls(Code(breakable, "OnRpcBreak"), "Transform", "SetParent"), "Native break creates detached fragments: never use for cleanup");
        var native = Mod("PvpCoverNative");
        var entries = (Array)AccessTools.Field(Mod("PvpCoverCatalog"), "Entries").GetValue(null)!;
        var audited = new Dictionary<string, (uint AssetId, float Width, float Height, float OffsetY)>
        {
            ["DomitoryBarrel_0"] = (4148004312, .875f, .875f, .4375f),
            ["DomitoryBarrel_2"] = (3412844843, .875f, 1, .5f),
            ["DomitoryBarrel_3_Potato"] = (3401681619, .875f, .875f, .4375f),
            ["DomitoryBarrel_4_Carrot"] = (1484013372, .875f, .875f, .4375f),
            ["DomitoryTable_1"] = (2839126442, 1.35f, .8f, .4f)
        };
        foreach (var entry in entries)
        {
            T Field<T>(string name) => (T)AccessTools.Field(entry!.GetType(), name).GetValue(entry)!;
            Check(audited.Remove(Field<string>("Id"), out var expected) &&
                (Field<uint>("AssetId"), Field<float>("Width"), Field<float>("Height"), Field<float>("OffsetY")) == expected,
                "Catalog matches audited stock asset identity and collision dimensions");
        }
        Check(audited.Count == 0, "Every audited compact cover variant is available");
        Check(Calls(Code(native, "Center"), "Collider2D", "get_offset"), "Cleanup uses each prop's actual collision center");
        code = Code(native, "Spawn");
        Check(Calls(code, "Collider2D", "get_offset"), "Spawn uses selected prop's collider offset rather than a barrel constant");
        int setAt = code.FindIndex(i => i.Calls(setter));
        int spawnAt = code.FindIndex(i => i.operand is MethodInfo m && m.DeclaringType?.Name == "NetworkServer" && m.Name == "Spawn");
        Check(setAt >= 0 && spawnAt > setAt, "Unbreakable flag precedes native network spawn");
        Check(code.Any(i => i.operand is MethodInfo m && m.DeclaringType?.Name.StartsWith("Action") == true && m.Name == "Invoke") &&
            code.FindIndex(i => i.operand is MethodInfo m && m.Name == "Invoke") < spawnAt, "Track identity before publication can fail");
        Check(!code.Any(i => i.operand is MethodInfo m && m.Name == "AddComponent"), "No extra client-required network behaviours");
        code = Code(native, "Remove");
        Check(Calls(code, "NetworkServer", "Destroy") && !Calls(code, "BreakableProp", "BreakForce") && !Calls(code, "BreakableProp", "RpcBreak"), "Network despawn without client debris");
        code = Code(Game("PathGrid"), "UpdateRegion");
        Check(code.Any(i => i.operand is FieldInfo f && f.Name == "isScanning") && code.Any(i => i.operand is FieldInfo f && f.Name == "pendingRegionUpdates"), "Native scan queues regional refreshes");
        code = Code(Mod("PvpCoverRuntime"), "FlushPathfinding");
        Check(Calls(code, "PathGrid", "UpdateRegion") && !Calls(code, "PathGrid", "get_IsBuilt"), "Do not drop refreshes during scans");
        code = Code(Mod("PvpCoverFeature"), "Shutdown");
        Check(Calls(code, "PvpCoverRuntime", "get_ActiveCount") && code.Any(i => i.opcode == OpCodes.Throw), "Unload stops while cleanup is pending");
        Console.WriteLine($"Passed {checks} installed-game PvP cover contracts.");
    }
}
