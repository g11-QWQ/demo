using System.Collections.Generic;
using UnityEngine;

namespace StarFall
{
    // Shared four-way flow fields for each actor radius; exact rectangle sweeps handle movement and shots.
    public sealed class ArenaNavigation
    {
        private readonly Vector2 size;
        private readonly List<Rect> obstacles = new List<Rect>();
        private readonly Dictionary<float, Field> fields = new Dictionary<float, Field>();
        private readonly int width, height;
        private sealed class Field { public int Goal = -1; public bool[] Open; public int[] Cost; }
        public IReadOnlyList<Rect> Obstacles => obstacles;

        public ArenaNavigation(BattleConfig config)
        {
            size = config.arenaSize;
            width = Mathf.CeilToInt(size.x); height = Mathf.CeilToInt(size.y);
            if (config.enableObstacles && config.obstacles != null)
                foreach (var box in config.obstacles)
                    if (box.z > 0 && box.w > 0) obstacles.Add(new Rect(box.x - box.z / 2, box.y - box.w / 2, box.z, box.w));
        }

        public bool IsFree(Vector3 point, float radius)
        {
            if (Mathf.Abs(point.x) > size.x / 2 - radius || Mathf.Abs(point.z) > size.y / 2 - radius) return false;
            foreach (var box in obstacles)
                if (point.x >= box.xMin - radius && point.x <= box.xMax + radius
                    && point.z >= box.yMin - radius && point.z <= box.yMax + radius) return false;
            return true;
        }

        public Vector3 Move(Vector3 start, Vector3 displacement, float radius)
        {
            if (!IsFree(start, radius)) start = ClosestFree(start, radius);
            int steps = Mathf.Max(1, Mathf.CeilToInt(displacement.magnitude / 0.2f));
            var step = displacement / steps;
            for (int i = 0; i < steps; i++)
            {
                var candidate = start;
                candidate.x = Mathf.Clamp(start.x + step.x, -size.x / 2 + radius, size.x / 2 - radius);
                if (IsFree(candidate, radius)) start = candidate;
                candidate = start;
                candidate.z = Mathf.Clamp(start.z + step.z, -size.y / 2 + radius, size.y / 2 - radius);
                if (IsFree(candidate, radius)) start = candidate;
            }
            return start;
        }

        public Vector3 ClosestFree(Vector3 point, float radius)
        {
            if (IsFree(point, radius)) return point;
            float best = float.PositiveInfinity;
            Vector3 result = Vector3.zero;
            bool found = false;
            for (int i = 0; i < width * height; i++)
            {
                var p = Center(i, point.y);
                float distance = (p - point).sqrMagnitude;
                if (distance >= best || !IsFree(p, radius)) continue;
                best = distance; result = p; found = true;
            }
            if (!found) throw new System.InvalidOperationException("Arena has no walkable location for radius " + radius);
            return result;
        }

        public bool FirstWallHit(Vector3 start, Vector3 end, float radius, out float time)
        {
            time = float.PositiveInfinity;
            foreach (var obstacle in obstacles)
            {
                var expanded = Rect.MinMaxRect(obstacle.xMin - radius, obstacle.yMin - radius, obstacle.xMax + radius, obstacle.yMax + radius);
                if (Intersect(start, end, expanded, out float entry, out _)) time = Mathf.Min(time, entry);
            }
            var bounds = Rect.MinMaxRect(-size.x / 2 + radius, -size.y / 2 + radius, size.x / 2 - radius, size.y / 2 - radius);
            if (Intersect(start, end, bounds, out _, out float exit) && exit < 1) time = Mathf.Min(time, exit);
            // Rect.Contains excludes its maximum edges. Actors spawned exactly on the
            // right/top inset boundary must still be able to move back into the arena.
            if (start.x < bounds.xMin || start.x > bounds.xMax || start.z < bounds.yMin || start.z > bounds.yMax) time = 0;
            return time <= 1;
        }

        public bool ClearLine(Vector3 start, Vector3 end, float radius) => !FirstWallHit(start, end, radius, out float time) || time >= 0.99999f;

