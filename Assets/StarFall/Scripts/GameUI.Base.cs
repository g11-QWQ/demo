using System;
using UnityEngine;
using UnityEngine.UI;

namespace StarFall
{
    public sealed partial class GameUI
    {
        private GameObject homeBase, confirmation, saveError;
        private Text wallet, report, baseNotice, confirmationText, saveErrorText;
        private readonly Text[] researchText = new Text[3];
        private readonly Button[] researchButtons = new Button[3];
        private readonly Button[] staffButtons = new Button[2];
        private readonly Text[] staffCaptions = new Text[2];
        private Button deployButton, confirmButton, cancelButton;
        private Action confirmedAction;
        public bool BaseVisible => homeBase != null && homeBase.activeSelf;
        public Button DeployButton => deployButton;
        public Button ResearchButton(int index) => researchButtons[index];
        public Button ConfirmButton => confirmButton;
        public bool SaveErrorVisible => saveError != null && saveError.activeSelf;

        private void BuildBase()
        {
            homeBase = FullPanel("Sanctuary", Background);
            Label(homeBase.transform, "Title", "星陨圣所", new Vector2(-380, 280), new Vector2(440, 65), 38, Color.white, TextAnchor.MiddleLeft);
            wallet = Label(homeBase.transform, "Wallet", "", new Vector2(300, 280), new Vector2(570, 65), 24, Cyan, TextAnchor.MiddleRight);
            Label(homeBase.transform, "Loadout", "法杖整备 · 单宝石槽 · 切换免费", new Vector2(-320, 205), new Vector2(550, 45), 21, Muted, TextAnchor.MiddleLeft);
            for (int i = 0; i < 2; i++)
            {
                int index = i;
                staffButtons[i] = Button(homeBase.transform, "Staff" + i, "", new Vector2(-320, 102 - i * 150), new Vector2(550, 130), () => app.EquipStaff(index));
                staffCaptions[i] = Label(staffButtons[i].transform, "Stats", "", Vector2.zero, new Vector2(510, 116), 20, Color.white);
            }
            Label(homeBase.transform, "Research", "永久研习 · 下次出击生效", new Vector2(310, 205), new Vector2(550, 45), 21, Muted, TextAnchor.MiddleLeft);
            for (int i = 0; i < 3; i++)
            {
                int index = i;
                researchButtons[i] = Button(homeBase.transform, "Research" + i, "", new Vector2(310, 125 - i * 112), new Vector2(550, 96), () => RequestResearch(index));
                researchText[i] = Label(researchButtons[i].transform, "Description", "", Vector2.zero, new Vector2(515, 88), 20, Color.white);
            }
            Button(homeBase.transform, "GemWorkshop", "选择宝石 / 共鸣试验", new Vector2(-320, -140), new Vector2(550, 42), OpenWorkshop);
            report = Label(homeBase.transform, "Report", "", new Vector2(-320, -210), new Vector2(550, 62), 18, Cyan, TextAnchor.MiddleLeft);
            baseNotice = Label(homeBase.transform, "Notice", "", new Vector2(0, -260), new Vector2(1160, 54), 17, Muted);
            deployButton = Button(homeBase.transform, "Deploy", "进入战场", new Vector2(365, -306), new Vector2(420, 58), app.Deploy, true);
            Button(homeBase.transform, "Menu", "返回主菜单", new Vector2(-395, -306), new Vector2(380, 58), app.ReturnToMenu);
            Button(homeBase.transform, "Settings", "设置", new Vector2(-60, -306), new Vector2(220, 58), app.OpenSettings);
            homeBase.SetActive(false);

            confirmation = FullPanel("ConfirmPurchase", new Color(0.025f, 0.04f, 0.07f, 0.98f));
            confirmationText = Label(confirmation.transform, "Question", "", new Vector2(0, 80), new Vector2(900, 200), 28, Color.white);
            confirmButton = Button(confirmation.transform, "Confirm", "确认", new Vector2(220, -110), new Vector2(380, 64), () =>
            {
                var action = confirmedAction; confirmedAction = null;
                confirmation.SetActive(false); action?.Invoke();
            });
            cancelButton = Button(confirmation.transform, "Cancel", "取消", new Vector2(-220, -110), new Vector2(380, 64), () => CancelConfirmation(), true);
            confirmation.SetActive(false);
            saveError = FullPanel("SaveError", new Color(0.025f, 0.04f, 0.07f, 1));
            saveErrorText = Label(saveError.transform, "Error", "", new Vector2(0, 60), new Vector2(1000, 260), 24, Color.white);
            Button(saveError.transform, "Retry", "重试保存 / 读取", new Vector2(0, -150), new Vector2(420, 64), app.RetrySave, true);
            saveError.SetActive(false);
        }

