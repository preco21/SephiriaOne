using System.Runtime.CompilerServices;

public partial class UnitAvatar
{
    public bool IsDead;
    public float MaxHp = 100, hp;
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
    [MethodImpl(MethodImplOptions.NoInlining)] private void RpcRevive() { ReviveRpcCalls++; }
}
