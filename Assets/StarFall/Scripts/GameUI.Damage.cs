using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace StarFall
{
    public sealed partial class GameUI
    {
        private sealed class DamageLabel { public Text Text; public Vector3 Position; public Color Color; public float Age, Side, Lift; }
        private RectTransform damageLayer;
        private readonly List<DamageLabel> damageLabels = new List<DamageLabel>();
        private readonly Stack<Text> damagePool = new Stack<Text>();
        private int damageSerial;
        private bool damageLayoutDirty;
        public int DamageNumberCount => damageLabels.Count;
        public int LastDamageNumber { get; private set; }
        public Color LastDamageColor { get; private set; }
        public static int RoundDamage(float value) => Mathf.Max(1, Mathf.FloorToInt(value + 0.5f));
        private void BuildDamageNumbers()
        {
            damageLayer = Box(canvas, "DamageNumbers", Vector2.zero, Vector2.zero); Stretch(damageLayer);
            Canvas.willRenderCanvases += LayoutDamageNumbers;
        }
        public void ShowDamage(Vector3 position, float amount, DamageElement element)
        {
            if (damageLayer == null || amount <= 0) return;
            LastDamageNumber = RoundDamage(amount); LastDamageColor = ElementCatalog.ColorFor(element);
            if (damageLabels.Count >= 160) return;
            Text text;
            if (damagePool.Count > 0) text = damagePool.Pop();
            else
            {
                text = Label(damageLayer, "Damage", "", Vector2.zero, new Vector2(130, 48), 21, Color.white);
                var outline = text.gameObject.AddComponent<Outline>(); outline.effectColor = new Color(0, 0, 0, 0.95f); outline.effectDistance = new Vector2(1.5f, -1.5f);
            }
            text.text = LastDamageNumber.ToString(); text.color = LastDamageColor; text.gameObject.SetActive(true);
            // Spread simultaneous area hits immediately, rather than letting all labels begin at one point.
            int slot = ++damageSerial % 21;
            damageLabels.Add(new DamageLabel { Text = text, Position = position, Color = LastDamageColor,
                Side = (slot % 7 - 3) * 19, Lift = (slot / 7) * 19 });
            damageLayoutDirty = true;
        }
        public void StepDamageNumbers(float delta)
        {
            for (int i = damageLabels.Count - 1; i >= 0; i--)
            {
                var label = damageLabels[i]; label.Age += delta;
                if (label.Age >= 0.9f) { label.Text.gameObject.SetActive(false); damagePool.Push(label.Text); damageLabels.RemoveAt(i); }
            }
            damageLayoutDirty = true;
        }
        private void LayoutDamageNumbers()
        {
            if (damageLayer == null || app == null) return;
            if (!damageLayoutDirty) return;
            damageLayoutDirty = false;
            var camera = Camera.main; if (camera == null) return;
            bool visible = app.Session.State == MatchState.Playing || app.Session.State == MatchState.Paused || app.Session.State == MatchState.ClaimingReward || app.Session.State == MatchState.ChoosingBuild;
            damageLayer.gameObject.SetActive(visible);
            foreach (var label in damageLabels)
            {
                var screen = camera.WorldToViewportPoint(label.Position);
                label.Text.enabled = screen.z > 0;
                float t = label.Age;
                label.Text.rectTransform.anchoredPosition = new Vector2((screen.x - 0.5f) * canvas.rect.width + label.Side * (0.65f + t * 0.45f),
                    (screen.y - 0.5f) * canvas.rect.height + 16 + label.Lift + 90 * t - 42 * t * t);
                label.Text.rectTransform.localScale = Vector3.one * (1 + 0.3f * Mathf.Exp(-t * 15));
                var color = label.Color; color.a = Mathf.Clamp01((0.9f - t) / 0.3f); label.Text.color = color;
            }
        }
        public void ClearDamageNumbers()
        {
            foreach (var label in damageLabels) { label.Text.gameObject.SetActive(false); damagePool.Push(label.Text); }
            damageLabels.Clear();
        }
        private void OnDestroy() => Canvas.willRenderCanvases -= LayoutDamageNumbers;
    }
}
