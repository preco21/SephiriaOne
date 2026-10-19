using System.Runtime.CompilerServices;

public enum EInvincibleType { Normal, Fall }
public class ItemEntity : UnityEngine.Object { }
public static class ItemDatabase
{
    public static ItemEntity Bread = new();
    public static ItemEntity FindItemById(int id) => id == 1119 ? Bread : null;
}
public class GridInventory : UnityEngine.Object
{
    public Charm_Basic Bread;
    public bool TryGetUniqueEffect(ItemEntity item, out Charm_Basic charm)
    { charm = ReferenceEquals(item, ItemDatabase.Bread) ? Bread : null; return charm != null; }
}
public partial class Charm_Basic
{
    public bool IsEffectEnabled;
    public int limitedEffectEnabledLevel;
    public int CurrentLevelToIdx() => Math.Clamp(limitedEffectEnabledLevel, 0, 2);
}
public class Charm_HitInvincible : Charm_Basic
{
    public float[] addTimeByLevel = { 0.4f, 0.8f, 1.2f };
}
public partial class UnitAvatar
{
    public bool isServer = true;
    public GridInventory Inventory = new();
    public float invincibleTime = 0.3f;
    private float invincibleTimer;
    private bool isHitInvincibleEnabled;
    private EInvincibleType invincibleType;
    private Timer restoreFallingTimer = new(2);
    public void SetHitWindow(float elapsed, EInvincibleType type = EInvincibleType.Normal)
    { invincibleTimer = elapsed; invincibleType = type; isHitInvincibleEnabled = true; }
    [MethodImpl(MethodImplOptions.NoInlining)] public void AdvanceHitWindow(float seconds)
    {
        if (!isHitInvincibleEnabled) return;
        invincibleTimer += seconds;
        float duration = invincibleTime;
        if (invincibleType == EInvincibleType.Fall) duration += restoreFallingTimer.time;
        if (invincibleTimer >= duration) { invincibleTimer = 0; isHitInvincibleEnabled = false; }
    }
}
