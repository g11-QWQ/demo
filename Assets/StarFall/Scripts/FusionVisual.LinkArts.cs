using UnityEngine;

namespace StarFall
{
    public sealed partial class FusionVisual
    {
        public void DrawLinkArt(FusionKind kind, float age, Vector3 position, Vector3 direction, float lifetime)
        {
            transform.position = position; used = 0;
            float fade = Mathf.Clamp01((lifetime - age) / 0.5f);
            int index = (int)kind - 1;
            Color a = Fade(ElementCatalog.ColorFor(FusionCatalog.Primary[index]), fade);
            Color b = Fade(ElementCatalog.ColorFor(FusionCatalog.Secondary[index]), fade);
            if (kind == FusionKind.Judgment)
            {
                Ring(2.5f, 0.2f, age, 0.12f, a, 6);
                Ring(2.1f, 0.5f, -age, 0.09f, b, 6);
                for (int i = 0; i < 6; i++)
                {
                    var line = Line(4, 0.11f, b); var p = Quaternion.Euler(0, i * 60, 0) * Vector3.forward * 2.2f;
                    line.SetPosition(0, p); line.SetPosition(1, p * 0.6f + Vector3.up * 1.2f); line.SetPosition(2, p * 0.9f + Vector3.up * 1.8f); line.SetPosition(3, Vector3.up * 2.5f);
                }
            }
            else if (kind == FusionKind.FireTornado || kind == FusionKind.StormFront)
            {
                for (int wing = 0; wing < 3; wing++)
                {
                    var line = Line(24, 0.13f, wing % 2 == 0 ? a : b);
                    for (int p = 0; p < 24; p++)
                    {
                        float t = p / 23f;
                        var side = Vector3.Cross(Vector3.up, direction);
                        line.SetPosition(p, direction * (kind == FusionKind.StormFront ? t * 9 : age * 4 + Mathf.Sin(t * Mathf.PI) * 1.5f) + side * (kind == FusionKind.StormFront ? Mathf.Sin(t * 25 + wing) * 0.7f : (t - 0.5f) * 5) + Vector3.up * (0.2f + wing * 0.2f));
                    }
                }
            }
            else if (kind == FusionKind.OceanVortex || kind == FusionKind.ConductivePool || kind == FusionKind.SandDrill)
            {
                for (int i = 0; i < 3; i++)
                {
                    float radius = kind == FusionKind.SandDrill ? 2.5f + i * 0.35f : 1.5f + Mathf.Repeat(age * 2 + i, 4);
                    Ring(radius, 0.2f + i * 0.15f, age * (i % 2 == 0 ? 4 : -4), 0.12f, i % 2 == 0 ? a : b, kind == FusionKind.SandDrill ? 8 : 48);
                }
            }
            else
            {
                int count = kind == FusionKind.RailSpear ? 3 : kind == FusionKind.StoneTomb ? 6 : 5;
                for (int i = 0; i < count; i++)
                {
                    Vector3 center = kind == FusionKind.Volcano ? direction * (1.5f + i * 1.5f) : kind == FusionKind.RailSpear ? direction * (age * 6) + Vector3.Cross(Vector3.up, direction) * (i - 1) : Quaternion.Euler(0, i * 360f / count, 0) * Vector3.forward * 1.8f;
                    if (kind == FusionKind.SteamBurst)
                    {
                        var bubble = Line(32, 0.14f, a, true);
                        var halo = Line(32, 0.07f, b, true);
                        float r = 0.55f + Mathf.Repeat(age * 1.4f + i * 0.15f, 0.65f);
                        for (int p = 0; p < 32; p++)
                        {
                            float angle = p * Mathf.PI * 2 / 32;
                            bubble.SetPosition(p, center + new Vector3(Mathf.Cos(angle) * r, 0.25f, Mathf.Sin(angle) * r));
                            halo.SetPosition(p, center + new Vector3(Mathf.Cos(angle) * r * 0.65f, 0.45f, Mathf.Sin(angle) * r * 0.65f));
                        }
                        continue;
                    }
                    var line = Line(5, 0.2f, i % 2 == 0 ? a : b, true);
                    float height = kind == FusionKind.SteamBurst ? 0.8f + Mathf.Sin(age * 6 + i) * 0.3f : 1.8f;
                    line.SetPosition(0, center + Vector3.left * 0.5f); line.SetPosition(1, center + Vector3.up * height); line.SetPosition(2, center + Vector3.right * 0.5f); line.SetPosition(3, center + Vector3.forward * 0.5f); line.SetPosition(4, center + Vector3.back * 0.5f);
                    var glow = Line(2, 0.07f, i % 2 == 0 ? b : a); glow.SetPosition(0, center); glow.SetPosition(1, center + Vector3.up * height * 1.2f);
                }
            }
            // Small orbiting sparks and a ground seal make the automatic skill readable
            // without the towering geometry and wide coverage of an ultimate.
            Ring(2.4f, 0.08f, -age * 0.8f, 0.06f, b, kind == FusionKind.StoneTomb ? 6 : 48);
            for (int i = 0; i < 6; i++)
            {
                var spark = Line(8, 0.08f, i % 2 == 0 ? a : b);
                for (int p = 0; p < 8; p++)
                {
                    float angle = i * Mathf.PI / 3 + age * 2 + p * 0.035f;
                    float radius = 2.4f + Mathf.Sin(age * 4 + i) * 0.25f;
                    spark.SetPosition(p, new Vector3(Mathf.Cos(angle) * radius, 0.25f + p * 0.04f, Mathf.Sin(angle) * radius));
                }
            }
            for (int i = used; i < ribbons.Length; i++) ribbons[i].enabled = false;
        }
    }
}
