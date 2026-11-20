using System;
using System.Collections.Generic;

namespace StarFall
{
    // Drafts have revision tickets: a stale or double click cannot consume a later upgrade.
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
        public int Revision { get; private set; }
        public int TotalRanks { get; private set; }
        public int DistinctBuilds => ranks.Count;
        public IReadOnlyList<BuildId> Offers => offerView;
        public DamageElement Element { get; private set; } = DamageElement.Fire;
        public DamageElement Resonance { get; private set; }
        public int MaximumRanks { get; private set; }
        public bool IsMaxed => TotalRanks + PendingChoices >= MaximumRanks;
        public int ExperienceRequired => IsMaxed ? 0 : Cost(Level);

        public BuildProgression()
        {
            offerView = offers.AsReadOnly();
            Reset(null);
        }

        public int Rank(BuildId id) => ranks.TryGetValue(id, out int rank) ? rank : 0;

        public void Reset(int[] thresholds, int? seed = null, DamageElement element = DamageElement.Fire)
        {
            Element = element; Resonance = DamageElement.Physical; MaximumRanks = 1;
            foreach (var definition in BuildCatalog.All)
                if (!BuildCatalog.IsResonance(definition.Id) && MatchesElement(definition.Id)) MaximumRanks += definition.MaxRank;
            costs = thresholds == null || thresholds.Length == 0 ? new[] { 3, 4, 5, 6, 8, 10, 12 } : (int[])thresholds.Clone();
            for (int i = 0; i < costs.Length; i++) costs[i] = Math.Max(1, costs[i]);
            random = seed.HasValue ? new Random(seed.Value) : new Random();
            ranks.Clear(); offers.Clear();
            Level = 1; Experience = PendingChoices = TotalRanks = 0;
            Revision++;
        }

        private int Cost(int level) => level <= costs.Length ? costs[level - 1] : costs[costs.Length - 1] + (level - costs.Length) * 4;

        public void AddExperience(int value)
        {
            if (value <= 0) return;
            Experience += value;
            while (!IsMaxed && Experience >= Cost(Level))
            {
                Experience -= Cost(Level);
                Level++;
                PendingChoices++;
            }
            if (PendingChoices > 0 && offers.Count == 0) Draw();
        }

        public bool Choose(BuildId id, int revision)
        {
            if (revision != Revision || PendingChoices <= 0 || !offers.Contains(id)) return false;
            if (Rank(id) >= BuildCatalog.Get(id).MaxRank) return false;
            ranks[id] = Rank(id) + 1;
            if (BuildCatalog.IsResonance(id)) Resonance = BuildCatalog.ResonanceElement(id);
            TotalRanks++;
            PendingChoices--;
            offers.Clear();
            Revision++;
            if (PendingChoices > 0) Draw();
            return true;
        }

        private void Draw()
        {
            var eligible = new List<BuildId>();
            foreach (var definition in BuildCatalog.All)
                if (Eligible(definition.Id)) eligible.Add(definition.Id);
            if (Resonance == DamageElement.Physical && (TotalRanks == 2 || TotalRanks == 5))
            {
                var resonances = eligible.FindAll(BuildCatalog.IsResonance);
                if (resonances.Count > 0) { var selected = resonances[random.Next(resonances.Count)]; offers.Add(selected); eligible.Remove(selected); }
            }
            while (offers.Count < 3 && eligible.Count > 0)
            {
                int index = random.Next(eligible.Count);
                offers.Add(eligible[index]); eligible.RemoveAt(index);
            }
            // More compatible pairs must not force the player to bind a resonance.
            if (offers.Count > 0 && offers.TrueForAll(BuildCatalog.IsResonance))
            {
                var regular = eligible.FindAll(id => !BuildCatalog.IsResonance(id));
                if (regular.Count > 0) offers[offers.Count - 1] = regular[random.Next(regular.Count)];
            }
            Revision++;
        }
        private bool MatchesElement(BuildId id) => BuildCatalog.ExclusiveElement(id) == DamageElement.Physical || BuildCatalog.ExclusiveElement(id) == Element;
        public bool Eligible(BuildId id)
        {
            if (Rank(id) >= BuildCatalog.Get(id).MaxRank) return false;
            if (BuildCatalog.IsResonance(id)) return TotalRanks >= 2 && Resonance == DamageElement.Physical && ElementCatalog.Compatible(Element, BuildCatalog.ResonanceElement(id));
            return MatchesElement(id);
        }
        public bool GrantForTraining(BuildId id)
        {
            if (PendingChoices > 0 || Rank(id) >= BuildCatalog.Get(id).MaxRank) return false;
            if (BuildCatalog.IsResonance(id))
            {
                if (Resonance != DamageElement.Physical || !ElementCatalog.Compatible(Element, BuildCatalog.ResonanceElement(id))) return false;
                Resonance = BuildCatalog.ResonanceElement(id);
            }
            else if (!MatchesElement(id)) return false;
            ranks[id] = Rank(id) + 1; TotalRanks++; Revision++; return true;
        }
    }
}
