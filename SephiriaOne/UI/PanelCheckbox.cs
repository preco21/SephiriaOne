using System;
using UnityEngine.UI;

namespace SephiriaOne
{
    internal sealed class PanelCheckbox
    {
        private readonly Toggle toggle;
        private readonly PanelToggleState state = new PanelToggleState();
        private readonly Func<SettingsSnapshot, bool> value, available;
        private readonly Func<bool, bool> apply;
        private bool canEnable, canMutate;
        internal PanelCheckbox(Toggle toggle, Func<SettingsSnapshot, bool> value, Func<SettingsSnapshot, bool> available, Func<bool, bool> apply)
        {
            this.toggle = toggle; this.value = value; this.available = available; this.apply = apply;
            toggle.onValueChanged.AddListener(Edit);
        }
        private void Edit(bool next)
        {
            state.UserEdit(next, canEnable, canMutate, apply);
            toggle.SetIsOnWithoutNotify(state.Value);
            toggle.interactable = state.CanEdit(!state.Value, canEnable, canMutate);
        }
        internal void Refresh(SettingsSnapshot snapshot)
        {
            state.Observe(value(snapshot)); canEnable = available(snapshot); canMutate = snapshot.CanMutate;
            toggle.SetIsOnWithoutNotify(state.Value);
            toggle.interactable = state.CanEdit(!state.Value, canEnable, canMutate);
        }
    }
}
