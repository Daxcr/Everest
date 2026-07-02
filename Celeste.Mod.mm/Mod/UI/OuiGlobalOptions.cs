using Celeste.Mod.Core;
using FMOD.Studio;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Monocle;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;

namespace Celeste.Mod.UI {
    public class OuiGlobalOptions : Oui {

        /// <summary>
        /// Interface used to "tag" mod options submenus.
        /// </summary>
        public interface ISubmenu { }

        public static OuiGlobalOptions Instance;

        private TextMenu menu;
        private TextMenu optionmenu;

        private const float onScreenX = 64f;
        private const float offScreenX = 64f + 1920f;

        private float alpha = 0f;

        private int savedMenuIndex = -1;

        private Action startSearching;

        public OuiGlobalOptions() {
            Instance = this;
        }

        // public static TextMenu CreateMenu(bool inGame, EventInstance snapshot) {
            
        // }

        private void ReloadModList() {
            Vector2 position = Vector2.Zero;

            int selected = -1;
            if (menu != null) {
                position = menu.Position;
                selected = menu.Selection;
                Scene.Remove(menu);
            }

            menu = CreateModList();
            // startSearching = AddSearchBox(menu, Overworld);

            if (selected >= 0) {
                menu.Selection = selected;
                menu.Position = position;
            }

            Scene.Add(menu);
        }

        public void ReloadOptionList() {
            Vector2 position = Vector2.Zero;

            int selected = -1;
            if (optionmenu != null) {
                position = optionmenu.Position;
                selected = optionmenu.Selection;
                Scene.Remove(optionmenu);
            }

            optionmenu = CreateOptionList(false, null);

            if (selected >= 0) {
                optionmenu.Selection = selected;
                optionmenu.Position = position;
            }

            Scene.Add(optionmenu);
        }

        public static TextMenu CreateOptionList(bool inGame, EventInstance snapshot) {
            patch_TextMenu menu = new patch_TextMenu {
                CompactWidthMode = true,
                BatchMode = true,
                Justify = new Vector2(0f, 0f),
                Position = new Vector2(612f, 64f)
            };

            menu.Add(new TextMenuExt.SubHeaderExt(Dialog.Clean("MODOPTIONS_GLOBALOPTIONS_MODS")));

            return menu;
        }

        public static TextMenu CreateModList() {
            patch_TextMenu menu = new patch_TextMenu {
                CompactWidthMode = true,
                BatchMode = true,
                Justify = new Vector2(0f, 0f),
                Position = new Vector2(64f, 64f)
            };

            menu.Add(new TextMenuExt.HeaderImage("menu/everest") {
                ImageColor = Color.White,
                ImageOutline = true,
                ImageScale = 0.32f
            });

            TextMenu.Item celeste;
            TextMenu.Item everest;
            TextMenu.Item bindings;
            TextMenu.Item managemods;
            menu.Add(new Spacer(32f));
            menu.Add(celeste = new ScalableButton(Dialog.Clean("MODOPTIONS_GLOBALOPTIONS_CELESTE")) { Scale = 0.6f });
            menu.Add(everest = new ScalableButton(Dialog.Clean("MODOPTIONS_GLOBALOPTIONS_EVEREST")) { Scale = 0.6f });
            menu.Add(bindings = new ScalableButton(Dialog.Clean("MODOPTIONS_GLOBALOPTIONS_BINDINGS")) { Scale = 0.6f });
            menu.Add(managemods = new ScalableButton(Dialog.Clean("MODOPTIONS_GLOBALOPTIONS_MANAGEMODS")) { Scale = 0.6f });

            TextMenu.Item header;
            menu.Add(header = new TextMenuExt.SubHeaderExt(Dialog.Clean("MODOPTIONS_GLOBALOPTIONS_MODS")));

            foreach (EverestModule mod in Everest._Modules.OrderBy(mod => mod.Metadata.Name)) {
                if (mod.Metadata.Name is "Everest" or "Celeste")
                    continue;
                if (mod.SettingsType == null)
                    continue;

                int settingPropertiesCount = mod.SettingsType
                    .GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
                    .Count(property => property.CanRead && property.CanWrite);

                if (settingPropertiesCount == 0)
                    continue;
                
                menu.Add(new ScalableButton(mod.Metadata.Name) { Scale = 0.6f });
            }

            return menu;
        }

