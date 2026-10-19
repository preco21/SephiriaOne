using System;
using UnityEngine.UI;

namespace SephiriaOne
{
    internal sealed class PanelCheckbox
    {
        private readonly Toggle toggle;
        private readonly PanelToggleState state;
        private readonly Func<SettingsSnapshot, bool> value, available;
        private readonly Func<bool, bool> apply;
        private readonly Func<SettingsSnapshot, bool> mutable;
        private bool compatible, canMutate;
        internal PanelCheckbox(Toggle toggle, Func<SettingsSnapshot, bool> value, Func<SettingsSnapshot, bool> available, Func<bool, bool> apply,
            Func<SettingsSnapshot, bool> mutable = null, bool recoveryValue = false)
        {
            this.toggle = toggle; this.value = value; this.available = available; this.apply = apply;
            this.mutable = mutable; state = new PanelToggleState(recoveryValue);
            toggle.onValueChanged.AddListener(Edit);
        }
        private void Edit(bool next)
        {
            state.UserEdit(next, compatible, canMutate, apply);
            toggle.SetIsOnWithoutNotify(state.Value);
            toggle.interactable = state.CanEdit(!state.Value, compatible, canMutate);
        }
        internal void Refresh(SettingsSnapshot snapshot)
        {
            state.Observe(value(snapshot)); compatible = available(snapshot); canMutate = mutable == null ? snapshot.CanMutate : mutable(snapshot);
            toggle.SetIsOnWithoutNotify(state.Value);
            toggle.interactable = state.CanEdit(!state.Value, compatible, canMutate);
        }
    }
}
