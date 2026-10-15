using System.Runtime.CompilerServices;

// Representative native callback order and penalty effects. Installed-code
// contracts separately verify these are the real mutation/cleanup boundaries.
public sealed class CharacterBuff { }
public sealed class SaveData
{
    private readonly Dictionary<string, int> values = new();
    public int GetInt(string key, int fallback) => values.TryGetValue(key, out int value) ? value : fallback;
    public void SetInt(string key, int value) => values[key] = value;
}
public static class SaveManager { public static SaveData CurrentRun = new(); }
public partial class UnitAvatar
{
    public int CrimeBuffs, BattleStops;
    public void ApplyBuff(CharacterBuff buff) { CrimeBuffs++; }
    public void StopBattle() { BattleStops++; IsInBattle = false; }
}
public partial class RuntimeFactionManager
{
    public int TempHostilityChanges, ReputationChanges;
    [MethodImpl(MethodImplOptions.NoInlining)] public void SetTempEnemyRelation(string lhs, string rhs) { TempHostilityChanges++; }
    public void SetRelationValue(string lhs, string rhs, int value) { ReputationChanges++; }
}
public partial class DungeonManager
{
    public CharacterBuff crimeDebuff = new();
    public Dictionary<string, int> dungeonEnvironment = new();
    public int MerchantNotices, WandererNotices, CrimeChecks;
    [MethodImpl(MethodImplOptions.NoInlining)] public void NotifyWanderersHostile() { WandererNotices++; }
    private void RpcMerchantsHostileChange() { MerchantNotices++; }
    [MethodImpl(MethodImplOptions.NoInlining)] public void NPCDeadCheckServerside(UnitAI_NewBasic npc, DamageInstance damageInstance)
    {
        CrimeChecks++;
        if (damageInstance == null || !npc || !npc.Avatar || string.IsNullOrWhiteSpace(npc.socialID) ||
            !damageInstance.origin || !(damageInstance.origin is UnitAvatar { faction: "Player" }) ||
            RuntimeFactionManager.Instance.GetRelationValue(npc.Avatar.faction, "Player") <= 21) return;
        foreach (var player in PlayerSpawner.MultiplayerList)
            if (player && player.PlayerAvatar) player.PlayerAvatar.ApplyBuff(crimeDebuff);
        if (npc.Avatar.faction == "Merchant")
        {
            RuntimeFactionManager.Instance.SetRelationValue("Merchant", "Player", -100);
            RuntimeFactionManager.Instance.SetRelationValue("Player", "Merchant", -100);
            RpcMerchantsHostileChange(); dungeonEnvironment["RedMerchant"] = 1;
        }
        int crime = SaveManager.CurrentRun.GetInt("CrimeCount", 0);
        SaveManager.CurrentRun.SetInt("CrimeCount", crime + 1);
    }
}
public partial class UnitAI_NewBasic
{
    public string socialID = "companion-fixture";
    public void BindPenaltyCallbacks()
    {
        Avatar.OnHit += (_, damage) => OnDamaged(damage);
        Avatar.OnDeath += OnDie;
        isInBattleActiveByAI = true;
    }
    [MethodImpl(MethodImplOptions.NoInlining)] private void OnDamaged(DamageInstance damage)
    {
        if (damage == null || !damage.origin || !(damage.origin is UnitAvatar attacker) || attacker.faction == Avatar.faction) return;
        long layer = RuntimeFactionManager.Instance.FindFactionLayer(attacker.faction);
        if ((Avatar.GetHostileFactionLayers(EDamageFromType.None) & layer) == 0)
        {
            RuntimeFactionManager.Instance.SetTempEnemyRelation(Avatar.faction, attacker.faction);
            if (Avatar.faction == "Wanderer" && attacker.faction == "Player" && DungeonManager.Instance)
                DungeonManager.Instance.NotifyWanderersHostile();
        }
        if (!Avatar.IsInBattle) SetTarget(attacker);
    }
    [MethodImpl(MethodImplOptions.NoInlining)] private void OnDie(DamageInstance damage)
    {
        if (targetFound) { OnLostTarget(); CurrentTarget = null; targetFound = false; }
        if (DungeonManager.Instance) DungeonManager.Instance.NPCDeadCheckServerside(this, damage);
        if (isInBattleActiveByAI) { isInBattleActiveByAI = false; Avatar.StopBattle(); }
    }
}
