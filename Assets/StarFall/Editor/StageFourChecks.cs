using System;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

namespace StarFall.Editor
{
    public static class StageFourChecks
    {
        public static void Run(GameApplication app, Action<bool, string> check, Action<GameApplication, string> capture, Action<Button> click)
        {
            CheckStore(check);
            check(app.UI.BaseVisible && app.Progress.Ready, "Base loads after result with a valid persistent profile");
            var starting = app.Progress.Snapshot;
            check(starting.souls >= 100, "Collected souls reach base wallet");
            check(starting.proofs >= 1, "Victory proof is stored independently from soul currency");
            app.OpenSettings(); check(app.SettingsOpen, "Base can open settings"); app.CloseSettings();
            click(app.UI.ResearchButton(0)); app.UI.CancelConfirmation();
            check(app.Progress.Snapshot.ranks[0] == 0 && app.Progress.Snapshot.souls == starting.souls, "Cancelling purchase leaves wallet and rank unchanged");
            capture(app, "16-sanctuary");
            click(app.UI.ResearchButton(0));
            capture(app, "17-research-confirmation");
            check(app.Progress.Snapshot.souls == starting.souls, "Opening confirmation does not debit wallet");
            click(app.UI.ConfirmButton);
            app.UI.ConfirmButton.onClick.Invoke();
            check(app.Progress.Snapshot.ranks[0] == 1 && app.Progress.Snapshot.souls == starting.souls - 20, "Real research confirmation debits once and upgrades once");
            app.PurchaseResearch(ResearchId.Elements, 0);
            app.PurchaseResearch(ResearchId.Collection, 0);
            app.EquipStaff(1);
            var reloaded = new ProgressStore(app.Progress.Path);
            check(reloaded.Snapshot.staff == 1 && reloaded.Snapshot.ranks[0] == 1 && reloaded.Snapshot.ranks[1] == 1 && reloaded.Snapshot.ranks[2] == 1, "Equipment and research survive disk reload");
            capture(app, "18-permanent-growth");
            click(app.UI.DeployButton);
            check(app.Session.Health == 110 && Mathf.Abs(app.Battle.FireEfficiency - 0.9f) < 0.001f && Mathf.Abs(app.Battle.ActiveSoulRadius - 9.6f) < 0.001f, "Deployment applies character research and equipped staff snapshot");
            app.EquipStaff(0); app.PurchaseResearch(ResearchId.Vitality, 1);
            check(app.Progress.Snapshot.staff == 1 && app.Progress.Snapshot.ranks[0] == 1, "Combat cannot mutate equipment or permanent research");
            app.Battle.Step(0.36f, Vector2.zero, null, true);
            var enemy = app.Battle.Enemies[0];
            enemy.transform.position = new Vector3(0, 0.6f, 4);
            float enemyHealth = enemy.Health;
            app.Battle.Step(0.3f, Vector2.zero, Vector3.forward * 10);
            check(Mathf.Abs(enemy.Health - (enemyHealth - 7.2f)) < 0.001f, "Permanent efficiency and selected staff affect actual fireball damage");
            long wallet = app.Progress.Snapshot.souls;
            app.Session.CollectSoul(2);
            check(app.Progress.Snapshot.activeSouls == 2 && app.Progress.Snapshot.souls == wallet && app.Session.Builds.Experience == 2, "Pickup checkpoints currency without consuming local experience");
            app.Progress.BeforeWriteForTests = () => throw new IOException("Injected checkpoint failure");
            app.Session.CollectSoul(1);
            check(app.SaveBlocked && app.UI.SaveErrorVisible && app.Progress.Snapshot.activeSouls == 2, "Failed checkpoint blocks game and preserves previous persistent state");
            capture(app, "19-save-retry");
            app.ReturnToMenu(); check(app.Session.State != MatchState.MainMenu, "Save failure cannot discard pending battle earnings");
            app.Progress.BeforeWriteForTests = null;
            app.RetrySave();
            check(!app.SaveBlocked && app.Progress.Snapshot.activeSouls == 3 && app.Session.State == MatchState.ChoosingBuild, "Retry saves accumulated souls and preserves pending build choice");
            app.ChooseBuild(app.Session.Builds.Offers[0], app.Session.Builds.Revision);
            app.Session.Pause();
            app.UI.RequestAbort();
            click(app.UI.ConfirmButton);
            check(app.UI.BaseVisible && app.Progress.Snapshot.souls == wallet + 3 && app.Progress.Snapshot.lastOutcome == "主动中止", "Confirmed abort returns collected currency to base");
            app.ReturnToMenu(); app.NewGame(); app.Deploy();
            check(app.Session.SoulExperience == 0 && app.Session.Builds.TotalRanks == 0 && app.Session.MaxHealth == 110, "New action clears local growth and retains permanent growth");
            app.Session.CollectSoul(2);
            app.Progress.BeforeWriteForTests = () => throw new IOException("Injected result failure");
            app.Session.Damage(10000);
            check(app.SaveBlocked && app.Progress.Snapshot.souls == wallet + 3, "Failed defeat settlement does not publish uncommitted earnings");
            app.Progress.BeforeWriteForTests = null; app.RetrySave(); app.ReturnToBase();
            check(app.UI.BaseVisible && app.Progress.Snapshot.souls == wallet + 5 && app.Progress.Snapshot.lastOutcome == "阵亡", "Defeat retry credits exactly once and returns to base");
            app.ReturnToBase(); check(app.Progress.Snapshot.souls == wallet + 5, "Repeated return cannot settle again");
            app.Deploy(); app.Session.CollectSoul(2); app.Session.Pause();
            app.Progress.BeforeWriteForTests = () => throw new IOException("Injected abort failure");
            app.ReturnToBase();
            check(app.SaveBlocked && app.Session.State == MatchState.Paused, "Failed abort stays frozen until earnings are saved");
            app.Progress.BeforeWriteForTests = null; app.RetrySave();
            check(app.UI.BaseVisible && app.Session.State == MatchState.HomeBase && app.Progress.Snapshot.souls == wallet + 7 && app.Progress.Snapshot.activeRunId == "", "Retry completes requested abort instead of resuming a settled battle");
            capture(app, "20-base-after-action");
        }

