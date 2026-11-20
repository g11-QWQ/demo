using System;
using System.IO;
using UnityEngine;

namespace StarFall
{
    public sealed partial class GameApplication
    {
        public ProgressStore Progress { get; private set; }
        public bool SaveBlocked { get; private set; }
        private string runId;
        private Func<bool> retrySave;

        private void InitializeProgress()
        {
            string path = Path.Combine(Application.persistentDataPath, "progress-v1.json");
#if UNITY_EDITOR
            if (UnityEditor.SessionState.GetBool("StarFall.Checks.Pending", false))
                path = Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs/Checks/Profiles", Guid.NewGuid().ToString("N") + ".json"));
#endif
            Progress = new ProgressStore(path);
            Session.SoulsCollected += SaveCollectedSouls;
        }

        public void Deploy()
        {
            if (Session.State != MatchState.HomeBase || SettingsOpen || SaveBlocked || !Progress.Ready) return;
            if (!Progress.BeginRun(out runId)) { UI.RefreshBase(); return; }
            TestMode = false;
            Battle.Begin(Progress.Snapshot);
        }

        private void SaveCollectedSouls()
        {
            if (TestMode || string.IsNullOrEmpty(runId)) return;
            if (!Progress.Checkpoint(runId, Session.SoulExperience))
            {
                Session.Pause();
                BlockSave(() => Progress.Checkpoint(runId, Session.SoulExperience));
            }
        }

        private bool SettleRun()
        {
            if (TestMode || string.IsNullOrEmpty(runId)) return true;
            bool won = Session.State == MatchState.Won;
            if (!Progress.Finish(runId, Session.SoulExperience, won && Session.HasClearanceProof,
                won ? "胜利" : Session.State == MatchState.Lost ? "阵亡" : "主动中止")) return false;
            runId = null;
            return true;
        }

        public void ReturnToBase() => RequestReturn(true);

        private void RequestReturn(bool home)
        {
            if (SaveBlocked) return;
            if (!SettleRun())
            {
                BlockSave(() =>
                {
                    if (!SettleRun()) return false;
                    CompleteReturn(home); return true;
                });
                return;
            }
            CompleteReturn(home);
        }

        private void CompleteReturn(bool home)
        {
            TestMode = false; UI.CloseWorkshop(); Battle.Clear(); SettingsOpen = false; UI.SetSettingsVisible(false);
            if (home) Session.ReturnToBase(); else Session.ReturnToMenu();
        }

        public void PurchaseResearch(ResearchId id, int expectedRank)
        {
            if (Session.State != MatchState.HomeBase || SaveBlocked) return;
            Progress.Upgrade(id, expectedRank); UI.RefreshBase();
        }
        public void EquipStaff(int staff)
        {
            if (Session.State != MatchState.HomeBase || SaveBlocked) return;
            Progress.Equip(staff); UI.RefreshBase();
        }
        private void BlockSave(Func<bool> retry)
        {
            SaveBlocked = true; retrySave = retry;
            UI.ShowSaveError(Progress.Error);
        }
        public void RetrySave()
        {
            if (!SaveBlocked || retrySave == null) return;
            if (!retrySave()) { UI.ShowSaveError(Progress.Error); return; }
            SaveBlocked = false; retrySave = null;
            UI.HideSaveError();
            if (Session.State == MatchState.Paused) Session.Resume();
            resultRemaining = Battle.Config.resultDisplaySeconds;
            UI.ShowState(Session.State);
        }
    }
}