        public override IEnumerator Enter(Oui from) {
            ReloadModList();

            // restore selection if coming from a submenu.
            if (savedMenuIndex != -1 && typeof(ISubmenu).IsAssignableFrom(from.GetType())) {
                menu.Selection = Math.Min(savedMenuIndex, menu.LastPossibleSelection);
                optionmenu.Selection = Math.Min(savedMenuIndex, optionmenu.LastPossibleSelection);
                menu.Position.Y = menu.ScrollTargetY;
                optionmenu.Position.Y = optionmenu.ScrollTargetY;
            }

            menu.Visible = Visible = true;
            optionmenu.Visible = Visible = true;
            menu.Focused = false;
            optionmenu.Focused = false;

            for (float p = 0f; p < 1f; p += Engine.DeltaTime * 4f) {
                menu.X = offScreenX + -1920f * Ease.CubeOut(p);
                optionmenu.X = offScreenX + -1920f * Ease.CubeOut(p);
                alpha = Ease.CubeOut(p);
                yield return null;
            }

            menu.Focused = true;
            optionmenu.Focused = true;
        }

        public override IEnumerator Leave(Oui next) {
            if (menu == null) {
                yield break;
            }
            Audio.Play(SFX.ui_main_whoosh_large_out);
            menu.Focused = false;
            optionmenu.Focused = false;

            // save the menu position in case we want to restore it.
            savedMenuIndex = menu.Selection;

            yield return Everest.SaveSettings();

            for (float p = 0f; p < 1f; p += Engine.DeltaTime * 4f) {
                menu.X = onScreenX + 1920f * Ease.CubeIn(p);
                alpha = 1f - Ease.CubeIn(p);
                yield return null;
            }

            menu.Visible = Visible = false;
            optionmenu.Visible = Visible = false;
            menu.RemoveSelf();
            optionmenu.RemoveSelf();
            menu = null;
            optionmenu = null;
        }

        public override void Update() {
            if (menu != null && menu.Focused &&
                Selected && Input.MenuCancel.Pressed) {
                Audio.Play(SFX.ui_main_button_back);
                Overworld.Goto<OuiMainMenu>();
            }

            if (Selected && Focused) {
                if (Input.QuickRestart.Pressed) {
                    startSearching?.Invoke();
                    return;
                }
            }

            base.Update();
        }

        public override void Render() {
            // if (alpha > 0f)
            //     Draw.Rect(-10f, -10f, 1940f, 1100f, Color.Black * alpha * 0.4f);
            menu.Y = 64f;
            if (optionmenu != null)
                optionmenu.Y = 64f;
            base.Render();
        }
    }
    public class ScalableButton : TextMenu.Button {
        public float Scale = 1f;
        public bool WasSelected = false;

        public ScalableButton(string label) : base(label) { }

        public override float Height() {
            return ActiveFont.LineHeight * Scale;
        }

        public override float LeftWidth() {
            return ActiveFont.Measure(Label).X * Scale;
        }

        public override void Update() {
            base.Update();
            if (Container is patch_TextMenu patchedMenu) {
                bool isSelected = patchedMenu.Selection == patchedMenu.Items.IndexOf(this);

                if (isSelected && !WasSelected) {
                    OuiGlobalOptions.Instance.ReloadOptionList();
                }

                WasSelected = isSelected;
            }
        }

        public override void Render(Vector2 position, bool highlighted) {
            float alpha = Container.Alpha;
            Color color = Disabled ? Color.DarkSlateGray : (highlighted ? Container.HighlightColor : Color.White);
            Color strokeColor = Color.Black * (alpha * alpha * alpha);

            ActiveFont.DrawOutline(
                Label,
                position,
                new Vector2(0f, 0.5f),
                Vector2.One * Scale,
                color * alpha,
                2f,
                strokeColor
            );
        }
    }
    public class Spacer : TextMenu.Item {
        private float height;
        public Spacer(float height) {
            this.height = height;
            Selectable = false;
        }
        public override float Height() => height;
        public override void Render(Vector2 position, bool highlighted) { }
    }
}
