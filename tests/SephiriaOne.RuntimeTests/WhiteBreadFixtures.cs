public enum EInvincibleType { Normal, Fall }
public class Timer { public float time; public Timer(float value) { time = value; } }
public class ItemEntity { }
public static class ItemDatabase
{
    public static readonly ItemEntity Bread = new();
    public static ItemEntity FindItemById(int id) => id == 1119 ? Bread : null;
}
public class Charm_Basic : UnityEngine.Object
{
    public UnitAvatar NetworkAvatar;
    public bool IsEffectEnabled;
}
public class Charm_HitInvincible : Charm_Basic
{
    public float[] addTimeByLevel = { 0.4f, 0.8f, 1.2f };
    public int CurrentLevelToIdx() => 2;
}
public partial class UnitAvatar { public float invincibleTime = 1.5f; }
public sealed partial class GridInventory
{
    public Charm_Basic Bread;
    public bool TryGetUniqueEffect(ItemEntity item, out Charm_Basic charm)
    { charm = ReferenceEquals(item, ItemDatabase.Bread) ? Bread : null; return charm != null; }
}
