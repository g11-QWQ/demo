using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace StarFall
{
    public sealed partial class GameUI
    {
        private GameObject buildSelection;
        private readonly Button[] buildButtons = new Button[3];
        private readonly Text[] buildTitles = new Text[3];
        private readonly Text[] buildDescriptions = new Text[3];
        private readonly Text[] buildRoutes = new Text[3];
        private readonly Text[] buildRanks = new Text[3];
        private Text draftTitle, draftHint, draftOwned, buildStatus, pauseBuilds;
        private RectTransform experienceFill;
        private int displayedDraft = -1;
        private int shownRanks = -1;
        private string ownedSummary = "尚未获得构筑";
        private float unlockChoicesAt;
        private bool choiceInputArmed;
        public bool BuildSelectionVisible => buildSelection != null && buildSelection.activeSelf;
        public bool ChoicesArmed => choiceInputArmed;
        public Button GetBuildButton(int index) => buildButtons[index];

        private void BuildSelectionPanel()
        {
            buildSelection = FullPanel("BuildSelection", new Color(0.025f, 0.035f, 0.06f, 0.98f));
            draftTitle = Label(buildSelection.transform, "Title", "灵魂觉醒", new Vector2(0, 272), new Vector2(1100, 70), 40, Cyan);
            draftHint = Label(buildSelection.transform, "Hint", "战斗已暂停 · 选择一项强化", new Vector2(0, 212), new Vector2(1100, 45), 21, Muted);
            for (int i = 0; i < 3; i++)
            {
                var button = Button(buildSelection.transform, "BuildChoice" + i, "", new Vector2((i - 1) * 340, -10), new Vector2(316, 350), () => { });
                buildButtons[i] = button;
                buildRoutes[i] = Label(button.transform, "Route", "", new Vector2(0, 135), new Vector2(280, 36), 17, Cyan);
                buildTitles[i] = Label(button.transform, "Name", "", new Vector2(0, 76), new Vector2(290, 60), 30, Color.white);
                buildDescriptions[i] = Label(button.transform, "Description", "", new Vector2(0, -20), new Vector2(280, 152), 18, Color.white);
                buildRanks[i] = Label(button.transform, "Rank", "", new Vector2(0, -137), new Vector2(280, 40), 20, Cyan);
            }
            draftOwned = Label(buildSelection.transform, "Owned", "", new Vector2(0, -245), new Vector2(1100, 80), 18, Muted);
            Label(buildSelection.transform, "Controls", "鼠标选择 / 方向键切换 + Enter 确认", new Vector2(0, -316), new Vector2(1100, 36), 16, Muted);
            buildStatus = Label(hud.transform, "BuildStatus", "", new Vector2(-163, 61), new Vector2(290, 40), 18, Cyan, TextAnchor.MiddleRight);
            buildStatus.rectTransform.anchorMin = buildStatus.rectTransform.anchorMax = new Vector2(1, 0);
            var experienceTrack = Image(hud.transform, "ExperienceTrack", Panel, new Vector2(165, 95), new Vector2(300, 4));
            experienceTrack.rectTransform.anchorMin = experienceTrack.rectTransform.anchorMax = Vector2.zero;
            experienceFill = Image(experienceTrack.transform, "Fill", Cyan, Vector2.zero, Vector2.zero, true).rectTransform;
            pauseBuilds = Label(pause.transform, "OwnedBuilds", "", new Vector2(405, 0), new Vector2(300, 470), 20, Muted);
            buildSelection.SetActive(false);
        }

        private void Update()
        {
            if (app.SaveBlocked || !BuildSelectionVisible || choiceInputArmed) return;
            var keys = Keyboard.current;
            bool held = (Mouse.current != null && Mouse.current.leftButton.isPressed)
                || (keys != null && (keys.spaceKey.isPressed || keys.enterKey.isPressed || keys.numpadEnterKey.isPressed));
            if (held) { draftHint.text = "松开空格、确认键或鼠标，再选择强化"; return; }
            if (Time.unscaledTime < unlockChoicesAt) return;
            choiceInputArmed = true;
            draftHint.text = DraftHint();
            foreach (var button in buildButtons) button.interactable = button.gameObject.activeSelf;
            if (app.Session.Builds.Offers.Count > 0) Select(buildButtons[0]);
        }

        private void RefreshBuildChoices()
        {
            var builds = app.Session.Builds;
            if (displayedDraft == builds.Revision) return;
            displayedDraft = builds.Revision;
            choiceInputArmed = false;
            unlockChoicesAt = Time.unscaledTime + 0.2f;
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            draftTitle.text = $"{(builds.ChoiceLevel % 3 == 0 ? "形态突破" : "灵魂强化")} · Lv.{builds.ChoiceLevel}";
            draftHint.text = DraftHint();
            for (int i = 0; i < buildButtons.Length; i++)
            {
                var button = buildButtons[i];
                bool active = i < builds.Offers.Count;
                button.gameObject.SetActive(active);
                button.interactable = false;
                button.onClick.RemoveAllListeners();
                if (!active) continue;
                // Center smaller drafts when the remaining card pool has fewer than three kinds.
                button.GetComponent<RectTransform>().anchoredPosition = new Vector2((i - (builds.Offers.Count - 1) * 0.5f) * 340, -10);
                var id = builds.Offers[i];
                int revision = builds.Revision;
                var definition = AttackLinkCatalog.Present(id, builds);
                buildRoutes[i].text = definition.Route;
                buildTitles[i].text = definition.Name;
                buildDescriptions[i].text = definition.Description;
                buildRanks[i].text = definition.MaxRank == 1 ? "" : $"{builds.Rank(id)} → {builds.Rank(id) + 1} 级";
                button.onClick.AddListener(() =>
                {
                    if (!choiceInputArmed) return;
                    choiceInputArmed = false;
                    foreach (var card in buildButtons) card.interactable = false;
                    app.ChooseBuild(id, revision);
                });
            }
            UpdateOwnedSummary();
            draftOwned.text = "当前构筑\n" + ownedSummary;
        }

        private void UpdateOwnedSummary()
        {
            var builds = app.Session.Builds;
            if (shownRanks == builds.TotalRanks) return;
            shownRanks = builds.TotalRanks;
            var text = new StringBuilder();
            foreach (var definition in BuildCatalog.All)
            {
                int rank = builds.Rank(definition.Id);
                if (rank <= 0) continue;
                if (text.Length > 0) text.Append("   ·   ");
                text.Append(AttackLinkCatalog.Present(definition.Id, builds).Name).Append(" ").Append(rank).Append("级");
            }
            ownedSummary = text.Length == 0 ? "尚未获得构筑" : text.ToString();
        }

        private string DraftHint() => app.Session.Builds.PendingChoices > 1
            ? $"战斗已暂停 · 还有 {app.Session.Builds.PendingChoices} 次选择，逐次确认后继续"
            : app.Session.Builds.ChoiceLevel == 12 ? "战斗已暂停 · 解锁即满能量，按 Q 释放"
            : "战斗已暂停 · 每级伤害 +9%、攻速 +2.5%";

        private void RefreshBuildHud(MatchSession session)
        {
            if (session.State == MatchState.ChoosingBuild) RefreshBuildChoices();
            UpdateOwnedSummary();
            int needed = session.Builds.ExperienceRequired;
            experienceFill.anchorMax = new Vector2(needed == 0 ? 1 : Mathf.Clamp01((float)session.Builds.Experience / needed), 1);
            buildStatus.text = $"构筑 {session.Builds.DistinctBuilds}项" + (session.Builds.Rank(BuildId.SoulFire) > 0 ? $" · 魂火 {session.SoulFireStacks}/5" : "");
            if (session.State == MatchState.Paused)
                pauseBuilds.text = "当前构筑\n\n" + ownedSummary.Replace("   ·   ", "\n");
        }
    }
}
