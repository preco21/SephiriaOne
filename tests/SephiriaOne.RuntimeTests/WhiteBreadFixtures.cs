public enum EInvincibleType { Normal, Fall }
public class Timer { public float time; public Timer(float value) { time = value; } }
public class ItemEntity : UnityEngine.Object { public int id; public EItemType type; public EItemActiveType activeType; public ItemName aName; }
public class ItemName { public string key; }
public enum EItemType { Artifact, Potion }
public enum EItemActiveType { Normal, Disabled }
public static class ItemDatabase
{
    public static readonly ItemEntity Bread = new();
    public static ItemEntity LargePotion = new() { id = 1, type = EItemType.Potion, aName = new() { key = "Item_BigPotionOfRegeneration_Name" } };
    public static ItemEntity FindItemById(int id) => id == 1119 ? Bread : id == 1 ? LargePotion : null;
    private static int instance;
    public static int GenerateInstanceID(Random _) => ++instance;
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
