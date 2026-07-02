using Celeste.Mod.Core;

namespace Celeste.Mod.UI {
    class OuiExperiments : OuiGenericMenu, OuiModOptions.ISubmenu {
        public override string MenuName => Dialog.Clean("MODOPTIONS_EXPERIMENTS");

        public OuiExperiments() {
            backToParentMenu = onBackPressed;
        }

        private void onBackPressed(Overworld overworld) {
            overworld.Goto<OuiModOptions>();
        }

        protected override void addOptionsToMenu(patch_TextMenu menu) {
            TextMenu.Item unifyOptions = null;
            TextMenu.Item betterModOptions = null;

            // menu.Add(new TextMenuExt.SubHeaderExt(Dialog.Clean("MODOPTIONS_EXPERIMENTS_DISCLAIM")));
            // Probably not needed until a risky experiment is added

            menu.Add(betterModOptions = new TextMenu.OnOff(Dialog.Clean("MODOPTIONS_EXPERIMENTS_BETTERMODOPTIONS"), CoreModule.Settings.BetterModOptions)
                .Change(value => {
                    CoreModule.Settings.BetterModOptions = value;
                    unifyOptions.Disabled = !value;
                }));
            betterModOptions.AddDescription(menu, Dialog.Clean("MODOPTIONS_EXPERIMENTS_BETTERMODOPTIONS_DESCR"));

            menu.Add(unifyOptions = new TextMenu.OnOff(Dialog.Clean("MODOPTIONS_EXPERIMENTS_UNIFYOPTIONS"), CoreModule.Settings.UnifyOptions) {
                Disabled = !CoreModule.Settings.UnifyOptions
            }.Change(value => {
                CoreModule.Settings.UnifyOptions = value;
            }));
            unifyOptions.AddDescription(menu, Dialog.Clean("MODOPTIONS_EXPERIMENTS_UNIFYOPTIONS_DESCR"));

            unifyOptions.Disabled = !CoreModule.Settings.BetterModOptions;
        }
    }
}
