using UnityEngine;
using UnityEngine.InputSystem;

namespace StarFall
{
    public sealed partial class GameApplication
    {
        public bool TestMode { get; private set; }
        private DamageElement testElement = DamageElement.Fire;
        public DamageElement TestResonance { get; set; }
        public void SelectGem(int index)
        {
            if (SaveBlocked || SettingsOpen || (Session.State != MatchState.MainMenu && Session.State != MatchState.HomeBase)) return;
            Progress.EquipGem(index); TestResonance = DamageElement.Physical; UI.RefreshWorkshop(); UI.RefreshBase();
        }
        public void StartTraining(bool bossOnly = false)
        {
            if (SaveBlocked || SettingsOpen || !Progress.Ready || (Session.State != MatchState.HomeBase && Session.State != MatchState.MainMenu)) return;
            TestMode = true; testElement = (DamageElement)(Progress.Snapshot.gemIndex + 1);
            UI.CloseWorkshop();
            RestartTraining();
            Session.Invulnerable = true;
            if (bossOnly) Battle.TrainingBoss();
        }
        public void RestartTraining(int gemIndex = -1)
        {
            if (!TestMode || SaveBlocked) return;
            bool invulnerable = Session.Invulnerable;
            if (gemIndex >= 0) testElement = (DamageElement)(gemIndex + 1);
            if (!ElementCatalog.Compatible(testElement, TestResonance)) TestResonance = DamageElement.Physical;
            Battle.Begin(null, testElement, true);
            Session.Invulnerable = invulnerable;
            if (TestResonance != DamageElement.Physical)
                Session.Builds.GrantForTraining((BuildId)((int)BuildId.ResFire + (int)TestResonance - 1));
        }
        public void ToggleTrainingInvulnerability() { if (TestMode && !SaveBlocked) Session.Invulnerable = !Session.Invulnerable; }
        public void StartShowcase(int index)
        {
            if (SaveBlocked || SettingsOpen || (!TestMode && Session.State != MatchState.MainMenu && Session.State != MatchState.HomeBase)) return;
            TestMode = true;
            TestResonance = index == 0 ? DamageElement.Lightning : index == 1 ? DamageElement.Wind : DamageElement.Ocean;
            UI.CloseWorkshop(); RestartTraining(index == 2 ? 2 : 0); Session.Invulnerable = true;
            Battle.TrainingTargets(); Battle.TrainingChargeFusion();
        }
        public void TrainingUpgrade() { if (TestMode && !SaveBlocked) Session.TrainingUpgrade(); }
        public void CycleTrainingResonance()
        {
            if (!TestMode || SaveBlocked) return;
            for (int offset = 1; offset <= 6; offset++)
            {
                var next = (DamageElement)(((int)TestResonance + offset) % 6);
                if (next == DamageElement.Physical || ElementCatalog.Compatible(testElement, next)) { TestResonance = next; RestartTraining(); return; }
            }
        }
        private void HandleTrainingInput(Keyboard keyboard)
        {
            if (!TestMode || keyboard == null || SettingsOpen || SaveBlocked) return;
            if (keyboard.rKey.wasPressedThisFrame) RestartTraining();
            if (keyboard.digit1Key.wasPressedThisFrame) RestartTraining(0);
            if (keyboard.digit2Key.wasPressedThisFrame) RestartTraining(1);
            if (keyboard.digit3Key.wasPressedThisFrame) RestartTraining(2);
            if (keyboard.digit4Key.wasPressedThisFrame) RestartTraining(3);
            if (keyboard.digit5Key.wasPressedThisFrame) RestartTraining(4);
            if (keyboard.uKey.wasPressedThisFrame) TrainingUpgrade();
            if (keyboard.bKey.wasPressedThisFrame) Battle.TrainingBoss();
            if (keyboard.iKey.wasPressedThisFrame) ToggleTrainingInvulnerability();
            if (keyboard.cKey.wasPressedThisFrame) CycleTrainingResonance();
            if (keyboard.fKey.wasPressedThisFrame) Battle.TrainingChargeFusion();
            if (keyboard.gKey.wasPressedThisFrame) Battle.TrainingTargets();
        }
    }
}

