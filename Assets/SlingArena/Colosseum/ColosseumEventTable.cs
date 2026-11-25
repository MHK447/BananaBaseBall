using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace SlingArena.Colosseum
{
    /// <summary>
    /// The balance sheet for the map events (see ColosseumEvents). It is kept in the game's spreadsheet as three
    /// tabs, and brought into the project as three CSV files under Resources/ColosseumTables:
    ///   MapEventInfo   one row for each event: whether it is in play, how often it is picked, and when it comes.
    ///   MapEventValue  the numbers of each event, one to a row (how many, how big, how much it hurts).
    ///   MapEventRule   what a stage, a map or an attempt at a stage changes: events that always come there,
    ///                  events that never do, and how many are picked at random.
    /// Anything missing from the files, or written wrongly in them, falls back to the values below, so a broken
    /// sheet can never stop a match; what was wrong is listed in Problems.
    /// </summary>
    public sealed class ColosseumEventTable
    {
        public const string Gate = "power_gate", Quake = "quake", Portal = "portal", Lava = "lava";
        public const string Spike = "spike", Ice = "ice", Bumper = "bumper", Crate = "crate";
        /// <summary>Events that belong to one map of the journey and come nowhere else: the tide of the Tidal Bastion, the fire of the Ember Crown.</summary>
        public const string Tide = "tide", Fire = "fire_pillar";
        /// <summary>Every event there is, in the order the sheet lists them.</summary>
        public static readonly string[] Keys = { Gate, Quake, Portal, Lava, Spike, Ice, Bumper, Crate, Tide, Fire };
        public const string Folder = "ColosseumTables", InfoName = "MapEventInfo", ValueName = "MapEventValue", RuleName = "MapEventRule";

        public enum Appear { Off, Always, Random }

        /// <summary>One row of MapEventInfo.</summary>
        public sealed class Info
        {
            public string Key;
            public Appear Appear;
            public float Weight;
            /// <summary>The event comes at the start of a round picked between these two.</summary>
            public int RoundMin, RoundMax, UnlockArena;
            /// <summary>How many rounds it stays; 0 for the rest of the set.</summary>
            public int Duration;
            /// <summary>The one map it comes on, as Plain writes a map's name; empty for an event that comes on any map.</summary>
            public string Map = string.Empty;
            /// <summary>The maps it never comes on, as Plain writes their names: no sheet of ice forms in the desert.</summary>
            public readonly List<string> Away = new List<string>();

            public bool Here(string map)
            {
                string plain = Plain(map);
                return (Map.Length == 0 || Map == plain) && !Away.Contains(plain);
            }
        }

        /// <summary>One row of MapEventRule.</summary>
        public sealed class Rule
        {
            public int Id;
            public bool Use = true, EditorTest;
            public readonly List<string> Maps = new List<string>();
            public int StageFrom, StageTo, AttemptFrom, AttemptTo;
            public int RandomCount = -1;
            public readonly List<string> Force = new List<string>(), Ban = new List<string>();
            /// <summary>The round a forced event is pinned to, where the row says so ("ice@1").</summary>
            public readonly Dictionary<string, int> ForceRound = new Dictionary<string, int>();

            public bool Fits(int stage, int attempt, string map, bool editor)
            {
                if (!Use) return false;
                if (EditorTest) return editor;
                if (StageFrom > 0 && stage < StageFrom) return false;
                if (StageTo > 0 && stage > StageTo) return false;
                if (AttemptFrom > 0 && attempt < AttemptFrom) return false;
                if (AttemptTo > 0 && attempt > AttemptTo) return false;
                return Maps.Count == 0 || Maps.Contains(Plain(map));
            }
        }

        /// <summary>What the rules come to for one match.</summary>
        public sealed class Verdict
        {
            public int RandomCount = 1;
            public readonly List<string> Forced = new List<string>();
            public readonly HashSet<string> Banned = new HashSet<string>();
            /// <summary>Forced events whose round a rule has pinned, in place of the round MapEventInfo would give.</summary>
            public readonly Dictionary<string, int> Rounds = new Dictionary<string, int>();
        }

        /// <summary>An event that is going to happen in a set.</summary>
        public struct Planned
        {
            public string Key;
            /// <summary>The round it comes at the start of, and the round it leaves at the start of (0: it stays).</summary>
            public int Round, Ends;
        }

        struct InfoSpec
        {
            public string Key, Map, Away; public Appear Appear; public float Weight; public int RoundMin, RoundMax, Duration, UnlockArena;
            /// <summary>`map` is the one map the event comes on; `away` names the maps it never comes on, with ';' between them.</summary>
            public InfoSpec(string key, Appear appear, float weight, int roundMin, int roundMax, int duration, int unlockArena, string map = "", string away = "")
            { Key = key; Appear = appear; Weight = weight; RoundMin = roundMin; RoundMax = roundMax; Duration = duration; UnlockArena = unlockArena; Map = map; Away = away; }
        }

        struct ValueSpec
        {
            public string Event, Name; public float Default, Min, Max;
            public ValueSpec(string @event, string name, float @default, float min, float max)
            { Event = @event; Name = name; Default = @default; Min = min; Max = max; }
        }

        /// <summary>The longest a set runs, in rounds: an event set to come later than this never comes.</summary>
        public const int LastRound = ColosseumGame.RoundLimit;

        static readonly InfoSpec[] InfoSpecs =
        {
            new InfoSpec(Gate, Appear.Random, 10f, 4, 4, 0, 2),
            new InfoSpec(Quake, Appear.Always, 0f, 3, 3, 0, 5),
            new InfoSpec(Portal, Appear.Random, 10f, 2, 5, 0, 4),
            new InfoSpec(Lava, Appear.Random, 10f, 3, 6, 0, 3),
            new InfoSpec(Spike, Appear.Random, 10f, 3, 6, 6, 5),
            new InfoSpec(Ice, Appear.Random, 10f, 2, 5, 0, 4, away: "PHARAOHSANDS"),
            new InfoSpec(Bumper, Appear.Random, 10f, 2, 5, 0, 3),
            new InfoSpec(Crate, Appear.Random, 10f, 3, 7, 4, 2),
            new InfoSpec(Tide, Appear.Random, 10f, 2, 5, 4, 2, "TIDALBASTION"),
            new InfoSpec(Fire, Appear.Random, 10f, 2, 5, 5, 3, "EMBERCROWN"),
        };

        // Every number an event has: what it is unless the sheet says otherwise, and the least and most it may be.
        static readonly ValueSpec[] ValueSpecs =
        {
            new ValueSpec(Gate, "damage_scale", 1.5f, 1f, 5f),
            new ValueSpec(Gate, "grow_scale", 1.4f, 1f, 2.2f),
            new ValueSpec(Gate, "gate_size", .7f, .3f, 1.4f),

            new ValueSpec(Quake, "gap_count_max", 3f, 1f, 3f),
            new ValueSpec(Quake, "gap_budget", 7f, 2f, 7f),

            new ValueSpec(Portal, "pair_count", 1f, 1f, 2f),
            new ValueSpec(Portal, "radius", .85f, .5f, 1.6f),
            new ValueSpec(Portal, "exit_speed_scale", 1f, .3f, 2f),
            new ValueSpec(Portal, "min_speed", 2.5f, 0f, 20f),
            new ValueSpec(Portal, "cooldown", .5f, .1f, 5f),

            new ValueSpec(Lava, "vent_count", 4f, 1f, 8f),
            new ValueSpec(Lava, "radius", 1.25f, .6f, 3f),
            new ValueSpec(Lava, "damage", 18f, 0f, 999f),
            new ValueSpec(Lava, "knock_up", 9f, 0f, 20f),
            new ValueSpec(Lava, "knock_out", 4f, 0f, 20f),
            new ValueSpec(Lava, "period_turn", 1f, 1f, 10f),

            new ValueSpec(Spike, "wall_count", 2f, 1f, 6f),
            new ValueSpec(Spike, "length", 3.2f, 1.2f, 6f),
            new ValueSpec(Spike, "damage", 16f, 0f, 999f),
            new ValueSpec(Spike, "min_speed", 4f, 0f, 30f),
            new ValueSpec(Spike, "bounce", .7f, .2f, 3f),

            new ValueSpec(Ice, "patch_count", 1f, 1f, 4f),
            new ValueSpec(Ice, "radius", 4f, 1f, 5f),
            new ValueSpec(Ice, "grip", .15f, 0f, 1f),

            new ValueSpec(Bumper, "pillar_count", 3f, 1f, 8f),
            new ValueSpec(Bumper, "radius", .5f, .3f, 1f),
            new ValueSpec(Bumper, "bounce", 1.25f, .5f, 3f),
            new ValueSpec(Bumper, "min_bounce_speed", 9f, 0f, 27f),
            new ValueSpec(Bumper, "damage", 0f, 0f, 999f),

            new ValueSpec(Crate, "hits", 1f, 1f, 5f),
            new ValueSpec(Crate, "heal_weight", 40f, 0f, 1000f),
            new ValueSpec(Crate, "shield_weight", 35f, 0f, 1000f),
            new ValueSpec(Crate, "extra_turn_weight", 25f, 0f, 1000f),
            new ValueSpec(Crate, "heal_amount", 30f, 0f, 999f),
            new ValueSpec(Crate, "shield_amount", 30f, 0f, 999f),
            new ValueSpec(Crate, "shield_round", 3f, 0f, 15f),

            new ValueSpec(Tide, "sling_scale", .9f, .5f, 1f),

            new ValueSpec(Fire, "pillar_count", 4f, 1f, 8f),
            new ValueSpec(Fire, "radius", .55f, .3f, 1.2f),
            new ValueSpec(Fire, "damage", 14f, 0f, 999f),
            new ValueSpec(Fire, "burn_damage", 5f, 0f, 999f),
            new ValueSpec(Fire, "burn_turn", 2f, 0f, 10f),
            new ValueSpec(Fire, "knock_out", 9f, 0f, 27f),
            new ValueSpec(Fire, "knock_up", 5f, 0f, 20f),
            new ValueSpec(Fire, "period_turn", 1f, 1f, 10f),
        };

        // Other names an event may be written by in the sheet, with the spaces taken out.
        static readonly Dictionary<string, string> Aliases = new Dictionary<string, string>
        {
            { "powergate", Gate }, { "gate", Gate }, { "boost", Gate }, { "게이트", Gate }, { "공격력게이트", Gate }, { "공격력증가게이트", Gate }, { "공격력증가", Gate },
            { "earthquake", Quake }, { "지진", Quake },
            { "portals", Portal }, { "twinportal", Portal }, { "twinportals", Portal }, { "포탈", Portal }, { "쌍둥이포탈", Portal },
            { "lavavent", Lava }, { "lavavents", Lava }, { "용암", Lava }, { "용암분출구", Lava },
            { "spikes", Spike }, { "spikewall", Spike }, { "spikewalls", Spike }, { "가시", Spike }, { "가시벽", Spike },
            { "icefield", Ice }, { "icepatch", Ice }, { "얼음", Ice }, { "얼음판", Ice }, { "얼음벽", Ice },
            { "bumpers", Bumper }, { "pillar", Bumper }, { "범퍼", Bumper }, { "범퍼기둥", Bumper },
            { "supply", Crate }, { "supplycrate", Crate }, { "box", Crate }, { "상자", Crate }, { "보급상자", Crate }, { "황제의보급상자", Crate },
            { "hightide", Tide }, { "flood", Tide }, { "밀물", Tide }, { "만조", Tide }, { "침수", Tide },
            { "fire", Fire }, { "firepillars", Fire }, { "pillaroffire", Fire }, { "불기둥", Fire }, { "화염기둥", Fire },
        };

        readonly Dictionary<string, Info> infos = new Dictionary<string, Info>();
        readonly Dictionary<string, float> values = new Dictionary<string, float>();
        readonly List<Rule> rules = new List<Rule>();

        /// <summary>What was wrong with the rows that were read, a line for each; empty when the sheet is sound.</summary>
        public readonly List<string> Problems = new List<string>();
        /// <summary>Where the numbers came from: "sheet", or "defaults" when there were no files to read.</summary>
        public string Source = "defaults";

        public IList<Rule> Rules { get { return rules; } }

        ColosseumEventTable()
        {
            foreach (InfoSpec spec in InfoSpecs)
            {
                infos[spec.Key] = new Info
                {
                    Key = spec.Key, Appear = spec.Appear, Weight = spec.Weight, RoundMin = spec.RoundMin, RoundMax = spec.RoundMax, Duration = spec.Duration, UnlockArena = spec.UnlockArena,
                    Map = spec.Map ?? string.Empty
                };
                foreach (string away in (spec.Away ?? string.Empty).Split(';'))
                    if (away.Length > 0) infos[spec.Key].Away.Add(away);
            }
            foreach (ValueSpec spec in ValueSpecs) values[spec.Event + "/" + spec.Name] = spec.Default;
        }

        /// <summary>The table as it stands with no sheet at all: the old game, plus one new event picked at random.</summary>
        public static ColosseumEventTable Defaults()
        {
            var table = new ColosseumEventTable();
            table.rules.Add(new Rule { Id = 1, RandomCount = 1 });
            return table;
        }

        /// <summary>
        /// The table from the text of the three CSV files; null for a file that is not there. A file that is
        /// missing leaves that part of the table as it is in Defaults.
        /// </summary>
        public static ColosseumEventTable Parse(string infoText, string valueText, string ruleText)
        {
            var table = new ColosseumEventTable();
            bool any = false;
            if (!string.IsNullOrEmpty(infoText)) { table.ReadInfo(Cells(infoText)); any = true; }
            if (!string.IsNullOrEmpty(valueText)) { table.ReadValues(Cells(valueText)); any = true; }
            if (!string.IsNullOrEmpty(ruleText)) { table.ReadRules(Cells(ruleText)); any = true; }
            if (table.rules.Count == 0) table.rules.Add(new Rule { Id = 1, RandomCount = 1 });
            if (any) table.Source = "sheet";
            return table;
        }

        /// <summary>
        /// The table the game plays by. In the editor the files are read straight off the disk, so that what was
        /// just brought in from the sheet counts from the very next match; a build reads them from its resources.
        /// </summary>
        public static ColosseumEventTable Load()
        {
            try
            {
                ColosseumEventTable table = Parse(Read(InfoName), Read(ValueName), Read(RuleName));
#if UNITY_EDITOR
                foreach (string problem in table.Problems) Debug.LogWarning("[MapEvent] " + problem);
#endif
                return table;
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception);
                return Defaults();
            }
        }

        static string Read(string name)
        {
#if UNITY_EDITOR
            string path = Application.dataPath + "/SlingArena/Colosseum/Resources/" + Folder + "/" + name + ".csv";
            if (System.IO.File.Exists(path)) return System.IO.File.ReadAllText(path);
#endif
            var asset = Resources.Load<TextAsset>(Folder + "/" + name);
            return asset != null ? asset.text : null;
        }

        // ------------------------------------------------------------------ what the game asks

        public Info Get(string key) { return infos[key]; }

        /// <summary>One number of an event, by the name it has in MapEventValue.</summary>
        public float Value(string key, string name)
        {
            return values.TryGetValue(key + "/" + name, out float value) ? value : 0f;
        }

        public int Count(string key, string name) { return Mathf.RoundToInt(Value(key, name)); }

        /// <summary>True for an event that may come on this map: one that belongs to another map never does.</summary>
        public bool Here(string key, string map) { return infos.TryGetValue(key, out Info info) && info.Here(map); }

        /// <summary>The event that belongs to this map and to no other, once the stage has it; null for a map with none.</summary>
        public string Own(string map, int stage)
        {
            string plain = Plain(map);
            foreach (string key in Keys)
            {
                Info info = infos[key];
                if (info.Map.Length > 0 && info.Map == plain && info.Appear != Appear.Off && stage >= info.UnlockArena) return key;
            }
            return null;
        }

        /// <summary>
        /// What the rules come to for a match at this stage, on this map, at this attempt. The rows are read from
        /// the top down, and every row that fits has its say: a lower row's number of random events replaces a
        /// higher row's; an event a row forces is forced (and no longer banned), an event a row bans is banned
        /// (and no longer forced). So the further down a row is, the more it counts.
        /// </summary>
        public Verdict Resolve(int stage, int attempt, string map, bool editor)
        {
            var verdict = new Verdict();
            foreach (Rule rule in rules)
            {
                if (!rule.Fits(stage, attempt, map, editor)) continue;
                if (rule.RandomCount >= 0) verdict.RandomCount = rule.RandomCount;
                foreach (string key in rule.Force)
                {
                    verdict.Banned.Remove(key);
                    if (!verdict.Forced.Contains(key)) verdict.Forced.Add(key);
                    // The row that forces it last also says when: at the round it names, or (naming none) whenever the event usually comes.
                    if (rule.ForceRound.TryGetValue(key, out int at)) verdict.Rounds[key] = at;
                    else verdict.Rounds.Remove(key);
                }
                foreach (string key in rule.Ban)
                {
                    verdict.Forced.Remove(key);
                    verdict.Rounds.Remove(key);
                    verdict.Banned.Add(key);
                }
            }
            return verdict;
        }

        /// <summary>
        /// The events of one set, in the order they come. An event that is switched off never comes. One that
        /// always comes does, unless the rules ban it there. One the rules force comes. And of the rest that may
        /// be picked at random, as many are picked as the rules allow, the heavier the more often.
        /// </summary>
        public List<Planned> Roll(int stage, int attempt, string map, bool editor, System.Random dice, List<string> reserves = null)
        {
            Verdict verdict = Resolve(stage, attempt, map, editor);
            var chosen = new List<string>();
            var pool = new List<Info>();
            foreach (string key in Keys)
            {
                Info info = infos[key];
                if (info.Appear == Appear.Off || stage < info.UnlockArena || !info.Here(map)) continue;
                bool forced = verdict.Forced.Contains(key), banned = verdict.Banned.Contains(key);
                if (forced || (info.Appear == Appear.Always && !banned)) chosen.Add(key);
                else if (info.Appear == Appear.Random && !banned && info.Weight > 0f) pool.Add(info);
            }
            for (int pick = 0; pool.Count > 0 && (pick < verdict.RandomCount || reserves != null); pick++)
            {
                float total = 0f;
                foreach (Info info in pool) total += StageWeight(info, stage);
                double at = dice.NextDouble() * total;
                int index = pool.Count - 1;
                for (int i = 0; i < pool.Count; i++)
                {
                    at -= StageWeight(pool[i], stage);
                    if (at < 0d) { index = i; break; }
                }
                string key = pool[index].Key;
                if (pick < verdict.RandomCount) chosen.Add(key);
                // Keep the same weights and bans for additional battle slots and alternative placements.
                else if (key != Quake) reserves.Add(key);
                pool.RemoveAt(index);
            }
            var plan = new List<Planned>();
            foreach (string key in chosen)
            {
                Info info = infos[key];
                int round = verdict.Rounds.TryGetValue(key, out int pinned) ? Mathf.Clamp(pinned, 1, LastRound) : dice.Next(info.RoundMin, info.RoundMax + 1);
                plan.Add(new Planned { Key = key, Round = round, Ends = info.Duration > 0 ? round + info.Duration : 0 });
            }
            if (reserves == null) plan.Sort((a, b) => a.Round != b.Round ? a.Round.CompareTo(b.Round) : System.Array.IndexOf(Keys, a.Key).CompareTo(System.Array.IndexOf(Keys, b.Key)));
            return plan;
        }

        static float StageWeight(Info info, int stage) => info.Weight * (info.UnlockArena == stage ? 3f : 1f);

        // ------------------------------------------------------------------ reading the files

        /// <summary>The event a cell names, or null: its key, or one of the other names it goes by.</summary>
        public static string KeyOf(string cell)
        {
            if (string.IsNullOrEmpty(cell)) return null;
            string name = cell.Trim().ToLowerInvariant();
            if (System.Array.IndexOf(Keys, name) >= 0) return name;
            name = name.Replace(" ", string.Empty).Replace("_", string.Empty).Replace("-", string.Empty);
            foreach (string key in Keys)
            {
                if (key.Replace("_", string.Empty) == name) return key;
            }
            return Aliases.TryGetValue(name, out string known) ? known : null;
        }

        /// <summary>A map's name with everything but its letters and digits taken out, in capitals.</summary>
        public static string Plain(string name)
        {
            if (string.IsNullOrEmpty(name)) return string.Empty;
            var plain = new System.Text.StringBuilder(name.Length);
            foreach (char letter in name)
            {
                if (char.IsLetterOrDigit(letter)) plain.Append(char.ToUpperInvariant(letter));
            }
            return plain.ToString();
        }

        void ReadInfo(List<string[]> rows)
        {
            Dictionary<string, int> columns = Header(rows, "event_key", InfoName);
            if (columns == null) return;
            var seen = new HashSet<string>();
            for (int r = 1; r < rows.Count; r++)
            {
                string[] row = rows[r];
                string cell = Cell(row, columns, "event_key");
                if (string.IsNullOrEmpty(cell)) continue;
                string key = KeyOf(cell);
                string where = InfoName + " " + (r + 1) + "행";
                if (key == null) { Problems.Add(where + ": 모르는 이벤트 '" + cell + "'"); continue; }
                if (!seen.Add(key)) { Problems.Add(where + ": '" + key + "' 가 두 번 나옵니다. 아래 행을 씁니다."); }
                Info info = infos[key];
                string appear = Cell(row, columns, "appear").ToLowerInvariant();
                // In the older online sheet, Gate and Quake were unconditional fixtures.
                // Keep their new arena timings until a tier-aware sheet specifies replacements.
                if (!columns.ContainsKey("unlock_arena") && (key == Gate || key == Quake)) continue;
                if (appear == "always" || appear == "항상" || appear == "고정") info.Appear = Appear.Always;
                else if (appear == "random" || appear == "랜덤") info.Appear = Appear.Random;
                else if (appear == "off" || appear == "끔" || appear == "사용안함" || appear == "0") info.Appear = Appear.Off;
                else if (appear.Length > 0) Problems.Add(where + ": appear 는 always / random / off 중 하나여야 합니다 ('" + appear + "')");
                info.UnlockArena = Mathf.Max(2, Mathf.RoundToInt(Number(row, columns, "unlock_arena", info.UnlockArena, where)));
                info.Weight = Mathf.Max(0f, Number(row, columns, "weight", info.Weight, where));
                int from = Mathf.RoundToInt(Number(row, columns, "start_round_min", info.RoundMin, where));
                int to = Mathf.RoundToInt(Number(row, columns, "start_round_max", from, where));
                if (from < 1 || to < from)
                {
                    Problems.Add(where + ": 등장 라운드는 1 이상이고 min ≤ max 여야 합니다 (" + from + "~" + to + ")");
                    from = Mathf.Max(1, from);
                    to = Mathf.Max(from, to);
                }
                info.RoundMin = from;
                info.RoundMax = to;
                info.Duration = Mathf.Max(0, Mathf.RoundToInt(Number(row, columns, "duration_round", info.Duration, where)));
                // A map named in the row ties the event to that map; "all" frees it to come anywhere. Left blank, it stays as it is.
                string home = Plain(Cell(row, columns, "map"));
                if (home.Length > 0) info.Map = home == "ALL" || home == "ANY" ? string.Empty : home;
                // Maps named under ban_map are the ones the event never comes on, with ';' between them; "none" lifts
                // what is set here. Left blank, they stay as they are.
                string banned = Cell(row, columns, "ban_map");
                if (banned.Length > 0)
                {
                    info.Away.Clear();
                    foreach (string name in banned.Split(';', ','))
                    {
                        string away = Plain(name);
                        if (away.Length > 0 && away != "NONE" && away != "없음") info.Away.Add(away);
                    }
                }
            }
        }

        void ReadValues(List<string[]> rows)
        {
            Dictionary<string, int> columns = Header(rows, "event_key", ValueName);
            if (columns == null) return;
            for (int r = 1; r < rows.Count; r++)
            {
                string[] row = rows[r];
                string cell = Cell(row, columns, "event_key"), name = Cell(row, columns, "value_key").ToLowerInvariant();
                if (string.IsNullOrEmpty(cell) && string.IsNullOrEmpty(name)) continue;
                string key = KeyOf(cell);
                string where = ValueName + " " + (r + 1) + "행";
                if (key == null) { Problems.Add(where + ": 모르는 이벤트 '" + cell + "'"); continue; }
                bool known = false;
                foreach (ValueSpec spec in ValueSpecs)
                {
                    if (spec.Event != key || spec.Name != name) continue;
                    known = true;
                    float value = Number(row, columns, "value", spec.Default, where);
                    if (value < spec.Min || value > spec.Max)
                    {
                        Problems.Add(where + ": " + key + " / " + name + " 값 " + value.ToString(CultureInfo.InvariantCulture) + " 은(는) "
                            + spec.Min.ToString(CultureInfo.InvariantCulture) + "~" + spec.Max.ToString(CultureInfo.InvariantCulture) + " 범위를 벗어나 범위 안으로 맞췄습니다.");
                        value = Mathf.Clamp(value, spec.Min, spec.Max);
                    }
                    values[key + "/" + name] = value;
                }
                if (!known) Problems.Add(where + ": '" + key + "' 에는 '" + name + "' 값이 없습니다.");
            }
        }

        void ReadRules(List<string[]> rows)
        {
            Dictionary<string, int> columns = Header(rows, "force_events", RuleName);
            if (columns == null) return;
            for (int r = 1; r < rows.Count; r++)
            {
                string[] row = rows[r];
                bool blank = true;
                foreach (string cell in row)
                {
                    if (!string.IsNullOrWhiteSpace(cell)) blank = false;
                }
                if (blank) continue;
                string where = RuleName + " " + (r + 1) + "행";
                var rule = new Rule { Id = Mathf.RoundToInt(Number(row, columns, "idx", r, where)) };
                rule.Use = Flag(Cell(row, columns, "use"), true);
                rule.EditorTest = Flag(Cell(row, columns, "editor_test"), false);
                foreach (string map in Cell(row, columns, "map").Split(';', ','))
                {
                    string plain = Plain(map);
                    if (plain.Length > 0 && plain != "ALL" && plain != "ANY") rule.Maps.Add(plain);
                }
                rule.StageFrom = Mathf.RoundToInt(Number(row, columns, "stage_from", 0f, where));
                rule.StageTo = Mathf.RoundToInt(Number(row, columns, "stage_to", 0f, where));
                rule.AttemptFrom = Mathf.RoundToInt(Number(row, columns, "attempt_from", 0f, where));
                rule.AttemptTo = Mathf.RoundToInt(Number(row, columns, "attempt_to", 0f, where));
                rule.RandomCount = Mathf.RoundToInt(Number(row, columns, "random_count", -1f, where));
                Names(Cell(row, columns, "force_events"), rule.Force, where + " force_events", rule.ForceRound);
                Names(Cell(row, columns, "ban_events"), rule.Ban, where + " ban_events", null);
                foreach (string key in rule.Force)
                {
                    if (rule.Ban.Contains(key)) Problems.Add(where + ": '" + key + "' 가 force_events 와 ban_events 양쪽에 있습니다. 금지로 처리합니다.");
                }
                rules.Add(rule);
            }
        }

        /// <summary>
        /// The events a cell lists. In force_events a name may be followed by the round it is to come at
        /// ("ice@1"): those rounds are put in the second list.
        /// </summary>
        void Names(string cell, List<string> into, string where, Dictionary<string, int> rounds)
        {
            if (string.IsNullOrWhiteSpace(cell)) return;
            foreach (string part in cell.Split(';', ',', '\n', '/'))
            {
                string name = part.Trim();
                if (name.Length == 0 || name == "-") continue;
                int round = 0, mark = name.IndexOf('@');
                if (mark >= 0)
                {
                    string when = name.Substring(mark + 1).Trim().TrimStart('R', 'r');
                    name = name.Substring(0, mark).Trim();
                    if (rounds == null) Problems.Add(where + ": '@라운드'는 force_events 에서만 쓸 수 있어 무시합니다.");
                    else if (!int.TryParse(when, NumberStyles.Integer, CultureInfo.InvariantCulture, out round) || round < 1 || round > LastRound)
                    {
                        Problems.Add(where + ": '" + part.Trim() + "' 의 라운드는 1 ~ " + LastRound + " 사이 숫자여야 합니다. 라운드 지정 없이 처리합니다.");
                        round = 0;
                    }
                }
                string lowered = name.ToLowerInvariant();
                if (lowered == "all" || name == "전체" || name == "전부")
                {
                    foreach (string key in Keys)
                    {
                        if (!into.Contains(key)) into.Add(key);
                        if (rounds != null && round > 0) rounds[key] = round;
                    }
                    continue;
                }
                string known = KeyOf(name);
                if (known == null) { Problems.Add(where + ": 모르는 이벤트 '" + name + "'"); continue; }
                if (!into.Contains(known)) into.Add(known);
                if (rounds != null && round > 0) rounds[known] = round;
            }
        }

        static bool Flag(string cell, bool otherwise)
        {
            string text = cell.Trim().ToLowerInvariant();
            if (text.Length == 0) return otherwise;
            return text == "1" || text == "true" || text == "yes" || text == "y" || text == "o" || text == "on" || text == "사용";
        }

        /// <summary>Where each column is, by the name in the first row; columns whose name starts with # are notes and are left out.</summary>
        Dictionary<string, int> Header(List<string[]> rows, string needed, string name)
        {
            var columns = new Dictionary<string, int>();
            if (rows.Count > 0)
            {
                for (int c = 0; c < rows[0].Length; c++)
                {
                    string title = rows[0][c].Trim().ToLowerInvariant();
                    if (title.Length == 0 || title[0] == '#') continue;
                    columns[title] = c;
                }
            }
            if (columns.ContainsKey(needed)) return columns;
            Problems.Add(name + ": 첫 행에 '" + needed + "' 열이 없어 이 탭은 읽지 않고 기본값을 씁니다.");
            return null;
        }

        static string Cell(string[] row, Dictionary<string, int> columns, string name)
        {
            return columns.TryGetValue(name, out int at) && at < row.Length && row[at] != null ? row[at].Trim() : string.Empty;
        }

        float Number(string[] row, Dictionary<string, int> columns, string name, float otherwise, string where)
        {
            string cell = Cell(row, columns, name);
            if (cell.Length == 0) return otherwise;
            // A percentage, or a number written with a comma for the decimal point, is still a number.
            bool percent = cell.EndsWith("%");
            string text = (percent ? cell.Substring(0, cell.Length - 1) : cell).Trim();
            if (text.IndexOf('.') < 0 && text.IndexOf(',') >= 0) text = text.Replace(',', '.');
            if (float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float value) && !float.IsNaN(value) && !float.IsInfinity(value))
                return percent ? value / 100f : value;
            Problems.Add(where + ": " + name + " 칸의 '" + cell + "' 은(는) 숫자가 아니어서 기본값을 씁니다.");
            return otherwise;
        }

        /// <summary>The rows and cells of a CSV file: commas between cells, and quotes round a cell that has a comma, a quote or a line break in it.</summary>
        public static List<string[]> Cells(string text)
        {
            var rows = new List<string[]>();
            var row = new List<string>();
            var cell = new System.Text.StringBuilder();
            bool quoted = false;
            int start = text.Length > 0 && text[0] == '﻿' ? 1 : 0;
            for (int i = start; i < text.Length; i++)
            {
                char letter = text[i];
                if (quoted)
                {
                    if (letter != '"') { cell.Append(letter); continue; }
                    if (i + 1 < text.Length && text[i + 1] == '"') { cell.Append('"'); i++; }
                    else quoted = false;
                    continue;
                }
                if (letter == '"') { quoted = true; continue; }
                if (letter == ',') { row.Add(cell.ToString()); cell.Length = 0; continue; }
                if (letter == '\r') continue;
                if (letter == '\n')
                {
                    row.Add(cell.ToString());
                    cell.Length = 0;
                    rows.Add(row.ToArray());
                    row.Clear();
                    continue;
                }
                cell.Append(letter);
            }
            if (cell.Length > 0 || row.Count > 0)
            {
                row.Add(cell.ToString());
                rows.Add(row.ToArray());
            }
            return rows;
        }

        /// <summary>The whole table in a few lines, for the editor's console.</summary>
        public string Summary()
        {
            var text = new System.Text.StringBuilder();
            foreach (string key in Keys)
            {
                Info info = infos[key];
                text.Append(key).Append(": ").Append(info.Appear.ToString().ToLowerInvariant());
                if (info.Appear == Appear.Random) text.Append(" w").Append(info.Weight.ToString(CultureInfo.InvariantCulture));
                text.Append(" R").Append(info.RoundMin);
                if (info.RoundMax != info.RoundMin) text.Append('-').Append(info.RoundMax);
                if (info.Duration > 0) text.Append(" +").Append(info.Duration);
                text.Append("  ");
            }
            text.Append("| rules ").Append(rules.Count);
            return text.ToString();
        }
    }
}
