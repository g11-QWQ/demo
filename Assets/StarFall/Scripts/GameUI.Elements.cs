using UnityEngine;
using UnityEngine.UI;

namespace StarFall
{
    public sealed partial class GameUI
    {
        private GameObject workshop, trainingBar;
        private Text workshopStatus, trainingHint;
        private Text fusionText;
        private RectTransform fusionFill;
        private GameObject fusionHud;
        private readonly Button[] gemButtons = new Button[5];
        private readonly Text[] gemCaptions = new Text[5];
        private readonly Button[] resonanceButtons = new Button[6];
        private readonly Text[] resonanceCaptions = new Text[6];
        public bool WorkshopVisible => workshop != null && workshop.activeSelf;
        public Button GemButton(int index) => gemButtons[index];
        private void BuildWorkshop()
        {
            workshop = FullPanel("GemWorkshop", Background);
            Label(workshop.transform, "Title", "宝石整备 · 元素共鸣原型", new Vector2(0, 295), new Vector2(1150, 60), 34, Color.white);
            for (int i = 0; i < 5; i++)
            {
                int index = i; var element = (DamageElement)(i + 1);
                gemButtons[i] = Button(workshop.transform, "Gem" + i, "", new Vector2(-400, 218 - i * 52), new Vector2(320, 46), () => app.SelectGem(index));
                gemCaptions[i] = Label(gemButtons[i].transform, "Element", "", Vector2.zero, new Vector2(290, 44), 23, ElementCatalog.ColorFor(element));
                Label(workshop.transform, "Spell" + i, ElementCatalog.Spells[i + 1] + "  ·  " + ElementCatalog.Descriptions[i + 1], new Vector2(180, 218 - i * 52), new Vector2(780, 46), 20, Color.white, TextAnchor.MiddleLeft);
            }
            Label(workshop.transform, "ResonanceTitle", "试炼预装共鸣（正式对局通过升级获得）", new Vector2(0, -34), new Vector2(1120, 40), 20, Muted);
            for (int i = 0; i < 6; i++)
            {
                int index = i;
                resonanceButtons[i] = Button(workshop.transform, "Resonance" + i, "", new Vector2(-475 + i * 190, -78), new Vector2(178, 46), () => { app.TestResonance = (DamageElement)index; RefreshWorkshop(); });
                resonanceCaptions[i] = Label(resonanceButtons[i].transform, "Name", "", Vector2.zero, new Vector2(170, 44), 18, i == 0 ? Color.white : ElementCatalog.ColorFor((DamageElement)i));
            }
            for (int i = 0; i < FusionCatalog.Primary.Length; i++)
            {
                int index = i;
                var kind = FusionCatalog.Get(FusionCatalog.Primary[i], FusionCatalog.Secondary[i]);
                var caption = FusionCatalog.Name(kind) + "\n" + ElementCatalog.Names[(int)FusionCatalog.Primary[i]] + "＋" + ElementCatalog.Names[(int)FusionCatalog.Secondary[i]] + " · 试打";
                var demo = Button(workshop.transform, "FusionDemo" + i, caption, new Vector2(-464 + (i % 5) * 232, -143 - (i / 5) * 57), new Vector2(220, 50), () => app.StartShowcase(index));
                demo.GetComponentInChildren<Text>().fontSize = 17;
            }
            workshopStatus = Label(workshop.transform, "Status", "", new Vector2(0, -245), new Vector2(1150, 48), 17, Cyan);
            Button(workshop.transform, "Close", "返回", new Vector2(-400, -306), new Vector2(330, 58), () => CloseWorkshop());
            Button(workshop.transform, "Trial", "60秒快速试炼", new Vector2(0, -306), new Vector2(360, 58), () => app.StartTraining(), true);
            Button(workshop.transform, "BossTrial", "直接挑战 Boss", new Vector2(400, -306), new Vector2(330, 58), () => app.StartTraining(true));
            workshop.SetActive(false);

            trainingBar = FullPanel("TrainingToolbar", Color.clear, false);
            for (int i = 0; i < 5; i++)
            {
                int index = i; TrainingButton(ElementCatalog.Names[i + 1] + " " + (i + 1), i, () => app.RestartTraining(index));
            }
            TrainingButton("升级 U", 5, app.TrainingUpgrade);
            TrainingButton("Boss B", 6, () => app.Battle.TrainingBoss());
            TrainingButton("无敌 I", 7, app.ToggleTrainingInvulnerability);
            TrainingButton("共鸣 C", 8, app.CycleTrainingResonance);
            TrainingButton("重开 R", 9, () => app.RestartTraining());
            TrainingButton("返回圣所", 10, app.ReturnToBase);
            var charge = Button(trainingBar.transform, "ChargeFusion", "充能 F", new Vector2(-520, -150), new Vector2(98, 34), () => app.Battle.TrainingChargeFusion());
            charge.GetComponent<RectTransform>().anchorMin = charge.GetComponent<RectTransform>().anchorMax = new Vector2(0.5f, 1); charge.GetComponentInChildren<Text>().fontSize = 17;
            var targets = Button(trainingBar.transform, "SpawnTargets", "怪群 G", new Vector2(-416, -150), new Vector2(98, 34), () => app.Battle.TrainingTargets());
            targets.GetComponent<RectTransform>().anchorMin = targets.GetComponent<RectTransform>().anchorMax = new Vector2(0.5f, 1); targets.GetComponentInChildren<Text>().fontSize = 17;
            trainingHint = Label(trainingBar.transform, "Hint", "", new Vector2(180, -150), new Vector2(800, 40), 15, Cyan);
            trainingHint.rectTransform.anchorMin = trainingHint.rectTransform.anchorMax = new Vector2(0.5f, 1);
            trainingBar.SetActive(false);
            fusionHud = Box(hud.transform, "FusionCharge", new Vector2(0, 97), new Vector2(650, 40)).gameObject;
            var fusionRect = fusionHud.GetComponent<RectTransform>(); fusionRect.anchorMin = fusionRect.anchorMax = new Vector2(0.5f, 0);
            fusionText = Label(fusionHud.transform, "Name", "", new Vector2(0, -3), new Vector2(650, 30), 17, Cyan);
            var track = Image(fusionHud.transform, "Track", Panel, new Vector2(0, 17), new Vector2(320, 5));
            fusionFill = Image(track.transform, "Fill", Cyan, Vector2.zero, Vector2.zero, true).rectTransform;
            fusionHud.SetActive(false);
        }
        private void TrainingButton(string text, int index, UnityEngine.Events.UnityAction action)
        {
            var button = Button(trainingBar.transform, "Training" + index, text, new Vector2(-520 + index * 104, -109), new Vector2(98, 36), action);
            var rect = button.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1);
            button.GetComponentInChildren<Text>().fontSize = 17;
        }
        public void OpenWorkshop()
        {
            if (app.SaveBlocked || app.SettingsOpen || (app.Session.State != MatchState.MainMenu && app.Session.State != MatchState.HomeBase)) return;
            RefreshWorkshop(); workshop.SetActive(true); workshop.transform.SetAsLastSibling(); Select(gemButtons[app.Progress.Snapshot.gemIndex]);
        }
        public bool CloseWorkshop()
        {
            if (!WorkshopVisible) return false;
            workshop.SetActive(false); ShowState(app.Session.State); return true;
        }
        public void RefreshWorkshop()
        {
            if (workshop == null || !app.Progress.Ready) return;
            int selected = app.Progress.Snapshot.gemIndex; var element = (DamageElement)(selected + 1);
            for (int i = 0; i < 5; i++) gemCaptions[i].text = ElementCatalog.Names[i + 1] + "宝石" + (selected == i ? " · 已选" : "");
            for (int i = 0; i < 6; i++)
            {
                bool available = i == 0 || ElementCatalog.Compatible(element, (DamageElement)i);
                resonanceButtons[i].interactable = available;
                resonanceCaptions[i].text = (i == 0 ? "不装共鸣" : ElementCatalog.Names[i] + "共鸣") + (app.TestResonance == (DamageElement)i ? " ✓" : available ? "" : " · 同元素");
            }
            workshopStatus.text = !string.IsNullOrEmpty(app.Progress.Error) ? app.Progress.Error : ElementCatalog.ResonanceName(element, app.TestResonance) + " · 试炼使用基础属性，默认无敌；不产生永久收益。";
        }
        private void RefreshTraining()
        {
            if (fusionHud != null)
            {
                bool combined = app.Battle.CurrentFusion != FusionKind.None && (app.Session.State == MatchState.Playing || app.Session.State == MatchState.Paused);
                fusionHud.SetActive(combined);
                if (combined)
                {
                    fusionFill.anchorMax = new Vector2(app.Battle.FusionEnergy / 100, 1);
                    fusionText.text = FusionCatalog.Name(app.Battle.CurrentFusion) + (app.Battle.FusionEnergy >= 99.99f ? " · 就绪：瞄准场内施法释放" : $" · 能量 {app.Battle.FusionEnergy:0}%");
                }
            }
            bool show = app.TestMode && app.Session.State != MatchState.ChoosingBuild && app.Session.State != MatchState.MainMenu && app.Session.State != MatchState.HomeBase;
            trainingBar.SetActive(show);
            if (show) trainingHint.text = "测试模式 · 无永久收益 · " + app.Battle.ActiveGem.displayName + " / " + ElementCatalog.ResonanceName(app.Battle.ActiveGem.element, app.Session.Builds.Resonance)
                + (app.Session.Invulnerable ? " · 无敌开启" : " · 正常受伤") + " · 切换宝石或共鸣将重开本局";
            if (show && app.Battle.TrainingShowcase && app.Battle.CurrentFusion != FusionKind.None)
                trainingHint.text = "试打 · 无永久收益 · " + FusionCatalog.Name(app.Battle.CurrentFusion) + "：" + FusionCatalog.Descriptions[(int)app.Battle.CurrentFusion - 1]
                    + (app.Session.Invulnerable ? " · 无敌" : " · 正常受伤");
        }
    }
}

