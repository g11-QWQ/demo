using System;

namespace StarFall
{
    public enum MatchState { MainMenu, Playing, Paused, Won, Lost, ChoosingBuild, ClaimingReward, HomeBase }

    // Rules and state have no Unity dependency, so they can be checked independently.
    public sealed class MatchSession
    {
        public MatchState State { get; private set; } = MatchState.MainMenu;
        public float Elapsed { get; private set; }
        public float Duration { get; private set; }
        public float Remaining => Math.Max(0, Duration - Elapsed);
        public float Health { get; private set; }
        public float MaxHealth { get; private set; }
        public int Kills { get; private set; }
        public int SoulExperience { get; private set; }
        public BuildProgression Builds { get; } = new BuildProgression();
        public int SoulFireStacks { get; private set; }
        public int RecoveryProgress { get; private set; }
        public bool FinalBossDefeated { get; private set; }
        public bool HasClearanceProof { get; private set; }
        public bool Invulnerable { get; set; }
        private MatchState pausedFrom;
        public event Action<MatchState> StateChanged;
        public event Action SoulsCollected;

        public void Start(float duration, float health, int[] levelExperience = null, int? draftSeed = null, DamageElement element = DamageElement.Fire)
        {
            if (duration <= 0 || health <= 0) throw new ArgumentOutOfRangeException();
            Duration = duration;
            Health = MaxHealth = health;
            Elapsed = 0;
            Kills = 0;
            SoulExperience = 0;
            SoulFireStacks = RecoveryProgress = 0;
            FinalBossDefeated = HasClearanceProof = false;
            pausedFrom = MatchState.Playing;
            Invulnerable = false;
            Builds.Reset(levelExperience, draftSeed, element);
            SetState(MatchState.Playing);
        }

        public void Advance(float delta)
        {
            if (State != MatchState.Playing || delta <= 0) return;
            Elapsed += delta;
        }

        public bool Damage(float amount)
        {
            if (State != MatchState.Playing || amount <= 0 || Invulnerable) return false;
            Health = Math.Max(0, Health - amount);
            if (Health == 0) SetState(MatchState.Lost);
            return true;
        }

        public void AddKill() { if (State == MatchState.Playing) Kills++; }
        public void CollectSoul(int value, bool activelyAbsorbing = false)
        {
            if (State != MatchState.Playing || value <= 0) return;
            SoulExperience += value;
            if (activelyAbsorbing && Builds.Rank(BuildId.SoulFire) > 0) SoulFireStacks = Math.Min(5, SoulFireStacks + value);
            if (Builds.Rank(BuildId.SoulRecovery) > 0)
            {
                RecoveryProgress += value;
                int heals = RecoveryProgress / 10;
                RecoveryProgress %= 10;
                Health = Math.Min(MaxHealth, Health + heals * 2);
            }
            Builds.AddExperience(value);
            if (Builds.PendingChoices > 0) SetState(MatchState.ChoosingBuild);
            SoulsCollected?.Invoke();
        }

        public bool ChooseBuild(BuildId id, int revision)
        {
            if (State != MatchState.ChoosingBuild || !Builds.Choose(id, revision)) return false;
            if (id == BuildId.Vitality) { MaxHealth += 20; Health = Math.Min(MaxHealth, Health + 20); }
            if (Builds.PendingChoices == 0) SetState(MatchState.Playing);
            return true;
        }
        public void TrainingUpgrade()
        {
            if (State != MatchState.Playing || Builds.IsMaxed) return;
            Builds.AddExperience(Builds.ExperienceRequired);
            if (Builds.PendingChoices > 0) SetState(MatchState.ChoosingBuild);
        }

        public float ConsumeSoulFire()
        {
            if (State != MatchState.Playing || SoulFireStacks <= 0) return 1;
            SoulFireStacks--;
            return 1.3f;
        }
        public bool DefeatFinalBoss()
        {
            if (State != MatchState.Playing || FinalBossDefeated) return false;
            FinalBossDefeated = true;
            SetState(MatchState.ClaimingReward);
            return true;
        }
        public bool ClaimClearanceProof()
        {
            if (State != MatchState.ClaimingReward || !FinalBossDefeated || HasClearanceProof) return false;
            HasClearanceProof = true;
            SetState(MatchState.Won);
            return true;
        }
        public void Pause()
        {
            if (State != MatchState.Playing && State != MatchState.ClaimingReward) return;
            pausedFrom = State;
            SetState(MatchState.Paused);
        }
        public void Resume() { if (State == MatchState.Paused) SetState(pausedFrom); }
        public void ReturnToMenu() => SetState(MatchState.MainMenu);
        public void ReturnToBase() => SetState(MatchState.HomeBase);

        private void SetState(MatchState state)
        {
            if (State == state) return;
            State = state;
            StateChanged?.Invoke(state);
        }
    }
}
