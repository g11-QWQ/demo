using UnityEngine;

namespace StarFall
{
    public sealed partial class FusionVisual
    {
        private static readonly Color Earth = new Color(0.77f, 0.57f, 0.22f);
        private void Stroke(Vector3 from, Vector3 to, float width, Color color, float wobble = 0, float phase = 0)
        {
            var line = Line(18, width, color);
            Vector3 side = Vector3.Cross((to - from).normalized, Vector3.up);
            for (int i = 0; i < 18; i++)
            {
                float t = i / 17f;
                line.SetPosition(i, Vector3.Lerp(from, to, t) + side * Mathf.Sin(i * 2.7f + phase) * wobble * Mathf.Sin(t * Mathf.PI));
            }
        }
        private void Shard(Vector3 p, Vector3 direction, float size, Color color)
        {
            var side = Vector3.Cross(direction, Vector3.up).normalized;
            var line = Line(4, 0.12f, color, true);
            line.SetPosition(0, p + direction * size);
            line.SetPosition(1, p + side * size * 0.4f);
            line.SetPosition(2, p - direction * size * 0.6f);
            line.SetPosition(3, p - side * size * 0.4f);
        }
        private void Extended(FusionKind kind, float age, float fade, Vector3 direction)
        {
            switch (kind)
            {
                case FusionKind.SteamBurst: Steam(age, fade); break;
                case FusionKind.Volcano: Volcano(age, fade); break;
                case FusionKind.StormFront: Storm(age, fade, direction); break;
                case FusionKind.ConductivePool: Conduction(age, fade); break;
                case FusionKind.RailSpear: Rail(age, fade, direction); break;
                case FusionKind.SandDrill: Drill(age, fade, direction); break;
                case FusionKind.StoneTomb: Tomb(age, fade); break;
            }
        }
        private void Steam(float age, float fade)
        {
            float radius = Mathf.Min(4.5f, age * 5), hot = Mathf.Clamp01(1 - age / 1.5f);
            Ring(radius, 0.15f, 0, 0.28f, Fade(Fire, hot));
            Ring(radius * 0.9f, 0.18f, 0, 0.13f, Fade(Water, fade));
            for (int i = 0; i < 12; i++)
            {
                float a = i * Mathf.PI / 6;
                Vector3 axis = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                var curl = Line(22, 0.13f, Fade(i % 2 == 0 ? new Color(0.6f, 0.85f, 1) : Gold, fade * 0.65f));
                for (int j = 0; j < 22; j++)
                {
                    float t = j / 21f, twist = t * 5 + age * 2;
                    curl.SetPosition(j, axis * (radius * 0.8f + Mathf.Sin(twist) * 0.4f) + Vector3.Cross(axis, Vector3.up) * Mathf.Cos(twist) * 0.45f + new Vector3(0, t * 2, t * 0.7f));
                }
            }
        }
        private void Volcano(float age, float fade)
        {
            float eruption = Mathf.Clamp01((age - 0.55f) * 3), cooled = Mathf.Clamp01((age - 3.3f) * 4);
            Ring(3, 0.12f, 0, 0.12f, Fade(Earth, fade), 10);
            Ring(2.4f, 0.15f, age, 0.2f, Fade(Fire, fade * eruption));
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4;
                var axis = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                Stroke(axis * 0.4f, axis * 3, 0.09f, Fade(Fire, fade), 0.3f, i);
                float travel = Mathf.Repeat(age * 0.65f + i * 0.13f, 1);
                var rock = axis * (0.3f + travel * 2.4f) + new Vector3(0, Mathf.Sin(travel * Mathf.PI) * 3, Mathf.Sin(travel * Mathf.PI) * 1.7f);
                Shard(rock, axis, 0.35f, Fade(cooled > 0 ? Earth : Gold, fade * eruption));
            }
            Ring(0.4f + cooled * 2.8f, 0.2f, age, 0.3f, Fade(Earth, cooled * fade), 6);
        }
        private void Storm(float age, float fade, Vector3 direction)
        {
            var side = Vector3.Cross(direction, Vector3.up);
            for (int k = 0; k < 5; k++)
            {
                var front = Line(40, k == 0 ? 0.22f : 0.07f, Fade(k % 2 == 0 ? Wind : Thunder, fade));
                for (int j = 0; j < 40; j++)
                {
                    float t = j / 39f * 2 - 1;
                    front.SetPosition(j, side * t * 4 + direction * (0.7f * (1 - t * t) - k * 0.3f) + Vector3.up * 0.2f);
                }
            }
            for (int k = 0; k < 7; k++)
            {
                var p = side * (k - 3) * 1.15f;
                Stroke(p - direction * 1.2f + Vector3.up * 0.3f, p + direction * 0.9f + Vector3.up * 0.3f, 0.12f, Fade(Thunder, fade * (0.6f + 0.4f * Mathf.Sin(age * 25 + k))), 0.35f, age * 20 + k);
            }
        }
        private void Conduction(float age, float fade)
        {
            Ring(3.5f, 0.1f, 0, 0.17f, Fade(Water, fade));
            Ring(3.1f, 0.12f, 0, 0.06f, Fade(Thunder, fade));
            for (int k = 0; k < 5; k++)
            {
                float r = Mathf.Repeat(age * 0.65f + k * 0.2f, 1) * 3.4f;
                Ring(r, 0.14f, 0, 0.07f, Fade(Water, fade * 0.6f));
            }
            for (int i = 0; i < 10; i++)
            {
                float a = i * Mathf.PI / 5 + Mathf.Floor(age * 4) * 0.5f;
                var from = new Vector3(Mathf.Cos(a), 0.18f, Mathf.Sin(a)) * 3;
                var to = new Vector3(Mathf.Cos(a + 2.5f), 0.18f, Mathf.Sin(a + 2.5f)) * 2.7f;
                Stroke(from, to, 0.08f, Fade(Thunder, fade * (0.4f + Mathf.Repeat(age * 2, 1) * 0.5f)), 0.25f, age * 10);
            }
        }
        private void Rail(float age, float fade, Vector3 direction)
        {
            var side = Vector3.Cross(direction, Vector3.up);
            float impact = Mathf.Clamp01((age - 0.85f) * 8);
            Ring(Mathf.Max(0.25f, 2 * (1 - age / 0.85f)), 0.18f, age * 3, 0.15f, Fade(Thunder, fade), 6);
            for (int i = -1; i <= 1; i += 2)
                Stroke(side * i * 0.7f - direction * 2, side * i * 0.7f + direction * 8, 0.08f, Fade(Thunder, fade * 0.7f), 0.12f, age * 5);
            Shard(direction * (impact * 8 - 1), direction, age < 0.85f ? 1.3f : 2.1f, Fade(Earth, fade));
            if (age < 0.85f) return;
            Stroke(-direction * 1.5f + Vector3.up * 0.2f, direction * 8 + Vector3.up * 0.2f, 0.5f, Fade(Earth, fade));
            Stroke(-direction + Vector3.up * 0.24f, direction * 8 + Vector3.up * 0.24f, 0.13f, Fade(Color.white, fade));
            for (int i = 0; i < 8; i++)
                Shard(direction * (i * 0.9f) + side * Mathf.Sin(i * 2 + age * 4), direction, 0.3f, Fade(Thunder, fade));
        }
        private void Drill(float age, float fade, Vector3 direction)
        {
            var side = Vector3.Cross(direction, Vector3.up);
            for (int k = 0; k < 5; k++)
            {
                var spiral = Line(70, k < 2 ? 0.15f : 0.07f, Fade(k < 2 ? Earth : Wind, fade));
                for (int j = 0; j < 70; j++)
                {
                    float t = j / 69f, a = t * Mathf.PI * 8 + age * 12 + k * 1.25f;
                    spiral.SetPosition(j, direction * (t * 4 - 2) + side * Mathf.Cos(a) * (1 - t) * 1.6f + Vector3.up * (0.3f + Mathf.Sin(a) * 0.25f));
                }
            }
            for (int k = 0; k < 8; k++)
            {
                float a = age * 7 + k * Mathf.PI / 4;
                Shard(new Vector3(Mathf.Cos(a) * 2, 0.2f, Mathf.Sin(a) * 2), direction, 0.4f, Fade(Earth, fade));
            }
            Shard(direction * 2, direction, 0.7f, Fade(Gold, fade));
        }
        private void Tomb(float age, float fade)
        {
            float rise = Mathf.Clamp01(age / 0.6f), collapse = Mathf.Clamp01((age - 2.4f) / 0.5f);
            Ring(3.4f, 0.13f, 0, 0.18f, Fade(Water, fade));
            Ring(2.7f * (1 - collapse), 0.16f, age * 0.3f, 0.09f, Fade(Earth, fade), 8);
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4;
                var axis = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                var p = axis * 3 * (1 - collapse);
                var stone = Line(5, 0.23f, Fade(Earth, fade), true);
                var side = Vector3.Cross(axis, Vector3.up) * 0.38f;
                var top = new Vector3(0, rise * 2, rise * 1.1f);
                stone.SetPosition(0, p - side); stone.SetPosition(1, p - side + top);
                stone.SetPosition(2, p + top + Vector3.forward * 0.25f);
                stone.SetPosition(3, p + side + top); stone.SetPosition(4, p + side);
                Stroke(p + top * 0.5f, Vector3.up * 0.2f, 0.06f, Fade(Water, fade * 0.7f), 0.2f, age * 2 + i);
            }
            if (collapse > 0) Ring(0.3f + collapse * 3.5f, 0.2f, 0, 0.3f, Fade(Gold, fade));
        }
    }
}
