using System;
using System.Collections.Generic;

namespace StarFall
{
    public sealed class BuildProgression
    {
        private readonly Dictionary<BuildId, int> ranks = new Dictionary<BuildId, int>();
        private readonly List<BuildId> offers = new List<BuildId>();
        private readonly IReadOnlyList<BuildId> offerView;
        private int[] costs;
        private Random random;
        public int Level { get; private set; } = 1;
        public int Experience { get; private set; }
        public int PendingChoices { get; private set; }
        public int ChoiceLevel => Level - PendingChoices + 1;
        public int Revision { get; private set; }
        public int TotalRanks { get; private set; }
        public int DistinctBuilds => ranks.Count;
        public IReadOnlyList<BuildId> Offers => offerView;
        public DamageElement Element { get; private set; } = DamageElement.Fire;
        public DamageElement Resonance { get; private set; }
        public int MaximumRanks => 14;
        public bool IsMaxed => Level >= 15;
        public int ExperienceRequired => IsMaxed ? 0 : Cost(Level);
        public int Form { get; private set; }
        public int Evolution { get; private set; }
        public int UltimateStyle { get; private set; }
        public int Specialization { get; private set; }
        public bool UltimateUnlocked => UltimateStyle > 0;
        public int NextBreakthrough => Level < 3 ? 3 : Level < 6 ? 6 : Level < 9 ? 9 : Level < 12 ? 12 : Level < 15 ? 15 : 0;
        public BuildProgression() { offerView = offers.AsReadOnly(); Reset(null); }
        public int Rank(BuildId id) => ranks.TryGetValue(id, out int rank) ? rank : 0;
        public void Reset(int[] thresholds, int? seed = null, DamageElement element = DamageElement.Fire)
        {
            Element = element; Resonance = DamageElement.Physical;
            costs = thresholds == null || thresholds.Length == 0 ? new[] { 3, 4, 5, 6, 8, 10, 12 } : (int[])thresholds.Clone();
            for (int i = 0; i < costs.Length; i++) costs[i] = Math.Max(1, costs[i]);
            random = seed.HasValue ? new Random(seed.Value) : new Random();
            ranks.Clear(); offers.Clear(); Level = 1; Experience = PendingChoices = TotalRanks = 0;
            Form = Evolution = UltimateStyle = Specialization = 0; Revision++;
        }
        private int Cost(int level) => level <= costs.Length ? costs[level - 1] : costs[costs.Length - 1] + (level - costs.Length) * 4;
        public void AddExperience(int value)
        {
            if (value <= 0) return;
            Experience = (int)Math.Min(int.MaxValue, (long)Experience + value);
            while (!IsMaxed && Experience >= Cost(Level)) { Experience -= Cost(Level); Level++; PendingChoices++; }
            if (PendingChoices > 0 && offers.Count == 0) Draw();
        }
        public bool Choose(BuildId id, int revision)
        {
            if (revision != Revision || PendingChoices <= 0 || !offers.Contains(id) || Rank(id) >= BuildCatalog.Get(id).MaxRank) return false;
            Apply(id); PendingChoices--; offers.Clear(); Revision++;
            if (PendingChoices > 0) Draw();
            return true;
        }
        private void Apply(BuildId id)
        {
            ranks[id] = Rank(id) + 1; TotalRanks++;
            if (BuildCatalog.IsResonance(id)) Resonance = BuildCatalog.ResonanceElement(id);
            if (id >= BuildId.FormPierce && id <= BuildId.FormHeavy) Form = (int)id - (int)BuildId.FormPierce + 1;
            if (id >= BuildId.EvolveSpread && id <= BuildId.EvolveEcho) Evolution = (int)id - (int)BuildId.EvolveSpread + 1;
            if (id >= BuildId.UltimateBurst && id <= BuildId.UltimateCycle) UltimateStyle = (int)id - (int)BuildId.UltimateBurst + 1;
            if (id >= BuildId.MasterAttack && id <= BuildId.MasterHarmony) Specialization = (int)id - (int)BuildId.MasterAttack + 1;
        }
        private void Draw()
        {
            var eligible = new List<BuildId>();
            foreach (var definition in BuildCatalog.All) if (Eligible(definition.Id)) eligible.Add(definition.Id);
            while (offers.Count < 3 && eligible.Count > 0)
            { int i = random.Next(eligible.Count); offers.Add(eligible[i]); eligible.RemoveAt(i); }
            Revision++;
        }
        private bool MatchesElement(BuildId id) => BuildCatalog.ExclusiveElement(id) == DamageElement.Physical || BuildCatalog.ExclusiveElement(id) == Element;
        public bool Eligible(BuildId id)
        {
            if (Rank(id) >= BuildCatalog.Get(id).MaxRank) return false;
            int level = ChoiceLevel;
            if (level == 3 && Form == 0) return id >= BuildId.FormPierce && id <= BuildId.FormHeavy;
            if (level == 6 && Resonance == DamageElement.Physical) return BuildCatalog.IsResonance(id) && ElementCatalog.Compatible(Element, BuildCatalog.ResonanceElement(id));
            if (level == 9 && Evolution == 0) return id >= BuildId.EvolveSpread && id <= BuildId.EvolveEcho;
            if (level == 12 && !UltimateUnlocked) return id >= BuildId.UltimateBurst && id <= BuildId.UltimateCycle;
            if (level == 15 && Specialization == 0) return id >= BuildId.MasterAttack && id <= BuildId.MasterHarmony;
            return !BuildCatalog.IsResonance(id) && !BuildCatalog.IsMilestone(id) && MatchesElement(id);
        }
        public bool GrantForTraining(BuildId id)
        {
            if (PendingChoices > 0 || Rank(id) >= BuildCatalog.Get(id).MaxRank) return false;
            if (BuildCatalog.IsResonance(id))
            { if (Resonance != DamageElement.Physical || !ElementCatalog.Compatible(Element, BuildCatalog.ResonanceElement(id))) return false; }
            else if (BuildCatalog.IsMilestone(id))
            {
                if ((id <= BuildId.FormHeavy && Form > 0) || (id >= BuildId.EvolveSpread && id <= BuildId.EvolveEcho && Evolution > 0)
                    || (id >= BuildId.UltimateBurst && id <= BuildId.UltimateCycle && UltimateUnlocked) || (id >= BuildId.MasterAttack && Specialization > 0)) return false;
            }
            else if (!MatchesElement(id)) return false;
            Apply(id); Revision++; return true;
        }
    }
}