        private static void CheckStore(Action<bool, string> check)
        {
            string folder = Path.GetFullPath("Logs/Checks/StoreTests/" + Guid.NewGuid().ToString("N"));
            string path = Path.Combine(folder, "progress.json");
            var store = new ProgressStore(path);
            check(store.Ready && store.Snapshot.souls == 0, "First run creates a zero-currency profile");
            check(!store.Upgrade(ResearchId.Vitality, 0), "Insufficient currency cannot purchase research");
            check(store.BeginRun(out string id) && !store.BeginRun(out _), "Only one active action may exist");
            check(store.Checkpoint(id, 500) && !store.Checkpoint(id, 2), "Collected total is monotonic");
            check(!store.Equip(1) && !store.Upgrade(ResearchId.Elements, 0), "Store rejects mid-run loadout changes");
            var recovered = new ProgressStore(path);
            check(recovered.Snapshot.souls == 500 && recovered.Snapshot.lastOutcome == "中断返还" && recovered.Snapshot.activeRunId == "", "Restart settles checkpointed interrupted action");
            check(recovered.Finish(id, 500, false, "中断返还") && recovered.Snapshot.souls == 500, "Recovered action receipt prevents duplicate earnings");
            recovered.BeforeWriteForTests = () => throw new IOException("Injected purchase failure");
            check(!recovered.Upgrade(ResearchId.Vitality, 0) && recovered.Snapshot.souls == 500 && recovered.Snapshot.ranks[0] == 0, "Failed purchase rolls back both payment and level");
            recovered.BeforeWriteForTests = null;
            check(recovered.Upgrade(ResearchId.Vitality, 0) && !recovered.Upgrade(ResearchId.Vitality, 0), "Stale confirmation cannot charge twice");
            for (int rank = 1; rank < 5; rank++) check(recovered.Upgrade(ResearchId.Vitality, rank), "Research progression purchases next rank");
            check(!recovered.Upgrade(ResearchId.Vitality, 5) && recovered.Snapshot.ranks[0] == 5 && recovered.Snapshot.souls == 200, "Research caps at five and costs total 300");
            check(recovered.BeginRun(out string victory), "New action begins after interruption recovery");
            check(recovered.Finish(victory, 7, true, "胜利") && recovered.Finish(victory, 7, true, "胜利") && recovered.Snapshot.souls == 207 && recovered.Snapshot.proofs == 1, "Victory credits souls and proof once");
            check(!recovered.Finish(id, 500, false, "stale"), "Old receipt cannot overwrite newer action");
            // Refresh backup with a completed, valid profile before deliberately corrupting the primary.
            recovered.Equip(1);
            File.WriteAllText(path, "damaged");
            var backup = new ProgressStore(path);
            check(backup.Ready && backup.Snapshot.souls == 207 && !string.IsNullOrEmpty(backup.Notice), "Damaged primary recovers validated backup");
            check(backup.Equip(1) && new ProgressStore(path).Snapshot.staff == 1, "Recovered save can commit without destroying backup");
            File.WriteAllText(path, "damaged"); File.WriteAllText(path + ".bak", "also damaged");
            var broken = new ProgressStore(path);
            check(!broken.Ready && File.ReadAllText(path) == "damaged", "Two damaged files are preserved and block replacement with empty profile");
        }
    }
}