        public void RefreshBase()
        {
            var data = app.Progress.Snapshot;
            if (data == null) return;
            wallet.text = $"灵魂 {data.souls}    /    通关之证 {data.proofs}";
            for (int i = 0; i < 2; i++)
            {
                staffCaptions[i].text = PermanentRules.StaffNames[i] + (data.staff == i ? "  ·  已装备" : "  ·  点击装备")
                    + (i == 0 ? "\n基础：全元素100% · 吸魂225% · 移速100%" : "\n基础：全元素85% · 吸魂300% · 移速100%") + "\n镶嵌：" + ElementCatalog.Names[data.gemIndex + 1] + "宝石";
                staffButtons[i].interactable = data.staff != i;
            }
            for (int i = 0; i < 3; i++)
            {
                int rank = data.ranks[i], cost = PermanentRules.Cost(rank);
                int next = Mathf.Min(rank + 1, PermanentRules.MaxRank);
                var config = app.Battle.Config;
                string effect = i == 0 ? $"出击生命 {config.playerHealth + rank * 10:0} → {config.playerHealth + next * 10:0}"
                    : i == 1 ? $"全元素研习加成 {rank * 5}% → {next * 5}%"
                    : $"角色基础吸魂半径 {config.PassiveSoulRadius + rank * 0.2f:0.0} → {config.PassiveSoulRadius + next * 0.2f:0.0}";
                researchText[i].fontSize = 18;
                researchText[i].text = $"{PermanentRules.Names[i]}  Lv.{rank}/{PermanentRules.MaxRank}\n{effect}\n"
                    + (rank == PermanentRules.MaxRank ? "已满级" : $"下一级：{cost} 灵魂" + (data.souls < cost ? $"（还差 {cost - data.souls}）" : " · 点击研习"));
                researchButtons[i].interactable = rank < PermanentRules.MaxRank && data.souls >= cost;
            }
            report.text = $"上次行动：{data.lastOutcome}\n灵魂收益 +{data.lastSouls}" + (data.lastProof ? "    通关之证 +1" : "");
            baseNotice.text = !string.IsNullOrEmpty(app.Progress.Error) ? app.Progress.Error : !string.IsNullOrEmpty(app.Progress.Notice) ? app.Progress.Notice : "拾取灵魂同时用于局内升级和局后货币；胜利、阵亡、中止均保留已收集灵魂。";
        }
        private void RequestResearch(int index)
        {
            if (app.Session.State != MatchState.HomeBase) return;
            var data = app.Progress.Snapshot; int rank = data.ranks[index];
            ShowConfirmation($"{PermanentRules.Names[index]}  Lv.{rank} → Lv.{rank + 1}\n花费 {PermanentRules.Cost(rank)} 灵魂\n{PermanentRules.Descriptions[index]}", () => app.PurchaseResearch((ResearchId)index, rank));
        }
        private void ShowConfirmation(string text, Action action)
        {
            confirmedAction = action; confirmationText.text = text;
            confirmation.SetActive(true); confirmation.transform.SetAsLastSibling(); Select(cancelButton);
        }
        public bool CancelConfirmation()
        {
            if (!confirmation.activeSelf) return false;
            confirmation.SetActive(false); confirmedAction = null; ShowState(app.Session.State); return true;
        }
        public void RequestAbort() => ShowConfirmation(app.TestMode ? "结束测试并返回圣所？\n测试收益不会入库。" : "中止本次行动并返回圣所？\n已收集的灵魂全部保留，局内构筑清空。", app.ReturnToBase);
        public void ShowSaveError(string error)
        {
            saveErrorText.text = "存档未完成\n\n" + error + (app.Progress.Ready ? "\n\n当前进度保留在内存中，重试成功后继续。" : "\n\n未覆盖已有存档，读取成功后继续。");
            saveError.SetActive(true); saveError.transform.SetAsLastSibling();
            Select(saveError.GetComponentInChildren<Button>());
        }
        public void HideSaveError() => saveError.SetActive(false);
    }
}



