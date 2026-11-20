using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace StarFall
{
    public enum ResearchId { Vitality, Elements, Collection }

    public static class PermanentRules
    {
        public const int MaxRank = 5;
        public static readonly int[] Costs = { 20, 35, 55, 80, 110 };
        public static readonly string[] Names = { "生命研习", "元素研习", "引魂研习" };
        public static readonly string[] Descriptions = { "每级最大生命 +10", "每级全元素效能 +5个百分点", "每级角色基础吸魂半径 +0.2" };
        public static readonly string[] StaffNames = { "基础法杖", "引魂法杖" };
        public static int Cost(int rank) => rank >= 0 && rank < MaxRank ? Costs[rank] : 0;
    }

    [Serializable]
    public sealed class PermanentProgress
    {
        public int version = 1;
        public long souls;
        public int proofs, staff, gemIndex;
        public int[] ranks = new int[3];
        public string activeRunId = "", lastRunId = "", lastOutcome = "尚未出击";
        public int activeSouls, lastSouls;
        public bool lastProof;
    }

    // Writes are committed before publishing the new in-memory state. No player data is reset on read errors.
    public sealed class ProgressStore
    {
        [Serializable] private sealed class Envelope { public string payload; public string checksum; }
        private PermanentProgress data;
        public string Path { get; }
        public string Error { get; private set; }
        public string Notice { get; private set; }
        public bool Ready => data != null;
        public PermanentProgress Snapshot => data == null ? null : Clone(data);
        public Action BeforeWriteForTests;

        public ProgressStore(string path) { Path = path; Load(); }
        public bool Load()
        {
            Error = ""; Notice = "";
            try
            {
                if (!File.Exists(Path) && !File.Exists(Path + ".bak")) return Commit(new PermanentProgress());
                try { data = Read(Path); }
                catch (NotSupportedException) { throw; }
                catch
                {
                    data = Read(Path + ".bak");
                    Notice = "主存档读取失败，已加载上次备份。";
                }
                if (!string.IsNullOrEmpty(data.activeRunId))
                    return Finish(data.activeRunId, data.activeSouls, false, "中断返还");
                return true;
            }
            catch (Exception ex) { data = null; Error = "无法读取存档，原文件已保留：" + ex.Message; return false; }
        }

        public bool BeginRun(out string id)
        {
            id = null;
            if (!Ready || !string.IsNullOrEmpty(data.activeRunId)) return Fail("已有行动尚未结算。");
            var next = Clone(data); next.activeRunId = Guid.NewGuid().ToString("N"); next.activeSouls = 0;
            if (!Commit(next)) return false;
            id = data.activeRunId; return true;
        }
        public bool Checkpoint(string id, int collected)
        {
            if (!Ready || data.activeRunId != id || string.IsNullOrEmpty(id) || collected < data.activeSouls) return Fail("行动记录不匹配。");
            if (collected == data.activeSouls) { Error = ""; return true; }
            var next = Clone(data); next.activeSouls = collected; return Commit(next);
        }
        public bool Finish(string id, int collected, bool proof, string outcome)
        {
            if (!Ready || string.IsNullOrEmpty(id)) return Fail("没有可结算的行动。");
            if (data.lastRunId == id) { Error = ""; return true; }
            if (data.activeRunId != id || collected < data.activeSouls) return Fail("行动记录不匹配。");
            var next = Clone(data);
            next.souls = checked(next.souls + collected);
            next.proofs = checked(next.proofs + (proof ? 1 : 0));
            next.lastRunId = id; next.lastOutcome = outcome; next.lastSouls = collected; next.lastProof = proof;
            next.activeRunId = ""; next.activeSouls = 0;
            return Commit(next);
        }
        public bool Upgrade(ResearchId id, int expectedRank)
        {
            int index = (int)id;
            if (!Ready || !string.IsNullOrEmpty(data.activeRunId) || index < 0 || index >= 3) return Fail("请在基地进行研习。");
            int rank = data.ranks[index], cost = PermanentRules.Cost(rank);
            if (rank != expectedRank || rank >= PermanentRules.MaxRank || data.souls < cost) return Fail("等级已变化、已满级或灵魂不足。");
            var next = Clone(data); next.souls -= cost; next.ranks[index]++; return Commit(next);
        }
        public bool Equip(int staff)
        {
            if (!Ready || !string.IsNullOrEmpty(data.activeRunId) || staff < 0 || staff >= PermanentRules.StaffNames.Length) return Fail("无法切换法杖。");
            var next = Clone(data); next.staff = staff; return Commit(next);
        }
        public bool EquipGem(int index)
        {
            if (!Ready || !string.IsNullOrEmpty(data.activeRunId) || index < 0 || index > 4) return Fail("请在基地选择宝石。");
            var next = Clone(data); next.gemIndex = index; return Commit(next);
        }

        private bool Commit(PermanentProgress next)
        {
            try
            {
                Validate(next);
                BeforeWriteForTests?.Invoke();
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path));
                string payload = JsonUtility.ToJson(next);
                byte[] bytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(new Envelope { payload = payload, checksum = Hash(payload) }, true));
                string temporary = Path + ".tmp";
                using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
                { stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
                // Preserve a known-good backup even when the primary is damaged.
                if (File.Exists(Path))
                {
                    bool primaryValid;
                    try { Read(Path); primaryValid = true; } catch { primaryValid = false; }
                    File.Replace(temporary, Path, primaryValid ? Path + ".bak" : null);
                }
                else File.Move(temporary, Path);
                data = next; Error = ""; return true;
            }
            catch (Exception ex) { return Fail("保存失败，请重试：" + ex.Message); }
        }
        private bool Fail(string message) { Error = message; return false; }
        private static PermanentProgress Clone(PermanentProgress value) => JsonUtility.FromJson<PermanentProgress>(JsonUtility.ToJson(value));
        private static string Hash(string text)
        {
            using (var sha = SHA256.Create()) return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(text)));
        }
        private static PermanentProgress Read(string path)
        {
            var envelope = JsonUtility.FromJson<Envelope>(File.ReadAllText(path));
            if (envelope == null || string.IsNullOrEmpty(envelope.payload) || envelope.checksum != Hash(envelope.payload)) throw new IOException("存档校验失败。");
            var value = JsonUtility.FromJson<PermanentProgress>(envelope.payload); Validate(value); return value;
        }
        private static void Validate(PermanentProgress value)
        {
            if (value == null) throw new IOException("存档为空。");
            if (value.version != 1) throw new NotSupportedException("不支持该存档版本。");
            if (value.souls < 0 || value.proofs < 0 || value.staff < 0 || value.staff >= PermanentRules.StaffNames.Length || value.ranks == null || value.ranks.Length != 3 || value.activeSouls < 0 || value.lastSouls < 0) throw new IOException("存档数值无效。");
            foreach (int rank in value.ranks) if (rank < 0 || rank > PermanentRules.MaxRank) throw new IOException("研习等级无效。");
            if (value.gemIndex < 0 || value.gemIndex > 4) throw new IOException("宝石类型无效。");
        }
    }
}
