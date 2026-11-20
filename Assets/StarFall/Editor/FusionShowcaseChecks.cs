using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace StarFall.Editor
{
    public static class FusionShowcaseChecks
    {
        public static void Run(GameApplication app, Action<bool, string> check, Action<GameApplication, string> capture)
        {
            var battle = app.Battle; var ui = app.UI; var original = battle.Config;
            var config = UnityEngine.Object.Instantiate(original);
            config.enableObstacles = config.enableBossEncounters = false;
            SetConfig(battle, config);
            long wallet = app.Progress.Snapshot.souls;
            try
            {
                ui.OpenWorkshop(); capture(app, "30-fusion-workshop");
                app.StartShowcase(0);
                check(app.TestMode && battle.TrainingShowcase && battle.EnemyCount == 12 && battle.FusionEnergy == 100, "One-click showcase prepares durable enemies and full charge");
                var target = battle.Enemies[0];
                check(GameUI.RoundDamage(6.4f) == 6 && GameUI.RoundDamage(6.5f) == 7, "Damage display rounds to nearest integer with halves up");
                for (int e = 1; e <= 5; e++)
                {
                    float before = target.Health;
                    target.TakeDamage(6.5f, (DamageElement)e);
                    check(ui.LastDamageNumber == 7 && ui.LastDamageColor == ElementCatalog.ColorFor((DamageElement)e), "Damage number has exact element color " + e);
                    check(Mathf.Abs(target.Health - (before - 6.5f)) < 0.001f, "Integer display preserves fractional health " + e);
                }
                int labels = ui.DamageNumberCount;
                target.TakeDamage(0, DamageElement.Fire);
                check(ui.DamageNumberCount == labels, "Zero damage creates no label");
                app.Session.Pause(); battle.Step(2, Vector2.zero);
                check(ui.DamageNumberCount == labels && battle.FusionEnergy == 100, "Pause freezes numbers and charge");
                app.Resume(); battle.Step(1, Vector2.zero, null, true);
                check(ui.DamageNumberCount == 0 && battle.FusionEnergy == 100 && battle.FusionCastCount == 0, "Absorbing holds charge and existing numbers expire");
                target.ApplyBurn(2); target.StepBurn(1.1f);
                check(ui.LastDamageNumber == 2 && ui.LastDamageColor == ElementCatalog.ColorFor(DamageElement.Fire), "Burn ticks create fire-colored numbers");
                target.TakeDamage(1000, DamageElement.Earth); labels = ui.DamageNumberCount;
                target.TakeDamage(5, DamageElement.Fire);
                check(ui.LastDamageNumber == 1000 && ui.DamageNumberCount == labels, "Lethal hit displays full damage and dead targets do not duplicate it");

                for (int pair = 0; pair < 3; pair++)
                {
                    app.StartShowcase(pair);
                    var victims = battle.Enemies.ToArray();
                    foreach (var enemy in victims) enemy.PrepareTrainingTarget(1000, 0);
                    var aim = pair == 0 ? victims[1].Position : new Vector3(0, 0.6f, 4);
                    battle.Step(0.02f, Vector2.zero, aim);
                    check(battle.FusionCastCount == 1 && battle.ActiveFusionCount == 1 && battle.FusionEnergy < 1, "Full charge casts signature spell " + pair);
                    battle.Step(pair == 0 ? 0.75f : 1.5f, Vector2.zero, aim, true);
                    check(victims.Any(e => e.Health < 1000) && ui.DamageNumberCount > 0, "Signature spell deals damage and emits labels " + pair);
                    ui.Refresh(app.Session, 0); capture(app, "31-fusion-" + pair);
                    float hp = victims.Sum(e => e.Health); int active = battle.ActiveFusionCount;
                    app.Session.Pause(); battle.Step(5, Vector2.zero);
                    check(battle.ActiveFusionCount == active && victims.Sum(e => e.Health) == hp, "Pause freezes signature spell " + pair);
                    app.Resume();
                    app.TrainingUpgrade(); battle.Step(5, Vector2.zero);
                    check(app.Session.State == MatchState.ChoosingBuild && battle.ActiveFusionCount == active && victims.Sum(e => e.Health) == hp, "Upgrade choice freezes signature spell " + pair);
                    BuildChecks.DrainChoices(app.Session);
                    if (pair == 2)
                    {
                        battle.Step(1.85f, Vector2.zero, aim, true);
                        check(victims.Sum(e => e.Health) < hp, "Whirlpool culminates in damaging wave");
                        ui.Refresh(app.Session, 0); capture(app, "32-vortex-wave");
                    }
                    battle.Step(5, Vector2.zero, null, true);
                    check(battle.ActiveFusionCount == 0, "Signature visual expires " + pair);
                    battle.TrainingTargets();
                    check(battle.FusionEnergy == 0 && ui.DamageNumberCount == 0 && battle.EnemyCount == 12, "Target reset clears charge, effects and numbers " + pair);
                    battle.TrainingChargeFusion();
                    app.RestartTraining();
                    check(battle.FusionEnergy == 0 && battle.ActiveFusionCount == 0 && ui.DamageNumberCount == 0, "Restart clears all signature state " + pair);
                }
                // Reverse main/secondary directions remain playable.
                int[] main = { 1, 2, 3 };
                DamageElement[] secondary = { DamageElement.Fire, DamageElement.Fire, DamageElement.Wind };
                for (int i = 0; i < main.Length; i++)
                {
                    app.TestResonance = secondary[i]; app.RestartTraining(main[i]);
                    battle.TrainingTargets(); battle.TrainingChargeFusion();
                    Vector3 aim = i == 0 ? battle.Enemies[1].Position : new Vector3(0, 0.6f, 4);
                    battle.Step(2, Vector2.zero, aim);
                    check(battle.FusionCastCount == 1 && battle.Enemies.Any(e => e.Health < 250), "Reverse elemental pairing works " + i);
                }
                // A spread credits energy once for its whole cast, even when several blades hit.
                app.StartShowcase(2); battle.TrainingTargets();
                battle.Step(0.02f, Vector2.zero, new Vector3(0, 0.6f, 4));
                battle.Step(0.6f, Vector2.zero, null, true);
                check(battle.FusionEnergy > 0 && battle.FusionEnergy <= 100f / (battle.AttackFrequency * 6) + 0.001f, "Wind volley cannot multiply fusion charge");
                battle.TrainingTargets();
                battle.Step(2, Vector2.zero, new Vector3(0, 0.6f, -16));
                check(battle.FusionEnergy == 0, "Missed attacks do not charge fusion");
                battle.TrainingChargeFusion(); battle.Step(0.8f, Vector2.zero, new Vector3(0, 0.6f, 4));
                battle.TrainingBoss();
                check(battle.ActiveFusionCount == 0 && battle.FusionEnergy == 0 && !battle.TrainingShowcase, "Boss shortcut clears showcase spells");
                app.ReturnToBase();
                check(app.Progress.Snapshot.souls == wallet, "All showcase activity remains isolated from permanent currency");
            }
            finally { app.ReturnToBase(); SetConfig(battle, original); UnityEngine.Object.Destroy(config); }
        }
        private static void SetConfig(BattleController battle, BattleConfig config)
        {
            var serialized = new SerializedObject(battle);
            serialized.FindProperty("config").objectReferenceValue = config;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
