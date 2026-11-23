using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace StarFall
{
    public sealed partial class GameUI : MonoBehaviour
    {
        [SerializeField] private Font font;
        private static readonly Color Background = new Color(0.035f, 0.055f, 0.09f);
        private static readonly Color Panel = new Color(0.075f, 0.11f, 0.17f);
        private static readonly Color Cyan = new Color(0.22f, 0.9f, 0.91f);
        private static readonly Color Muted = new Color(0.55f, 0.66f, 0.76f);
        private static readonly Color Coral = new Color(1, 0.32f, 0.38f);
        private GameApplication app;
        private RectTransform canvas;
        private GameObject menu, hud, pause, result, settings;
        private Text healthText, timerText, killsText, resultTitle, resultDescription, countdownText, volumeText, soulText, castingText;
        private RectTransform healthFill;
        private Button newGameButton, resumeButton, settingsBackButton, resultBackButton;
        public Button NewGameButton => newGameButton;
        public bool MenuVisible => menu != null && menu.activeSelf;

        public void Initialize(GameApplication application)
        {
            app = application;
            var root = new GameObject("Interface", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.transform.SetParent(transform, false);
            canvas = root.GetComponent<RectTransform>();
            root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            if (EventSystem.current == null)
            {
                var events = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                events.transform.SetParent(transform, false);
            }
            BuildMenu();
            BuildHud();
            BuildDamageNumbers();
            BuildPause();
            BuildResult();
            BuildSettings();
            BuildSelectionPanel();
            BuildBase();
            BuildWorkshop();
            settings.SetActive(false);
        }

        private void BuildMenu()
        {
            menu = FullPanel("MainMenu", Background);
            var layout = Box(menu.transform, "MenuLayout", new Vector2(0, 0), new Vector2(1080, 600));
            Label(layout, "Brand", "S T A R F A L L", new Vector2(-255, 215), new Vector2(550, 90), 62, Color.white, TextAnchor.MiddleLeft);
            Label(layout, "Subtitle", "晨星坠落  /  法杖与灵魂", new Vector2(-255, 146), new Vector2(550, 50), 23, Cyan, TextAnchor.MiddleLeft);
            Label(layout, "Objective", "鼠标瞄准 · 自动施法 · 挑战关底", new Vector2(-255, 83), new Vector2(550, 45), 19, Muted, TextAnchor.MiddleLeft);
            newGameButton = Button(layout, "NewGame", "新游戏", new Vector2(-320, -8), new Vector2(420, 64), app.NewGame, true);
            Button(layout, "Settings", "设置", new Vector2(-320, -88), new Vector2(420, 64), app.OpenSettings);
            Button(layout, "Quit", "退出", new Vector2(-320, -168), new Vector2(420, 64), app.Quit);
            Label(layout, "Help", "WASD 移动   /   按住空格吸魂   /   Q 终极 · ESC 暂停", new Vector2(-255, -257), new Vector2(550, 40), 17, Muted, TextAnchor.MiddleLeft);

            var diagram = Box(layout, "SurvivalCard", new Vector2(300, 5), new Vector2(420, 510));
            Image(diagram, "Card", Panel, Vector2.zero, new Vector2(420, 510));
            Label(diagram, "Protocol", "FIRE & SOUL / 01", new Vector2(0, 200), new Vector2(340, 42), 18, Muted);
            Label(diagram, "Duration", "关底 08:00", new Vector2(0, 125), new Vector2(380, 90), 44, Color.white);
            Image(diagram, "PlayerMark", Cyan, new Vector2(-25, 5), new Vector2(66, 66)).rectTransform.localEulerAngles = new Vector3(0, 0, 45);
            Image(diagram, "EnemyMarkA", Coral, new Vector2(105, 50), new Vector2(40, 40));
            Image(diagram, "EnemyMarkB", Coral, new Vector2(-125, -55), new Vector2(40, 40));
            Image(diagram, "BulletMark", new Color(1, 0.83f, 0.28f), new Vector2(45, 25), new Vector2(12, 12));
            Label(diagram, "Legend", "青色 · 你     红色 · 敌人", new Vector2(0, -140), new Vector2(370, 42), 19, Muted);
            Button(diagram, "QuickTest", "宝石整备 / 快速体验", new Vector2(0, -205), new Vector2(370, 54), OpenWorkshop, true);
        }

        private void BuildHud()
        {
            hud = FullPanel("BattleHUD", Color.clear, false);
            var top = Box(hud.transform, "TopBar", Vector2.zero, new Vector2(0, 86));
            top.anchorMin = new Vector2(0, 1); top.anchorMax = Vector2.one;
            top.pivot = new Vector2(0.5f, 1); top.anchoredPosition = Vector2.zero;
            Image(top, "Backdrop", new Color(0.025f, 0.04f, 0.07f, 0.95f), Vector2.zero, Vector2.zero, true);
            var health = Box(top, "Health", new Vector2(185, -43), new Vector2(300, 68));
            health.anchorMin = health.anchorMax = new Vector2(0, 1);
            healthText = Label(health, "HealthText", "生命 100 / 100", new Vector2(0, 12), new Vector2(300, 32), 20, Color.white, TextAnchor.MiddleLeft);
            var track = Image(health, "HealthTrack", Panel, new Vector2(0, -17), new Vector2(300, 7));
            healthFill = Image(track.transform, "Fill", Cyan, Vector2.zero, Vector2.zero, true).rectTransform;
            timerText = Label(top, "Timer", "03:00", new Vector2(0, -43), new Vector2(240, 70), 38, Color.white);
            timerText.rectTransform.anchorMin = timerText.rectTransform.anchorMax = new Vector2(0.5f, 1);
            killsText = Label(top, "Kills", "击杀 0", new Vector2(-160, -43), new Vector2(260, 60), 22, Cyan, TextAnchor.MiddleRight);
            killsText.rectTransform.anchorMin = killsText.rectTransform.anchorMax = Vector2.one;
            soulText = Label(hud.transform, "SoulExperience", "灵魂经验  0", new Vector2(165, 65), new Vector2(300, 56), 17, Cyan, TextAnchor.MiddleLeft);
            soulText.rectTransform.anchorMin = soulText.rectTransform.anchorMax = Vector2.zero;
            castingText = Label(hud.transform, "Casting", "火球术 · 自动施法", new Vector2(0, 61), new Vector2(600, 40), 20, new Color(1, 0.75f, 0.3f));
            castingText.rectTransform.anchorMin = castingText.rectTransform.anchorMax = new Vector2(0.5f, 0);
            var help = Label(hud.transform, "Controls", "WASD 移动    ·    鼠标瞄准    ·    按住空格吸魂（停止攻击）    ·    Q 终极 · ESC 暂停", new Vector2(0, 25), new Vector2(1100, 40), 17, Muted);
            help.rectTransform.anchorMin = help.rectTransform.anchorMax = new Vector2(0.5f, 0);
        }

        private void BuildPause()
        {
            pause = FullPanel("Pause", new Color(0.015f, 0.025f, 0.045f, 0.92f));
            Label(pause.transform, "Title", "行动暂停", new Vector2(0, 165), new Vector2(500, 75), 42, Color.white);
            resumeButton = Button(pause.transform, "Resume", "继续战斗", new Vector2(0, 50), new Vector2(380, 64), app.Resume, true);
            Button(pause.transform, "Settings", "设置", new Vector2(0, -30), new Vector2(380, 64), app.OpenSettings);
            Button(pause.transform, "Return", "中止并返回圣所", new Vector2(0, -110), new Vector2(380, 64), RequestAbort);
        }

        private void BuildResult()
        {
            result = FullPanel("Result", new Color(0.015f, 0.025f, 0.045f, 0.94f));
            resultTitle = Label(result.transform, "Title", "行动胜利", new Vector2(0, 140), new Vector2(650, 95), 54, Cyan);
            resultDescription = Label(result.transform, "Description", "", new Vector2(0, 45), new Vector2(650, 65), 24, Color.white);
            countdownText = Label(result.transform, "Countdown", "", new Vector2(0, -30), new Vector2(600, 45), 20, Muted);
            resultBackButton = Button(result.transform, "Return", "返回圣所", new Vector2(0, -125), new Vector2(380, 64), app.ReturnToBase, true);
        }

        private void BuildSettings()
        {
            settings = FullPanel("Settings", new Color(0.025f, 0.04f, 0.065f, 1));
            Label(settings.transform, "Title", "设置", new Vector2(0, 215), new Vector2(500, 80), 44, Color.white);
            volumeText = Label(settings.transform, "VolumeLabel", "", new Vector2(0, 112), new Vector2(460, 50), 22, Muted, TextAnchor.MiddleLeft);
            var sliderRect = Box(settings.transform, "Volume", new Vector2(0, 55), new Vector2(460, 42));
            sliderRect.gameObject.AddComponent<Image>().color = Color.clear;
            var slider = sliderRect.gameObject.AddComponent<Slider>();
            Image(sliderRect, "Track", Panel, Vector2.zero, new Vector2(460, 10));
            var fillArea = Box(sliderRect, "FillArea", Vector2.zero, new Vector2(440, 10));
            var fill = Image(fillArea, "Fill", Cyan, Vector2.zero, Vector2.zero, true);
            var handleArea = Box(sliderRect, "HandleArea", Vector2.zero, new Vector2(440, 42));
            var handle = Image(handleArea, "Handle", Color.white, Vector2.zero, new Vector2(18, 30));
            slider.fillRect = fill.rectTransform; slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle; slider.minValue = 0; slider.maxValue = 1;
            slider.SetValueWithoutNotify(app.Audio.Volume);
            volumeText.text = $"主音量  {Mathf.RoundToInt(slider.value * 100)}%";
            slider.onValueChanged.AddListener(value => { app.Audio.SetVolume(value); volumeText.text = $"主音量  {Mathf.RoundToInt(value * 100)}%"; });

            var toggleRect = Box(settings.transform, "Fullscreen", new Vector2(0, -30), new Vector2(460, 50));
            toggleRect.gameObject.AddComponent<Image>().color = Color.clear;
            var toggle = toggleRect.gameObject.AddComponent<Toggle>();
            var box = Image(toggleRect, "Box", Panel, new Vector2(-210, 0), new Vector2(32, 32));
            var check = Image(box.transform, "Check", Cyan, Vector2.zero, new Vector2(18, 18));
            toggle.targetGraphic = box; toggle.graphic = check;
            toggle.SetIsOnWithoutNotify(PlayerPrefs.GetInt("StarFall.Fullscreen", Screen.fullScreen ? 1 : 0) == 1);
            toggle.onValueChanged.AddListener(app.SetFullscreen);
            Label(toggleRect, "Label", "全屏（独立窗口生效）", new Vector2(30, 0), new Vector2(380, 50), 21, Color.white, TextAnchor.MiddleLeft);
            settingsBackButton = Button(settings.transform, "Back", "保存并返回", new Vector2(0, -150), new Vector2(460, 64), app.CloseSettings, true);
            Label(settings.transform, "Hint", "音量即时生效   ·   ESC 返回", new Vector2(0, -235), new Vector2(600, 42), 18, Muted);
        }

        public void ShowState(MatchState state)
        {
            if (state == MatchState.MainMenu) { displayedDraft = -1; shownRanks = -1; }
            RefreshTraining();
            homeBase.SetActive(state == MatchState.HomeBase);
            if (state == MatchState.HomeBase) { RefreshBase(); Select(deployButton); }
            menu.SetActive(state == MatchState.MainMenu);
            hud.SetActive(state != MatchState.MainMenu && state != MatchState.HomeBase && state != MatchState.ChoosingBuild);
            pause.SetActive(state == MatchState.Paused);
            result.SetActive(state == MatchState.Won || state == MatchState.Lost);
            buildSelection.SetActive(state == MatchState.ChoosingBuild);
            if (state == MatchState.ChoosingBuild) RefreshBuildChoices();
            if (state == MatchState.MainMenu) Select(newGameButton);
            else if (state == MatchState.Paused) Select(resumeButton);
            else if (state == MatchState.Won || state == MatchState.Lost) Select(resultBackButton);
            else if (state != MatchState.HomeBase && EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            if (app.SaveBlocked) { saveError.transform.SetAsLastSibling(); Select(saveError.GetComponentInChildren<Button>()); }
        }

        public void SetSettingsVisible(bool visible)
        {
            settings.SetActive(visible);
            if (visible) { settings.transform.SetAsLastSibling(); Select(settingsBackButton); }
        }

        public void Refresh(MatchSession session, float resultRemaining)
        {
            RefreshTraining();
            if (session.State == MatchState.MainMenu || session.State == MatchState.HomeBase) return;
            healthText.text = $"生命  {session.Health:0.#} / {session.MaxHealth:0.#}";
            soulText.fontSize = 17;
            soulText.text = (session.Builds.ExperienceRequired == 0 ? $"Lv.{session.Builds.Level}  构筑已满" : $"Lv.{session.Builds.Level}  经验 {session.Builds.Experience}/{session.Builds.ExperienceRequired}") + $"\n本局灵魂收益 {session.SoulExperience}";
            RefreshBuildHud(session);
            castingText.text = app.Battle.Player.IsAbsorbing ? $"吸收中 · 半径 {app.Battle.CollectionRadius:0.##} · 停止攻击" : app.Battle.ActiveGem.attackName + " · 自动施法";
            if (session.FinalBossDefeated) castingText.text = app.Battle.EncounterObjective;
            else if (!app.Battle.Player.IsAbsorbing && session.Builds.Resonance != DamageElement.Physical)
                castingText.text += " · " + ElementCatalog.ResonanceName(app.Battle.ActiveGem.element, session.Builds.Resonance);
            castingText.color = app.Battle.Player.IsAbsorbing ? Cyan : new Color(1, 0.75f, 0.3f);
            healthFill.anchorMax = new Vector2((float)session.Health / session.MaxHealth, 1);
            healthFill.GetComponent<Image>().color = session.Health <= session.MaxHealth * 0.3f ? Coral : Cyan;
            int seconds = Mathf.CeilToInt(session.Remaining);
            timerText.fontSize = 24;
            timerText.text = app.Battle.TrainingShowcase ? "组合试打" : session.FinalBossDefeated ? "领取通关之证" : seconds > 0 ? $"关底 {seconds / 60:00}:{seconds % 60:00}" : "击败关底 Boss";
            killsText.fontSize = 18;
            killsText.text = $"击杀 {session.Kills}\n{(app.Battle.TrainingShowcase ? "F 充能 · G 重置怪群" : session.FinalBossDefeated ? "关底已击败" : app.Battle.EncounterObjective)}";
            bool won = session.State == MatchState.Won;
            resultTitle.text = won ? "行动胜利" : "行动失败";
            resultTitle.color = won ? Cyan : Coral;
            resultDescription.text = won ? $"获得通关之证 ×1   /   击杀 {session.Kills}" : $"生存 {Mathf.FloorToInt(session.Elapsed)} 秒   /   击杀 {session.Kills}";
            resultDescription.text += app.TestMode ? "   /   测试不入库" : $"   /   灵魂 +{session.SoulExperience}";
            countdownText.text = app.TestMode ? "R 重开 / 1～5 换宝石 / 返回圣所" : $"{Mathf.Max(0, Mathf.CeilToInt(resultRemaining))} 秒后返回圣所";
        }

        private static void Select(Button button)
        {
            if (EventSystem.current == null) return;
            EventSystem.current.SetSelectedGameObject(null);
            button.Select();
        }

        private GameObject FullPanel(string name, Color color, bool raycast = true)
        {
            var rect = Box(canvas, name, Vector2.zero, Vector2.zero);
            Stretch(rect);
            var background = rect.gameObject.AddComponent<Image>();
            background.color = color; background.raycastTarget = raycast;
            return rect.gameObject;
        }

        private static RectTransform Box(Transform parent, string name, Vector2 position, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size; rect.anchoredPosition = position;
            return rect;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private static Image Image(Transform parent, string name, Color color, Vector2 position, Vector2 size, bool stretch = false)
        {
            var rect = Box(parent, name, position, size);
            if (stretch) Stretch(rect);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private Text Label(Transform parent, string name, string content, Vector2 position, Vector2 size, int fontSize, Color color, TextAnchor alignment = TextAnchor.MiddleCenter)
        {
            var rect = Box(parent, name, position, size);
            var text = rect.gameObject.AddComponent<Text>();
            text.font = font; text.text = content; text.fontSize = fontSize;
            text.color = color; text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            return text;
        }

        private Button Button(Transform parent, string name, string caption, Vector2 position, Vector2 size, UnityAction action, bool primary = false)
        {
            var image = Image(parent, name, primary ? Cyan : Panel, position, size);
            image.raycastTarget = true;
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.highlightedColor = new Color(0.8f, 0.95f, 1);
            colors.selectedColor = new Color(0.8f, 0.95f, 1);
            colors.pressedColor = new Color(0.55f, 0.7f, 0.8f);
            button.colors = colors;
            button.onClick.AddListener(action);
            Label(image.transform, "Caption", caption, Vector2.zero, size - new Vector2(20, 4), 23, primary ? Background : Color.white);
            return button;
        }
    }
}
