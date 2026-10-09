using System;

namespace SephiriaOne
{
    internal sealed class PanelToggleState
    {
        internal bool Value { get; private set; }
        internal void Observe(bool value) { Value = value; }
        internal bool CanEdit(bool value, bool available, bool authority) => authority && (!value || available);
        internal void UserEdit(bool value, bool available, bool authority, Func<bool, bool> apply)
        {
            if (CanEdit(value, available, authority) && apply(value)) Value = value;
        }
    }
}
