using System.Runtime.CompilerServices;

public partial class UnitAvatar
{
    public bool IsDead;
    public float maxHp = 100, finalMaxHp, hp;
    public sbyte isHPCursed;
    public int cursedMaxHp, HealthWrites;
    public float MaxHp { get => isHPCursed > 0 ? cursedMaxHp : maxHp + finalMaxHp * maxHp / 100f;
        set { maxHp = value; finalMaxHp = 0; isHPCursed = 0; } }
    public float NetworkmaxHp { set { maxHp = value; HealthWrites++; } }
    public float Networkhp { set { hp = value; HealthWrites++; } }
    public int NetworkcursedMaxHp { set { cursedMaxHp = value; HealthWrites++; } }
    public int ReviveCalls, ReviveRpcCalls, ReviveProtectionCalls, RemoteInventoryCalls;
    public event Action<float> OnHpChangedServerside;
    public event Action OnRevive;
    public Action ReviveCallback { get => OnRevive; set => OnRevive = value; }
    [MethodImpl(MethodImplOptions.NoInlining)] public void Revive(float amount)
    {
        if (!IsDead) return;
        ReviveCalls++; IsDead = false; hp = amount;
        OnHpChangedServerside?.Invoke(amount);
        OnRevive?.Invoke();
        StartReviveInvulnerable(); TakeRemoteInventory(); RpcRevive();
    }
    [MethodImpl(MethodImplOptions.NoInlining)] public void StartReviveInvulnerable() { ReviveProtectionCalls++; }
    [MethodImpl(MethodImplOptions.NoInlining)] public void TakeRemoteInventory() { RemoteInventoryCalls++; }
    [MethodImpl(MethodImplOptions.NoInlining)] private void RpcRevive() { ReviveRpcCalls++; Lifecycle.Add("RpcRevive"); }
}
