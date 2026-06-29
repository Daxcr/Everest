using Celeste.Mod.Core;
using FMOD.Studio;
using Microsoft.Xna.Framework;
using Monocle;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Celeste.Mod.UI {
    public class OuiMapList : Oui {

        private MountainCamera cameraStart;
        private MountainCamera cameraEnd;

        public List<patch_OuiChapterSelectIcon> OuiIcons;

        private patch_TextMenu menu;

        private const float onScreenX = 960f;
        private const float offScreenX = 2880f;

        private float alpha = 0f;

        private int type = 2;
        private int side = 0;
        private bool showHidden = false;
        private bool edited = false;

        private List<TextMenuExt.IItemExt> items = new List<TextMenuExt.IItemExt>();

        private List<string> sets = new List<string>();

        private patch_TextMenu CreateMenu(bool inGame, EventInstance snapshot) {
            menu = new patch_TextMenu();
            menu.CompactWidthMode = true;
            items.Clear();

            menu.Add(new TextMenu.Header(Dialog.Clean("maplist_title")));

            menu.Add(new patch_TextMenu.patch_SubHeader(Dialog.Clean("maplist_filters")));

            sets.Clear();
            foreach (patch_AreaData area in AreaData.Areas) {
                string levelSet = area.LevelSet;
                if (string.IsNullOrEmpty(levelSet))
                    continue;
                if (levelSet == "Celeste")
                    continue;
                if (sets.Contains(levelSet))
                    continue;
                sets.Add(levelSet);
            }

            menu.Add(new TextMenu.Slider(Dialog.Clean("maplist_type"), value => {
                if (value == 0)
                    return Dialog.Clean("levelset_celeste");
                if (value == 1)
                    return Dialog.Clean("maplist_type_everything");
                if (value == 2)
                    return Dialog.Clean("maplist_type_allmods");
                if (Dialog.Has("maplist_type_" + sets[value - 3]))
                    return Dialog.Clean("maplist_type_" + sets[value - 3]);
                return patch_Dialog.CleanLevelSet(sets[value - 3]);

            }, 0, 2 + sets.Count, type).Change(value => {
                type = value;
                ReloadItems();
            }));

            menu.Add(new TextMenu.Slider(Dialog.Clean("maplist_side"), value => ((char) ('A' + value)).ToString(), 0, Enum.GetValues(typeof(AreaMode)).Length - 1, side).Change(value => {
                side = value;
                ReloadItems();
            }));

            menu.Add(new TextMenu.OnOff(Dialog.Clean("maplist_showhidden"), showHidden).Change(value => {
                showHidden = value;
                ReloadItems();
            }));

            menu.Add(new TextMenu.Button(Dialog.Clean("maplist_search")).Pressed(() => {
                Overworld.Goto<OuiMapSearch>();
            }));

            // menu.Add(new patch_TextMenu.patch_SubHeader(Dialog.Clean("maplist_list")));
            // keep this or nah? imo it looks a little weird having 2 headings right next to each other

            ReloadItems();

            return menu;
        }

        private void ReloadItems(float delay = 0.03f) {
            menu.BatchMode = true;

            foreach (TextMenu.Item item in items)
                menu.Remove(item);
            items.Clear();

            string filterSet = null;
            if (type == 0) {
                filterSet = "Celeste";
            } else if (type >= 3) {
                filterSet = sets[type - 3];
            }

            string lastLevelSet = null;
            LevelSetStats levelSetStats = null;
            int levelSetAreaOffset = 0;
            int levelSetUnlockedAreas = int.MaxValue;
            int levelSetUnlockedModes = int.MaxValue;
            string name;

            List<patch_AreaData> Hidden = new();

            SaveData save = SaveData.Instance;
            List<AreaStats> areaStatsAll = save.Areas;
            for (int i = 0; i < AreaData.Areas.Count; i++) {
                patch_AreaData area = patch_AreaData.Get(i);
                if (area == null || !area.HasMode((AreaMode) side))
                    continue;

                bool isHidden = false;
                if (CoreModule.Settings.HiddenMapOverrides.TryGetValue(area.SID, out bool value) == true)
                    isHidden = value;
                else
                    isHidden = area.Meta?.Hidden ?? false;

                // TODO: Make subchapters hidden by default in the map list, even in debug mode.
                if (!save.DebugMode && !string.IsNullOrEmpty(area.Meta?.Parent))
                    continue;

                string levelSet = area.LevelSet;

                if (type != 1 && ((filterSet == null && levelSet == "Celeste") || (filterSet != null && filterSet != levelSet)))
                    continue;
                
                if (isHidden) {
                    if (showHidden)
                        Hidden.Add(area);
                    continue;
                }

                name = area.Name;
                name = name.DialogCleanOrNull() ?? name.SpacedPascalCase();

                if (lastLevelSet != levelSet) {
                    lastLevelSet = levelSet;

                    levelSetStats = patch_SaveData.Instance.GetLevelSetStatsFor(levelSet);
                    if (levelSetStats != null) {
                        levelSetAreaOffset = levelSetStats.AreaOffset;
                        levelSetUnlockedAreas = levelSetStats.UnlockedAreas;
                        levelSetUnlockedModes = levelSetStats.UnlockedModes;
                    } else {
                        levelSetAreaOffset = 0;
                        levelSetUnlockedAreas = int.MaxValue;
                        levelSetUnlockedModes = int.MaxValue;
                    }

                    string setname = patch_Dialog.CleanLevelSet(levelSet);
                    TextMenuExt.SubHeaderExt levelSetHeader = new TextMenuExt.SubHeaderExt(setname);
                    levelSetHeader.Alpha = 0f;
                    
                    menu.Add(levelSetHeader);
                    items.Add(levelSetHeader);
                }

                TextMenuExt.ButtonExt buttonMain = new TextMenuExt.ButtonExt(name);
                buttonMain.Alpha = 0f;

                if (area.Icon != "areas/null")
                    buttonMain.Icon = area.Icon;
                buttonMain.IconWidth = 64f;

                if (levelSet == "Celeste" && i > levelSetAreaOffset + levelSetUnlockedAreas)
                    buttonMain.Disabled = true;
                if (side == 1 && !areaStatsAll[i].Cassette)
                    buttonMain.Disabled = true;
                if (side >= 2 && levelSetUnlockedModes < (side + 1))
                    buttonMain.Disabled = true;

                buttonMain.Pressed(() => {
                    Inspect(area, (AreaMode) side);
                });

                TextMenuExt.ButtonExt hideButton = new TextMenuExt.ButtonExt(""); 
                hideButton.Alpha = 0f;
                hideButton.Icon = "menu/hide_map";
                hideButton.IconWidth = 12f;

                hideButton.Pressed(() => {
                    bool hiddenDefault = area.Meta?.Hidden ?? false;
                    area.Hidden = true;
                    if (!hiddenDefault)
                        CoreModule.Settings.HiddenMapOverrides[area.SID] = true;
                    else
                        CoreModule.Settings.HiddenMapOverrides.Remove(area.SID);
                    edited = true;
                    ReloadItems(0);
                });

                TextMenuExt.MapList horizontalRow = new TextMenuExt.MapList();
                horizontalRow.Add(buttonMain);

                // stops people from complaining about "x map disappeared"
                if (showHidden && levelSet != "Celeste")
                    horizontalRow.Add(hideButton);

                menu.Add(horizontalRow);
                items.Add(horizontalRow);
            }
            if (showHidden && Hidden.Count > 0)
            {
                string setname = patch_Dialog.Get("MAPLIST_HIDDEN");
                TextMenuExt.SubHeaderExt levelSetHeader = new TextMenuExt.SubHeaderExt(setname);
                levelSetHeader.Alpha = 0f;
                
                menu.Add(levelSetHeader);
                items.Add(levelSetHeader);
                foreach (patch_AreaData area in Hidden) {
                    name = area.Name;
                    name = name.DialogCleanOrNull() ?? name.SpacedPascalCase();

                    TextMenuExt.ButtonExt buttonMain = new TextMenuExt.ButtonExt(name);
                    buttonMain.Alpha = 0f;

                    if (area.Icon != "areas/null")
                        buttonMain.Icon = area.Icon;
                    buttonMain.IconWidth = 64f;

                    buttonMain.Pressed(() => {
                        Inspect(area, (AreaMode) side);
                    });

                    TextMenuExt.ButtonExt showButton = new TextMenuExt.ButtonExt(""); 
                    showButton.Alpha = 0f;
                    showButton.Icon = "menu/show_map";
                    showButton.IconWidth = 12f;

                    showButton.Pressed(() => {
                        bool hiddenDefault = area.Meta?.Hidden ?? false;
                        area.Hidden = false;
                        if (hiddenDefault)
                            CoreModule.Settings.HiddenMapOverrides[area.SID] = false;
                        else
                            CoreModule.Settings.HiddenMapOverrides.Remove(area.SID);
                        edited = true;
                        ReloadItems(0);
                    });

                    TextMenuExt.MapList horizontalRow = new TextMenuExt.MapList();
                    horizontalRow.Add(buttonMain);

                    horizontalRow.Add(showButton);

                    menu.Add(horizontalRow);
                    items.Add(horizontalRow);
                }
            }

            ((patch_TextMenu) menu).BatchMode = false;

            if (menu.Items.Count > 0)
                menu.Selection = Math.Clamp(menu.Selection, menu.FirstPossibleSelection, menu.LastPossibleSelection);
            
            // compute a delay so that options don't take more than a second to show up if many mods are installed.
            float delayBetweenOptions = delay;
            if (items.Count > 0)
                delayBetweenOptions = Math.Min(delay, 1f / items.Count);

            // Do this afterwards as the menu has now properly updated its size.
            if (delay > 0)
                for (int i = 0; i < items.Count; i++)
                    Add(new Coroutine(FadeIn(i, delayBetweenOptions, items[i])));
            else
                foreach (TextMenuExt.IItemExt item in items)
                    item.Alpha = 1;

            if (menu.Height > menu.ScrollableMinSize) {
                menu.Position.Y = menu.ScrollTargetY;
            }
        }

        private IEnumerator FadeIn(int i, float delayBetweenOptions, TextMenuExt.IItemExt item) {
            yield return delayBetweenOptions * i;
            float ease = 0f;

            Vector2 offset = item.Offset;

            for (float p = 0f; p < 1f; p += Engine.DeltaTime * 4f) {
                ease = Ease.CubeOut(p);
                item.Alpha = ease;
                item.Offset = offset + new Vector2(0f, 64f * (1f - ease));
                yield return null;
            }

            item.Alpha = 1f;
            item.Offset = offset;
        }

        private void ReloadMenu() {
            Vector2 position = Vector2.Zero;

            int selected = -1;
            if (menu != null) {
                position = menu.Position;
                selected = menu.Selection;
                Scene.Remove(menu);
            }

            menu = CreateMenu(false, null);

            if (selected >= 0) {
                menu.Selection = selected;
                menu.Position = position;
            }
            IEnumerable<TextMenu> menus = Scene.Entities.OfType<TextMenu>();
            Scene.Remove(menus);
            Scene.Add(menu);
        }

        public override IEnumerator Enter(Oui from) {

            ReloadMenu();

            menu.Visible = (Visible = true);
            menu.Focused = false;

            cameraStart = Overworld.Mountain.UntiltedCamera;
            cameraEnd = cameraStart;
            cameraEnd.Position = cameraEnd.Position + -cameraStart.Rotation.Forward() * 1f;
            Overworld.Mountain.EaseCamera(Overworld.Mountain.Area, cameraEnd, 2f);
            Overworld.Mountain.AllowUserRotation = false;

            for (float p = 0f; p < 1f; p += Engine.DeltaTime * 4f) {
                menu.X = offScreenX + -1920f * Ease.CubeOut(p);
                alpha = Ease.CubeOut(p);
                yield return null;
            }

            menu.Focused = true;
        }

        public override IEnumerator Leave(Oui next) {

            menu.Focused = false;
            if (edited)
                UserIO.SaveHandler(file: false, settings: true);
            Audio.Play(SFX.ui_main_whoosh_large_out);

            if (Overworld != null) {
                Overworld.Maddy.Show = true;
                Overworld.Mountain.EaseCamera(Overworld.Mountain.Area, cameraStart, 0.4f);
            }

            for (float p = 0f; p < 1f; p += Engine.DeltaTime * 4f) {
                menu.X = onScreenX + 1920f * Ease.CubeIn(p);
                alpha = 1f - Ease.CubeIn(p);
                yield return null;
            }

            menu.Visible = Visible = false;
            menu.RemoveSelf();
            edited = false;
            menu = null;
        }

        public override void Update() {

            if (menu != null && menu.Focused && Selected) {
                Overworld.Maddy.Show = false;

                if (Input.MenuCancel.Pressed || Input.Pause.Pressed || Input.ESC.Pressed) {
                    Audio.Play(SFX.ui_main_button_back);
                    Overworld.Goto<OuiChapterSelect>();
                }

            }

            base.Update();
        }

        public override void Render() {
            if (alpha > 0f)
                Draw.Rect(-10f, -10f, 1940f, 1100f, Color.Black * alpha * 0.4f);

            base.Render();
        }

        protected void Inspect(patch_AreaData area, AreaMode mode = AreaMode.Normal) {
            Focused = false;
            Audio.Play(SFX.ui_world_icon_select);
            SaveData.Instance.LastArea = area.ToKey(mode);
            if (OuiIcons != null && area.ID < OuiIcons.Count)
                OuiIcons[area.ID].Select();
            Overworld.Mountain.Model.EaseState(area.MountainState);
            Overworld.Goto<OuiChapterPanel>();
        }

        private void Start(patch_AreaData area, AreaMode mode = AreaMode.Normal, string checkpoint = null) {
            Focused = false;
            Audio.Play(SFX.ui_world_chapter_checkpoint_start);
            Add(new Coroutine(StartRoutine(area, mode, checkpoint)));
        }

        private IEnumerator StartRoutine(patch_AreaData area, AreaMode mode = AreaMode.Normal, string checkpoint = null) {
            Overworld.Maddy.Hide(false);
            area.Wipe(Overworld, false, null);
            Audio.SetMusic(null, true, true);
            Audio.SetAmbience(null, true);
            if ((area.ID == 0 || area.ID == 9) && checkpoint == null && mode == AreaMode.Normal) {
                ((patch_RendererList) (object) Overworld.RendererList).UpdateLists();
                Overworld.RendererList.MoveToFront(Overworld.Snow);
            }
            yield return 0.5f;
            LevelEnter.Go(new Session(area.ToKey(mode), checkpoint), false);
        }

    }
}