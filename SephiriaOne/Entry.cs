using UnityEngine;

namespace SephiriaOne
{
    public sealed class Entry : HorayModBase
    {
        protected override void OnModLoaded()
        {
            HorayModAPI.OnAllDatabasesReady += OnDatabasesReady;
            Debug.Log("[SephiriaOne] Loaded v0.1.0");
        }

        private void OnDatabasesReady()
        {
            Debug.Log("[SephiriaOne] All databases ready");
        }

        protected override void OnModUnloaded()
        {
            HorayModAPI.OnAllDatabasesReady -= OnDatabasesReady;
            Debug.Log("[SephiriaOne] Unloaded");
        }
    }
}