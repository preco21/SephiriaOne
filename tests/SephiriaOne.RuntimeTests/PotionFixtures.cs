public struct ItemMetadata
{
    public int instanceID, entityID;
    public sbyte quantity;
    public ItemMetadata(int instance, int entity, sbyte count) { instanceID = instance; entityID = entity; quantity = count; }
}
public sealed partial class GridInventory
{
    private bool writePermission;
    public bool PotionFull;
    public int PotionWrites;
    public Action BeforePotionWrite, AfterPotionWrite, PermissionOpened;
    public readonly Dictionary<sbyte, ItemMetadata> subBagMatrix = new();
    public readonly List<ItemMetadata> temporaryInventory = new();
    public bool HasPermission => writePermission;
    public sealed class Permission : IDisposable
    {
        private readonly GridInventory inventory;
        public Permission(GridInventory value) { inventory = value; inventory.writePermission = true; inventory.PermissionOpened?.Invoke(); }
        public void Dispose() { inventory.writePermission = false; }
    }
    public bool LocalAddItem(int instance, int id, sbyte count, int rotation, bool notification, bool isReward)
    {
        if (!writePermission) throw new InvalidOperationException("No native inventory permission");
        BeforePotionWrite?.Invoke();
        PotionWrites++;
        if (PotionFull) return false;
        inventoryMatrix[new ItemPosition { x = 0, y = 100 }] = new() { EntityID = id, Quantity = count };
        AfterPotionWrite?.Invoke();
        return true;
    }
}
