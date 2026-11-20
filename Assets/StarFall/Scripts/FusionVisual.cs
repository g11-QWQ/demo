using UnityEngine;

namespace StarFall
{
    // Procedural ribbons are deliberately airy: actors and hostile telegraphs remain visible.
    public sealed class FusionVisual : MonoBehaviour
    {
        private readonly LineRenderer[] ribbons = new LineRenderer[26];
        private MaterialPropertyBlock block;
        private int used;
        private static readonly Color Fire = new Color(1, 0.27f, 0.025f);
        private static readonly Color Gold = new Color(1, 0.82f, 0.27f);
        private static readonly Color Thunder = new Color(0.68f, 0.18f, 1);
        private static readonly Color Wind = new Color(0.13f, 0.95f, 0.62f);
        private static readonly Color Water = new Color(0.06f, 0.32f, 1);
        public void Initialize(Material material)
        {
            block = new MaterialPropertyBlock();
            for (int i = 0; i < ribbons.Length; i++)
            {
                var go = new GameObject("Ribbon" + i); go.transform.SetParent(transform, false);
                var line = go.AddComponent<LineRenderer>(); ribbons[i] = line;
                line.sharedMaterial = material; line.useWorldSpace = false; line.numCapVertices = 3; line.numCornerVertices = 2;
                line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; line.receiveShadows = false;
            }
        }
        private LineRenderer Line(int count, float width, Color color, bool loop = false)
        {
            var line = ribbons[used++]; line.enabled = true; line.loop = loop;
            line.positionCount = count; line.widthMultiplier = width;
            block.SetColor("_BaseColor", color); line.SetPropertyBlock(block); return line;
        }
        private void Ring(float radius, float y, float angle, float width, Color color, int corners = 64)
        {
            var line = Line(corners, width, color, true);
            for (int i = 0; i < corners; i++)
            {
                float a = angle + i * Mathf.PI * 2 / corners;
                line.SetPosition(i, new Vector3(Mathf.Cos(a) * radius, y, Mathf.Sin(a) * radius));
            }
        }
        private static Color Fade(Color color, float alpha) { color.a = Mathf.Clamp01(alpha); return color; }
        public void Draw(FusionKind kind, float age, Vector3 position, Vector3 direction, float lifetime)
        {
            transform.position = position; used = 0;
            float fade = Mathf.Clamp01((lifetime - age) / 0.45f);
            if (kind == FusionKind.Judgment) Judgment(age, fade);
            else if (kind == FusionKind.FireTornado) Tornado(age, fade, direction);
            else Vortex(age, fade, direction);
            for (int i = used; i < ribbons.Length; i++) ribbons[i].enabled = false;
        }
        private void Judgment(float age, float fade)
        {
            float charge = Mathf.Clamp01(age / 0.65f);
            Ring(2.5f, 0.12f, age, 0.09f, Fade(Thunder, fade));
            Ring(1.2f + charge * 0.4f, 0.15f, -age, 0.07f, Fade(Fire, fade), 6);
            var star = Line(9, 0.085f, Fade(Gold, fade), true);
            for (int i = 0; i < 9; i++) { float a = i * Mathf.PI * 8 / 9 + age * 0.4f; star.SetPosition(i, new Vector3(Mathf.Cos(a) * 2.2f, 0.13f, Mathf.Sin(a) * 2.2f)); }
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4 - age; var axis = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                var rune = Line(3, 0.08f, Fade(Thunder, fade));
                rune.SetPosition(0, axis * 2.7f + Vector3.up * 0.15f);
                rune.SetPosition(1, axis * 3.05f + Vector3.up * 0.15f);
                rune.SetPosition(2, axis * 2.8f + Vector3.Cross(axis, Vector3.up) * 0.18f + Vector3.up * 0.15f);
            }
            if (age < 0.65f)
            {
                Ring(Mathf.Lerp(3.5f, 0.35f, charge), 0.3f, age * 2, 0.12f, Fade(Thunder, 0.65f)); return;
            }
            float impact = age - 0.65f, boltAlpha = Mathf.Clamp01(1 - impact / 0.42f);
            for (int strand = 0; strand < 4; strand++)
            {
                Color color = strand == 0 ? Color.white : Thunder;
                var bolt = Line(15, strand == 0 ? 0.13f : 0.25f + strand * 0.11f, Fade(color, boltAlpha / (strand == 0 ? 1 : 1.5f)));
                for (int i = 0; i < 15; i++)
                {
                    float t = i / 14f;
                    float x = Mathf.Sin(i * 2.7f + strand * 0.3f) * 0.65f * (1 - t) + (strand - 1) * 0.17f * (1 - t);
                    bolt.SetPosition(i, new Vector3(x, 8 * (1 - t), 10 * (1 - t)));
                }
            }
            Ring(0.4f + impact * 9, 0.2f, 0, 0.23f, Fade(Fire, fade));
            Ring(0.25f + impact * 6, 0.22f, 0, 0.08f, Fade(Gold, fade));
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4 + 0.3f;
                var dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                var spark = Line(2, 0.08f, Fade(Gold, fade));
                spark.SetPosition(0, dir * (1 + impact * 5) + Vector3.up * 0.2f);
                spark.SetPosition(1, dir * (1.7f + impact * 6) + Vector3.up * 0.25f);
            }
        }
        private void Tornado(float age, float fade, Vector3 direction)
        {
            float grow = Mathf.Clamp01(age / 0.4f);
            Ring(2.1f * grow, 0.1f, age, 0.07f, Fade(Wind, fade * 0.7f));
            for (int strand = 0; strand < 7; strand++)
            {
                var spiral = Line(80, strand < 3 ? 0.23f : 0.1f, Fade(strand < 3 ? Fire : strand < 5 ? Gold : Wind, fade * grow * (strand < 3 ? 0.85f : 0.65f)));
                for (int i = 0; i < 80; i++)
                {
                    float t = i / 79f, a = t * Mathf.PI * 5 - age * 8 + strand * Mathf.PI * 2 / 7;
                    float radius = (0.25f + t * 1.7f + Mathf.Sin(t * 13 + age * 4) * 0.14f) * grow;
                    // Lean the upper funnel north so its height reads in the top-down camera.
                    spiral.SetPosition(i, new Vector3(Mathf.Cos(a) * radius, 0.1f + t * 4.7f, Mathf.Sin(a) * radius + t * 3.4f));
                }
            }
            for (int i = 0; i < 10; i++)
            {
                float t = Mathf.Repeat(age * 0.7f + i / 10f, 1), a = age * 4 + i * 2.4f;
                var p = new Vector3(Mathf.Cos(a) * (1 + t), t * 5, Mathf.Sin(a) * (1 + t) + t * 3.4f);
                var ember = Line(2, 0.07f, Fade(i % 3 == 0 ? Wind : Gold, fade * (1 - t)));
                ember.SetPosition(0, p); ember.SetPosition(1, p + Vector3.up * 0.3f + direction * 0.15f);
            }
            for (int i = 0; i < 4; i++)
            {
                var trail = Line(16, 0.07f, Fade(Fire, fade * 0.35f));
                var side = Vector3.Cross(direction, Vector3.up);
                for (int j = 0; j < 16; j++) { float t = j / 15f; trail.SetPosition(j, -direction * t * 4 + side * Mathf.Sin(t * 5 + age + i) * (i + 1) * 0.2f + Vector3.up * 0.07f); }
            }
        }
        private void Vortex(float age, float fade, Vector3 direction)
        {
            float grow = Mathf.Clamp01(age / 0.45f), pullFade = age < 3.2f ? 1 : Mathf.Clamp01(1 - (age - 3.2f) / 0.7f);
            Ring(3.5f * grow, 0.1f, age, 0.1f, Fade(Wind, fade * pullFade));
            Ring(3.2f * grow, 0.12f, -age, 0.28f, Fade(Water, fade * pullFade * 0.5f));
            for (int strand = 0; strand < 6; strand++)
            {
                var spiral = Line(72, strand % 3 == 0 ? 0.22f : 0.1f, Fade(strand % 3 == 0 ? Water : Wind, pullFade * (strand % 3 == 0 ? 0.75f : 0.4f)));
                for (int i = 0; i < 72; i++)
                {
                    float t = i / 71f, a = t * Mathf.PI * 1.5f + strand * Mathf.PI * 2 / 6 + age * 2.4f;
                    float radius = (0.2f + t * 3.2f) * grow;
                    spiral.SetPosition(i, new Vector3(Mathf.Cos(a) * radius, 0.15f + (1 - t) * 0.7f, Mathf.Sin(a) * radius));
                }
            }
            if (age < 3.2f) return;
            float travel = Mathf.Clamp01((age - 3.2f) / 0.7f) * 6;
            Vector3 side = Vector3.Cross(direction, Vector3.up);
            for (int k = 0; k < 6; k++)
            {
                var wave = Line(40, k == 0 ? 0.5f : 0.12f, Fade(k == 0 ? Water : k == 1 ? Color.white : Wind, fade * (1 - k * 0.12f)));
                for (int i = 0; i < 40; i++)
                {
                    float a = (i / 39f - 0.5f) * Mathf.PI * 0.85f;
                    wave.SetPosition(i, direction * (travel + Mathf.Cos(a) * 1.3f - k * 0.28f) + side * Mathf.Sin(a) * 3 + Vector3.up * (0.2f + k * 0.3f));
                }
            }
        }
    }
}