        private static bool Intersect(Vector3 start, Vector3 end, Rect box, out float entry, out float exit)
        {
            entry = 0; exit = 1;
            return Slab(start.x, end.x - start.x, box.xMin, box.xMax, ref entry, ref exit)
                && Slab(start.z, end.z - start.z, box.yMin, box.yMax, ref entry, ref exit);
        }

        private static bool Slab(float origin, float delta, float min, float max, ref float entry, ref float exit)
        {
            if (Mathf.Abs(delta) < 0.000001f) return origin >= min && origin <= max;
            float a = (min - origin) / delta, b = (max - origin) / delta;
            if (a > b) { float swap = a; a = b; b = swap; }
            entry = Mathf.Max(entry, a); exit = Mathf.Min(exit, b);
            return entry <= exit;
        }

        public Vector3 Direction(Vector3 position, Vector3 target, float radius)
        {
            target.y = position.y;
            if (ClearLine(position, target, radius)) return (target - position).normalized;
            if (!fields.TryGetValue(radius, out var field))
            {
                field = new Field { Open = new bool[width * height], Cost = new int[width * height] };
                for (int i = 0; i < field.Open.Length; i++) field.Open[i] = IsFree(Center(i, 0), radius + 0.05f);
                fields.Add(radius, field);
            }
            int goal = NearestOpen(target, field, false, radius);
            if (goal < 0) return Vector3.zero;
            if (goal != field.Goal)
            {
                field.Goal = goal;
                for (int i = 0; i < field.Cost.Length; i++) field.Cost[i] = int.MaxValue;
                var queue = new Queue<int>(); field.Cost[goal] = 0; queue.Enqueue(goal);
                while (queue.Count > 0)
                {
                    int current = queue.Dequeue();
                    foreach (int next in Neighbors(current))
                        if (field.Open[next] && field.Cost[next] == int.MaxValue)
                        { field.Cost[next] = field.Cost[current] + 1; queue.Enqueue(next); }
                }
            }
            int source = Index(position);
            if (!field.Open[source] || field.Cost[source] == int.MaxValue)
            {
                int reachable = NearestOpen(position, field, true, radius);
                return reachable < 0 ? Vector3.zero : (Center(reachable, position.y) - position).normalized;
            }
            int best = source;
            foreach (int next in Neighbors(source))
                if (field.Cost[next] < field.Cost[best] && ClearLine(position, Center(next, position.y), radius)) best = next;
            return (Center(best, position.y) - position).normalized;
        }

        private int NearestOpen(Vector3 point, Field field, bool requireReachable, float radius)
        {
            int direct = Index(point);
            if (field.Open[direct] && (!requireReachable || (field.Cost[direct] != int.MaxValue && ClearLine(point, Center(direct, point.y), radius)))) return direct;
            int best = -1; float distance = float.PositiveInfinity;
            for (int i = 0; i < field.Open.Length; i++)
            {
                if (!field.Open[i] || (requireReachable && field.Cost[i] == int.MaxValue)) continue;
                var p = Center(i, point.y); float candidate = (p - point).sqrMagnitude;
                if (candidate >= distance || (requireReachable && !ClearLine(point, p, radius))) continue;
                best = i; distance = candidate;
            }
            return best;
        }
        private int Index(Vector3 p) => Mathf.Clamp(Mathf.FloorToInt(p.x + size.x / 2), 0, width - 1)
            + Mathf.Clamp(Mathf.FloorToInt(p.z + size.y / 2), 0, height - 1) * width;
        private Vector3 Center(int index, float y) => new Vector3(index % width + 0.5f - size.x / 2, y, index / width + 0.5f - size.y / 2);
        private IEnumerable<int> Neighbors(int index)
        {
            int x = index % width, z = index / width;
            if (x > 0) yield return index - 1;
            if (x + 1 < width) yield return index + 1;
            if (z > 0) yield return index - width;
            if (z + 1 < height) yield return index + width;
        }
    }
}
