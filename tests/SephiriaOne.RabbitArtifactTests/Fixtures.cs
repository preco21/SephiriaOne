using System;
using System.Collections.Generic;

namespace UnityEngine
{
    public class Object
    {
        public bool Destroyed;
        public static implicit operator bool(Object value) => value != null && !value.Destroyed;
    }
    public static class Debug
    {
        public static readonly List<object> Warnings = new();
        public static void LogWarning(object value) => Warnings.Add(value);
    }
}

public sealed class LocalizedString { public string key; }
public sealed class ItemEntity : UnityEngine.Object
{
    public int id;
    public LocalizedString aName = new();
}
public sealed class CostumeEntity : UnityEngine.Object
{
    public string id;
    public ItemEntity[] startingItems = Array.Empty<ItemEntity>();
}
public static class CostumeDatabase
{
    public static readonly Dictionary<string, CostumeEntity> Items = new();
    public static CostumeEntity FindCostumeByID(string id) => Items.GetValueOrDefault(id);
}
public static class ItemDatabase
{
    public static readonly Dictionary<int, ItemEntity> Items = new();
    public static ItemEntity FindItemById(int id) => Items.GetValueOrDefault(id);
}
