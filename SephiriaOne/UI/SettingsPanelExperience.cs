namespace SephiriaOne
{
    public sealed partial class SettingsPanel
    {
        private void BuildExperienceEditor()
        {
            AddCheckbox("Block XP gain", 16, 0, 273, 28, s => s.Experience.Block,
                s => s.ExperienceAvailable, "/one xp block");
            AddCheckbox("Exempt Wing-Eared Rabbit", 16, 36, 273, 28, s => s.Experience.RabbitExempt,
                s => s.ExperienceAvailable, "/one xp rabbit-exempt");
            resetAll = widgets.Button(pageRoot, "Reset", 16, 137, 273, 25, () => Execute("/one xp reset", true));
            readout = widgets.Scroll(pageRoot, 312, 0, 272, 162);
        }
    }
}
