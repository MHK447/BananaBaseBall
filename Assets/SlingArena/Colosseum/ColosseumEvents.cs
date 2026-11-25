using System.Collections.Generic;
using UnityEngine;

namespace SlingArena.Colosseum
{
    /// <summary>
    /// One kind of map event, for as long as it is in play: what it looks like, and what it does to the heroes.
    /// ColosseumEvents decides when it comes and goes, and tells it what is happening in the match.
    /// </summary>
    abstract class MapFeature
    {
        protected ColosseumEvents host;
        protected Transform root;
        protected readonly MaterialPropertyBlock block = new MaterialPropertyBlock();
        bool built;

        /// <summary>The name the event goes by in the balance sheet.</summary>
        public abstract string Key { get; }
        /// <summary>The effect explained on the emperor's scroll.</summary>
        public abstract string Description { get; }
        /// <summary>The colour the event arrives in: the glow round the view, and the marks on the sand.</summary>
        public virtual Color Theme { get { return Color.white; } }
        /// <summary>How long it is heralded before the first of it stands on the court.</summary>
        public virtual float Lead { get { return ColosseumEvents.Herald; } }
        /// <summary>What the emperor makes of it while it is on its way (1 leap, 2 rage, 3 applause, 4 laugh, 5 shock; 0 nothing).</summary>
        public virtual int Mood { get { return 0; } }
        /// <summary>In play.</summary>
        public bool Live { get; protected set; }
        /// <summary>In the middle of something the match has to wait for.</summary>
        public virtual bool Busy { get { return false; } }

        public void Attach(ColosseumEvents owner)
        {
            host = owner;
            root = new GameObject("Map event " + Key).transform;
            root.SetParent(owner.Root, false);
        }

        /// <summary>Reserve space before the decree announces a winner. Prepared geometry stays off the court.</summary>
        public bool Reserve()
        {
            root.gameObject.SetActive(false);
            if (!built)
            {
                built = true;
                Build();
            }
            Clear();
            return Arrive();
        }

        public void Reveal()
        {
            Live = true;
            root.gameObject.SetActive(true);
            OnReveal();
        }

        protected virtual void OnReveal() { }

        /// <summary>Everything that can be made once and kept: meshes, and the objects that are shown and hidden.</summary>
        protected abstract void Build();
        /// <summary>Puts the event on the court, by the sheet's numbers; false if nothing could be placed.</summary>
        protected abstract bool Arrive();
        /// <summary>The event's time is up: it leaves, but in its own time, where it can be seen going.</summary>
        public abstract void Leave();
        /// <summary>Gone at once, and nothing of it left on anyone (a new set, or back to the lobby).</summary>
        public abstract void Clear();
        public virtual void Tick(float delta, float time) { }
        /// <summary>Once every step of the physics.</summary>
        public virtual void Step(float delta) { }
        /// <summary>A round is starting.</summary>
        public virtual void RoundBegins(int round) { }
        /// <summary>A turn is beginning: what an event keeps count of by the turn starts again from nothing.</summary>
        public virtual void TurnBegins() { }
        /// <summary>A turn has been played out and everyone is still; true if the event now does something the next turn has to wait for.</summary>
        public virtual bool TurnEnds() { return false; }
        /// <summary>A hero has run into something that is not a hero; true if it was this event's, and has been dealt with.</summary>
        public virtual bool Struck(ColosseumDoll doll, Vector3 point, Vector3 normal, Vector3 relative) { return false; }
        /// <summary>Something has burst on the court (a slam, a star, a bomb), set off by this side.</summary>
        public virtual void Blast(Vector3 at, float radius, int team) { }
        /// <summary>A hero's shield has just taken this much for it.</summary>
        public virtual void ShieldHeld(ColosseumDoll doll, int held) { }

        protected float Value(string name) { return host.Table.Value(Key, name); }
        protected int Count(string name) { return host.Table.Count(Key, name); }

        /// <summary>Tints a white see-through part.</summary>
        protected void Tint(Renderer part, Color color)
        {
            block.SetColor("_Color", color);
            part.SetPropertyBlock(block);
        }

        /// <summary>From nothing to full size, running a little past it on the way: 0 at the start, 1 at the end.</summary>
        protected static float Pop(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t) + Mathf.Sin(t * Mathf.PI) * .16f;
        }
    }

    /// <summary>
    /// A battle changes terrain on round 3 and introduces a royal decree on rounds 4, 7 and 12.
    /// The balance sheet selects the featured events for this map, stage and attempt, their weights and durations.
    /// </summary>
    public sealed class ColosseumEvents
    {
        sealed class Slot
        {
            public string Key;
            public int Turn, ExpiresAfterMoves;
            public bool Begun, Over;
        }

        struct Claim
        {
            public Vector3 At;
            public float Radius;
            public object Owner;
            /// <summary>Only a covering of the ground (ice): if there is no room elsewhere, other things may stand on it.</summary>
            public bool Soft;
        }

        const string StageKey = "colosseum.events.stage", AttemptKey = "colosseum.events.attempts";
        /// <summary>Kept with an event's key after it, once the player has been shown that event on the royal scroll.</summary>
        internal const string DecreeSeenKey = "colosseum.decree.seen.";

#if UNITY_EDITOR
        internal static void ResetEditorProgress()
        {
            PlayerPrefs.DeleteKey(StageKey);
            PlayerPrefs.DeleteKey(AttemptKey);
            // A new player has been shown none of the events: each is read out at leisure again the first time it comes.
            foreach (string key in ColosseumEventTable.Keys) PlayerPrefs.DeleteKey(DecreeSeenKey + key);
        }
#endif

        /// <summary>
        /// For trying events out from the editor: a plan to use in place of the sheet's ("ice;portal"), every event
        /// of it coming at TestRound; and a stage and an attempt to pretend to (0: the real ones).
        /// </summary>
        public static string TestPlan;
        public static int TestRound = 1, TestStage, TestAttempt;

        readonly ColosseumGame game;
        readonly IList<ColosseumDoll> dolls;
        readonly Dictionary<string, MapFeature> features = new Dictionary<string, MapFeature>();
        readonly List<MapFeature> order = new List<MapFeature>();
        readonly List<Slot> plan = new List<Slot>();
        readonly List<string> eligibleDecrees = new List<string>();
        readonly List<string> reserves = new List<string>();
        static readonly int[] EventTurns = { 1, 5, 10 };
        internal int TerrainTurn { get; private set; }
        /// <summary>
        /// Which battle of the player's this is, counting wins and losses from 1; 0 for a test. The third opens with
        /// the power gate decreed, the fourth with the earthquake, and from the fifth on each opens with whichever
        /// event the dice put first.
        /// </summary>
        internal int Match;
        /// <summary>True when the earthquake is to be read out on the royal scroll before it comes.</summary>
        internal bool TerrainDecreed { get; private set; }
        MapFeature reservedFeature;
        readonly List<(Vector3 At, float Radius, Color Tint, float Due)> reservedOmens = new List<(Vector3, float, Color, float)>();
        readonly List<Claim> claims = new List<Claim>();
        readonly System.Random dice = new System.Random(System.Guid.NewGuid().GetHashCode());
        int stage = 1, attempt = 1, regularMoves;
        string map = string.Empty;
        bool turnEnded, gateInPlay;
        int extraTurn = -1;

        /// <summary>
        /// How long an event is heralded before the first of it stands on the court: the light goes down and the
        /// rim of the view glows in the event's colour, drums roll, the ground grows uneasy, and on the sand a ring
        /// closes in on each place where something is about to appear.
        /// </summary>
        internal const float Herald = .55f;

        /// <summary>A mark on the sand where something is about to appear, and the burst of light as it does.</summary>
        sealed class Omen
        {
            public Transform Root, Shaft;
            public MeshRenderer Ring, Glow, Light, Runes, Core, Pulse, Rise;
            public float Radius, Age, Due;
            public Color Color;
            public bool Active;
        }

        const int MostOmens = 18;
        const float BurstFor = .36f;
        readonly List<Omen> omens = new List<Omen>();
        readonly MaterialPropertyBlock paint = new MaterialPropertyBlock();
        Transform veil;
        MeshRenderer veilShade, veilGlow;
        Color showColor = Color.white;
        float showAge = -1f, showLead, flash, aftershock;
        bool showStruck, showSpotSet;
        Vector3 showSpot = new Vector3(0f, .8f, 0f);

        /// <summary>True while something is being heralded and coming on: the heroes have eyes for nothing else.</summary>
        public bool Showing { get { return showAge >= 0f && showAge < showLead + .35f; } }
        /// <summary>Where it is coming: the place everyone looks.</summary>
        public Vector3 ShowSpot { get { return showSpot; } }

        internal ColosseumGame Game { get { return game; } }
        internal ColosseumArena Arena { get; private set; }
        internal ColosseumFx Fx { get; private set; }
        internal ColosseumHud Hud { get; private set; }
        internal Sfx Sound { get; private set; }
        internal IList<ColosseumDoll> Dolls { get { return dolls; } }
        internal Transform Root { get; private set; }
        internal Material Stroke { get; private set; }
        internal PhysicsMaterial WallSurface { get; private set; }
        internal ColosseumEventTable Table { get; private set; }
        /// <summary>The round of the set that is being played (1 for the first).</summary>
        internal int Round { get; private set; }
        /// <summary>The way a flat sign has to be turned to face the camera square on.</summary>
        internal static readonly Quaternion TowardLens = Quaternion.Euler(ColosseumGame.CameraPitch, 0f, 0f);

        /// <summary>The stage, the attempt at it and the map the events of this match were picked for.</summary>
        public int Stage { get { return stage; } }
        public int AttemptNumber { get { return attempt; } }
        public string Map { get { return map; } }

        public ColosseumEvents(ColosseumGame owner, ColosseumArena arena, ColosseumFx fx, ColosseumHud hud, Sfx sfx, IList<ColosseumDoll> everyone,
            PhysicsMaterial wallSurface)
        {
            game = owner;
            Arena = arena;
            Fx = fx;
            Hud = hud;
            Sound = sfx;
            dolls = everyone;
            WallSurface = wallSurface;
            Table = ColosseumEventTable.Defaults();
            Root = new GameObject("Colosseum map events").transform;
            Root.SetParent(owner.World, false);
            Stroke = Kit.StrokeMaterial(Color.white);
            foreach (MapFeature feature in new MapFeature[] { new MapPortals(), new MapLava(), new MapSpikes(), new MapIce(), new MapBumpers(), new MapCrate(),
                new MapTide(), new MapFirePillars() })
            {
                feature.Attach(this);
                features[feature.Key] = feature;
                order.Add(feature);
            }
        }

        // ------------------------------------------------------------------ the match and the set

        /// <summary>
        /// The number of this battle among those begun at this stage, wins and losses alike; the first is 1. A
        /// battle that is not counted (a test, or one not played for trophies) leaves the count as it was.
        /// </summary>
        public static int Attempt(int stage, bool counted)
        {
            int begun = PlayerPrefs.GetInt(StageKey, 0) == stage ? Mathf.Max(0, PlayerPrefs.GetInt(AttemptKey, 0)) : 0;
            if (!counted) return begun + 1;
            PlayerPrefs.SetInt(StageKey, stage);
            PlayerPrefs.SetInt(AttemptKey, begun + 1);
            PlayerPrefs.Save();
            return begun + 1;
        }

        /// <summary>
        /// A match is about to begin: the sheet is read afresh, and the old events are set by it too. This comes
        /// before the teams are put out, because the earthquake picks where the walls will break as they are.
        /// </summary>
        public void Prepare(int stageNumber, string mapName, bool counted)
        {
            stage = TestStage > 0 ? TestStage : Mathf.Max(1, stageNumber);
            // A pretended stage is judged by its own map, so that a map's rules can be tried from any stage.
            map = TestStage > 0 ? ColosseumJourneyMaps.Name(stage) : mapName ?? string.Empty;
            attempt = TestAttempt > 0 ? TestAttempt : Attempt(stage, counted && TestStage <= 0);
            Table = ColosseumEventTable.Load();
            ColosseumBoost.Tune(Table.Value(ColosseumEventTable.Gate, "damage_scale"), Table.Value(ColosseumEventTable.Gate, "grow_scale"),
                Table.Value(ColosseumEventTable.Gate, "gate_size"));
            ColosseumQuake.Tune(Table.Count(ColosseumEventTable.Quake, "gap_count_max"), Table.Count(ColosseumEventTable.Quake, "gap_budget"));
        }

        /// <summary>Schedule stage-eligible events once at the start of the match.</summary>
        public void BeginMatch()
        {
            plan.Clear();
            eligibleDecrees.Clear();
            reserves.Clear();
            Round = 0;
            regularMoves = 0;
            TerrainTurn = 0;
            TerrainDecreed = false;
            if (Match == 3 && string.IsNullOrEmpty(TestPlan) && TestStage <= 0)
                plan.Add(new Slot { Key = ColosseumEventTable.Gate, Turn = 1 });
            else if (Match == 4 && string.IsNullOrEmpty(TestPlan) && TestStage <= 0)
            {
                TerrainTurn = 1;
                TerrainDecreed = true;
            }
            else if (Application.isEditor && !string.IsNullOrEmpty(TestPlan))
            {
                int testTurn = Mathf.Max(1, TestRound);
                foreach (string part in TestPlan.Split(';', ','))
                {
                    string key = ColosseumEventTable.KeyOf(part);
                    if (key == null) continue;
                    if (key == ColosseumEventTable.Quake) TerrainTurn = testTurn;
                    else plan.Add(new Slot { Key = key, Turn = testTurn });
                }
            }
            else
            {
                var featured = Table.Roll(stage, attempt, map, Application.isEditor, dice, reserves);
                foreach (var pick in featured)
                {
                    if (pick.Key == ColosseumEventTable.Quake) TerrainTurn = pick.Round;
                    else eligibleDecrees.Add(pick.Key);
                }
                // The sheet's picks come first. Fill remaining milestones from its eligible weighted pool.
                eligibleDecrees.AddRange(reserves);
                reserves.Clear();
                // Introduce one of this arena's new mechanics on the first decree of its first match.
                if (attempt == 1)
                {
                    int debut = eligibleDecrees.FindIndex(key => Table.Get(key).UnlockArena == stage);
                    if (debut > 0)
                        (eligibleDecrees[0], eligibleDecrees[debut]) = (eligibleDecrees[debut], eligibleDecrees[0]);
                }
                for (int i = 0; i < eligibleDecrees.Count; i++)
                {
                    // From the fifth battle on, the first of them is decreed as the battle opens.
                    if (i < EventTurns.Length) plan.Add(new Slot { Key = eligibleDecrees[i], Turn = EventTurns[i] });
                    else reserves.Add(eligibleDecrees[i]);
                }
                if (eligibleDecrees.Contains(ColosseumEventTable.Gate))
                {
                    for (int i = plan.Count; i < EventTurns.Length; i++)
                        plan.Add(new Slot { Key = ColosseumEventTable.Gate, Turn = EventTurns[i] });
                }
                // An arena with nothing of its own unlocked yet still opens with a decree: one of the two the player
                // met in the third and fourth battles, the power gate or the earthquake.
                // An arena with nothing of its own unlocked yet takes any event at random, on each of the turns, never the
                // same one twice in a battle and the opening never the same as last battle's. The earthquake comes once.
                if (Match >= 5 && plan.Count == 0 && TerrainTurn == 0)
                {
                    string before = PlayerPrefs.GetString(LastOpeningKey, string.Empty);
                    var pool = new List<string>(ColosseumEventTable.Keys);
                    pool.RemoveAll(key => !Table.Here(key, map));
                    for (int i = 0; i < EventTurns.Length && pool.Count > 0; i++)
                    {
                        var choices = pool.FindAll(key => i > 0 || key != before);
                        if (choices.Count == 0) choices = pool;
                        string key = choices[dice.Next(choices.Count)];
                        pool.Remove(key);
                        if (key == ColosseumEventTable.Quake)
                        {
                            TerrainTurn = EventTurns[i];
                            TerrainDecreed = true;
                        }
                        else plan.Add(new Slot { Key = key, Turn = EventTurns[i] });
                    }
                    // What is left stands by, for a feature that finds nowhere to go.
                    pool.Remove(ColosseumEventTable.Quake);
                    reserves.AddRange(pool);
                    if (!eligibleDecrees.Contains(ColosseumEventTable.Gate)) eligibleDecrees.Add(ColosseumEventTable.Gate);
                }
                if (Match >= 5) AvoidRepeatedOpening();
                EnsureOwn();
            }
            if (Match >= 3 && string.IsNullOrEmpty(TestPlan)) RememberOpening();
            gateInPlay = Planned(ColosseumEventTable.Gate);
#if UNITY_EDITOR
            Debug.Log("[MapEvent] stage " + stage + " (" + map + "), attempt " + attempt + " [" + Table.Source + "] terrain turn=" + TerrainTurn + ", decree turns: " + PlanText());
#endif
        }

        const string LastOpeningKey = "colosseum.events.lastOpening";

        /// <summary>The event this battle opens with: decreed on its first turn.</summary>
        string Opening()
        {
            if (TerrainDecreed && TerrainTurn == EventTurns[0]) return ColosseumEventTable.Quake;
            Slot first = plan.Find(slot => slot.Turn == EventTurns[0]);
            return first != null ? first.Key : null;
        }

        /// <summary>A battle never opens with the same event as the one before it: the power gate, then the earthquake.</summary>
        void AvoidRepeatedOpening()
        {
            string before = PlayerPrefs.GetString(LastOpeningKey, string.Empty), now = Opening();
            if (now == null || now != before) return;
            var choices = new List<string>();
            foreach (string key in eligibleDecrees)
                if (key != before && !choices.Contains(key)) choices.Add(key);
            if (before != ColosseumEventTable.Gate && !choices.Contains(ColosseumEventTable.Gate)) choices.Add(ColosseumEventTable.Gate);
            if (before != ColosseumEventTable.Quake) choices.Add(ColosseumEventTable.Quake);
            string pick = choices[dice.Next(choices.Count)];
            int at = plan.FindIndex(slot => slot.Turn == EventTurns[0]);
            if (pick == ColosseumEventTable.Quake)
            {
                if (at >= 0) plan.RemoveAt(at);
                TerrainTurn = EventTurns[0];
                TerrainDecreed = true;
                return;
            }
            if (TerrainDecreed && TerrainTurn == EventTurns[0]) { TerrainTurn = 0; TerrainDecreed = false; }
            if (at >= 0) plan[at].Key = pick;
            else plan.Add(new Slot { Key = pick, Turn = EventTurns[0] });
        }

        /// <summary>
        /// A map with an event of its own (the tide, the fire) has it in every battle fought there. It opens the
        /// first battle on the map, and any battle on a stage that is being tried out from the editor; otherwise
        /// the dice pick which of the event turns it comes on, never the opening twice running.
        /// </summary>
        void EnsureOwn()
        {
            string own = Table.Own(map, stage);
            if (own == null) return;
            if (!eligibleDecrees.Contains(own)) eligibleDecrees.Insert(0, own);
            PlaceOwn(own, attempt == 1 || TestStage > 0);
            // It comes once in a battle. If it has been given a second turn as well (a changed opening can hand it
            // one), that turn goes to an event that was standing by, or failing that to the power gate.
            Slot kept = null;
            foreach (Slot slot in plan)
            {
                if (slot.Key == own && (kept == null || slot.Turn < kept.Turn)) kept = slot;
            }
            for (int i = plan.Count - 1; i >= 0; i--)
            {
                Slot other = plan[i];
                if (other.Key != own || other == kept) continue;
                string spare = reserves.Find(key => key != own && key != ColosseumEventTable.Quake && !Planned(key));
                if (spare != null)
                {
                    reserves.Remove(spare);
                    other.Key = spare;
                }
                else if (eligibleDecrees.Contains(ColosseumEventTable.Gate)) other.Key = ColosseumEventTable.Gate;
                else plan.RemoveAt(i);
            }
        }

        /// <summary>Puts the map's own event on the turn it is owed, making room for it there if it has to.</summary>
        void PlaceOwn(string own, bool opens)
        {
            Slot mine = plan.Find(slot => slot.Key == own);
            if (mine != null && (!opens || mine.Turn == EventTurns[0])) return;
            var turns = new List<int>();
            if (opens) turns.Add(EventTurns[0]);
            else
            {
                string before = PlayerPrefs.GetString(LastOpeningKey, string.Empty);
                foreach (int turn in EventTurns)
                {
                    if (turn == EventTurns[0] && before == own) continue;
                    // The earthquake keeps the turn it was given.
                    if (turn == TerrainTurn) continue;
                    turns.Add(turn);
                }
                if (turns.Count == 0) return;
            }
            int at = turns[dice.Next(turns.Count)];
            Slot there = plan.Find(slot => slot.Turn == at);
            // Two decrees are never read out at once. (Only the opening can be the earthquake's here: the dice are not offered its turn.)
            bool quake = TerrainDecreed && TerrainTurn == at;
            if (mine != null)
            {
                // Already in the plan, but later than the opening it is owed: it changes places with what was to open.
                if (there != null) there.Turn = mine.Turn;
                else if (quake) TerrainTurn = mine.Turn;
                mine.Turn = at;
                return;
            }
            if (there != null)
            {
                // What was to come then stands by instead, for a feature that finds nowhere to go.
                if (there.Key != ColosseumEventTable.Gate && !reserves.Contains(there.Key)) reserves.Insert(0, there.Key);
                there.Key = own;
                return;
            }
            plan.Add(new Slot { Key = own, Turn = at });
            if (!quake) return;
            // The earthquake was to open: it waits for an event turn that nothing else has, and with none to be had
            // it does not come to this battle.
            TerrainTurn = 0;
            foreach (int turn in EventTurns)
            {
                if (TerrainTurn == 0 && turn != at && plan.Find(slot => slot.Turn == turn) == null) TerrainTurn = turn;
            }
            if (TerrainTurn == 0) TerrainDecreed = false;
        }

        void RememberOpening()
        {
            string now = Opening();
            if (now == null) return;
            PlayerPrefs.SetString(LastOpeningKey, now);
            PlayerPrefs.Save();
        }

        /// <summary>True if the event is in this set's plan, whether or not it has come yet.</summary>
        public bool Planned(string key)
        {
            if (key == ColosseumEventTable.Quake) return TerrainTurn > 0;
            foreach (Slot slot in plan)
            {
                if (slot.Key == key) return true;
            }
            return false;
        }

        /// <summary>The planned events and their one-based turn numbers.</summary>
        public string PlanText()
        {
            var text = new System.Text.StringBuilder();
            foreach (Slot slot in plan)
                text.Append(text.Length > 0 ? " " : string.Empty).Append(slot.Key).Append('@').Append(slot.Turn);
            return text.ToString();
        }

        /// <summary>Every event gone, and nothing of them left on anyone (a new set, or back to the lobby).</summary>
        public void Clear()
        {
            CancelDecree();
            foreach (MapFeature feature in order)
            {
                if (feature.Live) feature.Clear();
            }
            foreach (ColosseumDoll doll in dolls)
            {
                if (doll == null) continue;
                doll.Grip = 1f;
                doll.Shield = 0;
            }
            plan.Clear();
            eligibleDecrees.Clear();
            TerrainTurn = 0;
            reserves.Clear();
            claims.Clear();
            SlingScale = 1f;
            turnEnded = false;
            extraTurn = -1;
            EndShow();
        }

        /// <summary>
        /// Advances the displayed round after both sides have played a regular move.
        /// Returns true on the first move of a new round, when its events are announced.
        /// </summary>
        public bool StartingTurn(int completedRegularMoves, List<string> coming)
        {
            regularMoves = completedRegularMoves;
            foreach (Slot slot in plan)
            {
                if (!slot.Begun || slot.Over || slot.ExpiresAfterMoves <= 0 || regularMoves < slot.ExpiresAfterMoves) continue;
                slot.Over = true;
                if (slot.Key == ColosseumEventTable.Gate) game.ClosePowerGate();
                else if (features.TryGetValue(slot.Key, out MapFeature leaving) && leaving.Live) leaving.Leave();
            }
            int round = regularMoves / 2 + 1;
            if (round == Round) return false;
            Round = round;
            foreach (MapFeature feature in order)
            {
                if (feature.Live) feature.RoundBegins(round);
            }
            foreach (Slot slot in plan)
            {
                if (slot.Begun || slot.Turn != round) continue;
                slot.Begun = true;
                slot.ExpiresAfterMoves = Expiry(slot.Key);
                coming.Add(slot.Key);
            }
            return true;
        }

        int Expiry(string key)
        {
            int rounds = Table.Get(key).Duration;
            return rounds > 0 ? regularMoves + rounds * 2 : 0;
        }

        /// <summary>Resolve placement and any fallback before the scroll reveals the event. Never reroll on reveal.</summary>
        public string PrepareDecree(string key, List<string> candidates, out string description)
        {
            candidates.Clear();
            description = string.Empty;
            Slot slot = plan.FindLast(entry => entry.Begun && !entry.Over && entry.Key == key);
            if (slot == null) return null;
            while (key != ColosseumEventTable.Gate)
            {
                if (!features.TryGetValue(key, out MapFeature feature)) return null;
                if (feature.Reserve())
                {
                    reservedFeature = feature;
                    description = feature.Description;
                    break;
                }
#if UNITY_EDITOR
                Debug.LogWarning("[MapEvent] " + key + ": 놓을 자리가 없어 이번에는 등장하지 못했습니다.");
#endif
                reservedOmens.Clear();
                if (reserves.Count > 0)
                {
                    key = reserves[0];
                    reserves.RemoveAt(0);
                }
                else if (eligibleDecrees.Contains(ColosseumEventTable.Gate)) key = ColosseumEventTable.Gate;
                else return null;
            }
            if (key == ColosseumEventTable.Gate)
            {
                description = "Cross the gate to boost attack for this turn.";
                // Opening a new gate replaces the old one. Only the renewed slot can expire it.
                foreach (Slot prior in plan)
                    if (prior != slot && prior.Begun && prior.Key == key) prior.Over = true;
            }
            slot.Key = key;
            slot.ExpiresAfterMoves = Expiry(key);
            candidates.Add(key);
            foreach (string eligible in eligibleDecrees)
                if (eligible != key) candidates.Add(eligible);
            if (eligibleDecrees.Count == 0)
                foreach (Slot preview in plan)
                    if (!candidates.Contains(preview.Key)) candidates.Add(preview.Key);
            return key;
        }

        public void CancelDecree()
        {
            if (reservedFeature != null) reservedFeature.Clear();
            reservedFeature = null;
            reservedOmens.Clear();
        }

        public void RevealDecree()
        {
            MapFeature feature = reservedFeature;
            if (feature == null) return;
            reservedFeature = null;
            feature.Reveal();
            showSpotSet = false;
            foreach (var omen in reservedOmens) ShowOmen(omen.At, omen.Radius, omen.Tint, omen.Due);
            reservedOmens.Clear();
            Sound.Play("event");
            Fanfare(feature.Theme, feature.Lead, true);
            if (feature.Mood > 0) Arena.ShowMood(feature.Mood, 1.7f);
            game.EventThrill(.4f);
        }

        // ------------------------------------------------------------------ the show an event makes of arriving

        /// <summary>
        /// The show for something arriving on the court, the first of it so long from now. Until then the view is
        /// shaded and its rim glows in this colour, and the ground grows more and more uneasy; as it lands (see
        /// Strike) the shade lifts at a stroke and the glow dies away.
        /// </summary>
        public void Fanfare(Color color, float lead, bool drums)
        {
            if (drums) Sound.Play("event-drum", .9f);
            game.EventPunch(.016f);
            // With no place marked for it, it is the middle of the court that everyone looks to.
            if (!showSpotSet) showSpot = new Vector3(0f, .8f, 0f);
            showSpotSet = false;
            if (veil == null && !BuildVeil()) return;
            showColor = color;
            showAge = 0f;
            showLead = Mathf.Max(.05f, lead);
            showStruck = false;
            flash = 0f;
            veil.gameObject.SetActive(true);
            PaintVeil(0f, 0f);
        }

        internal void Foretell(Vector3 at, float radius, Color color, float due)
        {
            reservedOmens.Add((at, radius, color, due));
        }

        /// <summary>The same mark put down at once, for an event that is already in play and is about to do something new.</summary>
        internal void Mark(Vector3 at, float radius, Color color, float due) { ShowOmen(at, radius, color, due); }

        /// <summary>Start the ground marker's countdown once the decree scroll closes.</summary>
        void ShowOmen(Vector3 at, float radius, Color color, float due)
        {
            if (omens.Count == 0) BuildOmens();
            Omen pick = null;
            foreach (Omen omen in omens)
            {
                if (!omen.Active) { pick = omen; break; }
                if (pick == null || omen.Age - omen.Due > pick.Age - pick.Due) pick = omen;
            }
            pick.Active = true;
            pick.Age = 0f;
            pick.Due = Mathf.Max(.05f, due);
            pick.Radius = Mathf.Max(.2f, radius);
            pick.Color = color;
            pick.Root.position = new Vector3(at.x, 0f, at.z);
            pick.Root.gameObject.SetActive(true);
            TickOmen(pick, 0f);
            // The first place marked is the one the heroes turn to.
            if (!showSpotSet)
            {
                showSpotSet = true;
                showSpot = new Vector3(at.x, .8f, at.z);
            }
        }

        /// <summary>
        /// Something of an event has just appeared, or landed, here: the ground gives under it (a ring of dust
        /// running out, sparks), the view is jolted and drawn in a touch, and the rim of it flares.
        /// </summary>
        internal void Strike(Vector3 at, float radius, float power, string sound = "thud", float slow = 0f)
        {
            at.y = 0f;
            Fx.Shockwave(at, Mathf.Max(.8f, radius * 2.3f));
            Fx.Puff(at, Mathf.Clamp(Mathf.RoundToInt(5f + radius * 6f), 6, 16), 2.4f + radius);
            Fx.Sparks(at + Vector3.up * .35f, Mathf.Clamp(Mathf.RoundToInt(4f + power * 16f), 4, 12));
            game.EventShake(power, slow);
            game.EventPunch(power * .2f);
            aftershock = Mathf.Max(aftershock, power * .6f);
            flash = Mathf.Max(flash, Mathf.Clamp01(power * 2.4f));
            if (!string.IsNullOrEmpty(sound)) Sound.Play(sound, Mathf.Clamp01(.5f + power));
            if (showStruck) return;
            // The first of it: the emperor is on his feet, and the show's clock is brought to this moment in
            // case it came a little early or late.
            showStruck = true;
            Arena.Cheer(1.1f, .6f);
            Sound.Play("cheer", .3f);
            game.EventThrill(.3f);
            if (showAge >= 0f && showAge < showLead) showAge = showLead;
        }

        void TickShow(float delta)
        {
            flash = Mathf.MoveTowards(flash, 0f, delta * 4.5f);
            // After the jolt itself, which is over in a blink, the ground goes on trembling for a moment as it dies away.
            if (aftershock > .012f)
            {
                game.EventShake(aftershock, 0f);
                aftershock *= Mathf.Exp(-delta * 3.6f);
            }
            else aftershock = 0f;
            if (showAge >= 0f)
            {
                showAge += delta;
                float shade, glow;
                if (showAge < showLead)
                {
                    float near = showAge / showLead;
                    game.EventShake(.03f + .1f * near * near, 0f);
                    shade = Mathf.SmoothStep(0f, 1f, showAge / .2f) * .42f;
                    glow = (.3f + .45f * near) * (.72f + .28f * Mathf.Sin(showAge * (16f + 26f * near)));
                }
                else
                {
                    float since = showAge - showLead;
                    shade = .42f * (1f - Mathf.Clamp01(since / .14f));
                    glow = .75f * (1f - Mathf.SmoothStep(0f, 1f, since / .55f));
                    if (since > .6f && flash <= 0f)
                    {
                        showAge = -1f;
                        veil.gameObject.SetActive(false);
                    }
                }
                if (showAge >= 0f) PaintVeil(shade, glow);
            }
            foreach (Omen omen in omens)
            {
                if (omen.Active) TickOmen(omen, delta);
            }
        }

        void EndShow()
        {
            showAge = -1f;
            flash = 0f;
            aftershock = 0f;
            if (veil != null) veil.gameObject.SetActive(false);
            foreach (Omen omen in omens)
            {
                omen.Active = false;
                omen.Root.gameObject.SetActive(false);
            }
        }

        void PaintVeil(float shade, float glow)
        {
            Camera lens = game.Lens;
            if (lens == null || veil == null) return;
            // Kept the size of the view, just in front of the camera.
            float half = lens.orthographicSize;
            veil.localPosition = new Vector3(0f, 0f, lens.nearClipPlane + .6f);
            veil.localScale = new Vector3(half * lens.aspect * 1.04f, half * 1.04f, 1f);
            Paint(veilShade, new Color(1f, 1f, 1f, Mathf.Clamp01(shade)));
            Color rim = Color.Lerp(showColor, Color.white, flash * .7f);
            rim.a = Mathf.Clamp01(glow + flash * .55f);
            Paint(veilGlow, rim);
        }

        void TickOmen(Omen omen, float delta)
        {
            omen.Age += delta;
            float radius = omen.Radius;
            Color color = omen.Color;
            // What is lit white-hot in it keeps a little of the event's colour.
            Color bright = Color.Lerp(color, Color.white, .78f);
            if (omen.Age < omen.Due)
            {
                // The seal settles onto its place, its two wheels turning against each other, while thin rings
                // close in on it from outside: brighter and quicker in its beat as the moment nears.
                float near = omen.Age / omen.Due, eased = 1f - (1f - near) * (1f - near);
                float beat = .8f + .2f * Mathf.Sin(omen.Age * (18f + 30f * near));
                float show = Mathf.Clamp01(omen.Age / .1f);
                omen.Ring.transform.localScale = Vector3.one * (radius * (1.5f - .5f * eased));
                omen.Ring.transform.localRotation = Quaternion.Euler(0f, omen.Age * 95f, 0f);
                color.a = show * Mathf.Clamp01(.45f + .55f * near);
                Paint(omen.Ring, color);
                bright.a = show * (.55f + .45f * near) * beat;
                Paint(omen.Core, bright);
                float open = 1f - Mathf.Pow(1f - Mathf.Clamp01(omen.Age / Mathf.Min(.3f, omen.Due)), 3f);
                omen.Runes.transform.localScale = Vector3.one * (radius * (.55f + .45f * open));
                omen.Runes.transform.localRotation = Quaternion.Euler(0f, -omen.Age * (120f + 160f * near), 0f);
                color.a = show * (.35f + .5f * near);
                Paint(omen.Runes, color);
                float lap = Mathf.Clamp(omen.Due * .5f, .22f, .5f), turn = Mathf.Repeat(omen.Age / lap, 1f);
                omen.Pulse.transform.localScale = Vector3.one * (radius * (2.5f - 1.5f * turn * turn));
                bright.a = Mathf.Sin(turn * Mathf.PI) * (.35f + .5f * near);
                Paint(omen.Pulse, bright);
                omen.Glow.transform.localScale = Vector3.one * (radius * 1.2f);
                color.a = (.16f + .42f * near) * beat;
                Paint(omen.Glow, color);
                float wide = radius * Mathf.Lerp(1.5f, .72f, eased);
                omen.Shaft.localScale = new Vector3(wide, Mathf.Lerp(3.6f, 7.5f, eased), wide);
                omen.Shaft.localRotation = Quaternion.Euler(0f, omen.Age * 210f, 0f);
                color.a = (.14f + .5f * near) * beat;
                Paint(omen.Light, color);
                float climb = Mathf.Repeat(omen.Age * 1.7f, 1f);
                omen.Rise.transform.localScale = new Vector3(radius, 1f + .4f * near, radius);
                omen.Rise.transform.localPosition = Vector3.up * (.1f + climb * 1.5f);
                omen.Rise.transform.localRotation = Quaternion.Euler(0f, omen.Age * -70f, 0f);
                bright.a = Mathf.Sin(climb * Mathf.PI) * (.3f + .6f * near);
                Paint(omen.Rise, bright);
                return;
            }
            // Its time has come: white-hot for an instant, and thrown outwards.
            float gone = (omen.Age - omen.Due) / BurstFor;
            if (gone >= 1f)
            {
                omen.Active = false;
                omen.Root.gameObject.SetActive(false);
                return;
            }
            float spread = 1f - (1f - gone) * (1f - gone);
            Color hot = Color.Lerp(Color.white, color, gone);
            omen.Ring.transform.localScale = Vector3.one * (radius * (1f + 1.1f * spread));
            hot.a = 1f - gone;
            Paint(omen.Ring, hot);
            Paint(omen.Core, new Color(1f, 1f, 1f, (1f - gone) * (1f - gone)));
            omen.Runes.transform.localScale = Vector3.one * (radius * (1f + .5f * spread));
            omen.Runes.transform.Rotate(0f, -delta * 520f, 0f, Space.Self);
            hot.a = (1f - gone) * .8f;
            Paint(omen.Runes, hot);
            // The last thin ring goes out ahead of the rest, as a wave across the sand.
            omen.Pulse.transform.localScale = Vector3.one * (radius * (1f + 2.6f * spread));
            Paint(omen.Pulse, new Color(1f, 1f, 1f, (1f - gone) * .9f));
            omen.Glow.transform.localScale = Vector3.one * (radius * (1.2f + .9f * spread));
            hot.a = (1f - gone) * .9f;
            Paint(omen.Glow, hot);
            float thin = Mathf.Max(.01f, radius * .72f * (1f - spread));
            omen.Shaft.localScale = new Vector3(thin, 7.5f + 3f * spread, thin);
            hot.a = (1f - gone) * .8f;
            Paint(omen.Light, hot);
            omen.Rise.transform.localScale = new Vector3(radius * (1f + .8f * spread), 1.4f + 2f * spread, radius * (1f + .8f * spread));
            omen.Rise.transform.localPosition = Vector3.up * (.4f + 2.4f * spread);
            Paint(omen.Rise, new Color(1f, 1f, 1f, 1f - gone));
        }

        void Paint(Renderer part, Color color)
        {
            paint.SetColor("_Color", color);
            part.SetPropertyBlock(paint);
        }

        /// <summary>A length of ring on the ground from one bearing to another (degrees), drawn out to a point at each end.</summary>
        static void Arc(MeshKit kit, float inner, float outer, float from, float to, Color32 inside, Color32 outside, int seg, float taper = .22f)
        {
            float middle = (inner + outer) * .5f, half = (outer - inner) * .5f;
            for (int i = 0; i < seg; i++)
            {
                float t0 = (float)i / seg, t1 = (float)(i + 1) / seg;
                float w0 = taper <= 0f ? 1f : Mathf.Clamp01(Mathf.Min(t0, 1f - t0) / taper), w1 = taper <= 0f ? 1f : Mathf.Clamp01(Mathf.Min(t1, 1f - t1) / taper);
                float a0 = Mathf.Lerp(from, to, t0) * Mathf.Deg2Rad, a1 = Mathf.Lerp(from, to, t1) * Mathf.Deg2Rad;
                var d0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0));
                var d1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));
                MapMesh.Sheet(kit, d0 * (middle - half * w0), d1 * (middle - half * w1), d1 * (middle + half * w1), d0 * (middle + half * w0), inside, inside, outside, outside);
            }
        }

        /// <summary>An arrowhead on the ground at a bearing (degrees), pointing in at the middle: its tip so far out, so long, so wide, so thick.</summary>
        static void Barb(MeshKit kit, float bearing, float tip, float length, float wide, float thick, Color32 point, Color32 tail)
        {
            float turn = bearing * Mathf.Deg2Rad;
            var way = new Vector3(Mathf.Cos(turn), 0f, Mathf.Sin(turn));
            var side = new Vector3(-way.z, 0f, way.x);
            Vector3 head = way * tip, notch = way * (tip + thick);
            for (int k = -1; k <= 1; k += 2)
            {
                Vector3 arm = way * (tip + length) + side * (wide * k);
                MapMesh.Sheet(kit, head, arm, arm + way * thick, notch, point, tail, tail, point);
            }
        }

        /// <summary>A small four-sided jewel on the ground.</summary>
        static void Gem(MeshKit kit, Vector3 at, Vector3 way, float along, float across, Color32 color)
        {
            var side = new Vector3(-way.z, 0f, way.x);
            MapMesh.Sheet(kit, at - way * along, at + side * across, at + way * along, at - side * across, color);
        }

        void BuildOmens()
        {
            var white = new Color32(255, 255, 255, 255);
            var none = new Color32(255, 255, 255, 0);
            // The mark is a seal of light. Its outer wheel: four lengths of ring with a soft glow either side of
            // them and, in the gaps between, a pair of arrowheads pointing in at what is coming...
            var kit = new MeshKit { Gradient = 0f };
            // (laid on a shadow, so that it shows on sand of any colour)
            var dark = new Color32(30, 20, 14, 78);
            var darkNone = new Color32(30, 20, 14, 0);
            MapMesh.Band(kit, Vector3.zero, .78f, .87f, darkNone, dark, 56);
            MapMesh.Band(kit, Vector3.zero, .86f, 1f, dark, dark, 56);
            MapMesh.Band(kit, Vector3.zero, 1f, 1.1f, dark, darkNone, 56);
            var soft = new Color32(255, 255, 255, 120);
            var body = new Color32(255, 255, 255, 235);
            for (int i = 0; i < 4; i++)
            {
                float mid = i * 90f + 45f;
                Arc(kit, .87f, 1f, mid - 35f, mid + 35f, body, white, 18, .1f);
                Arc(kit, 1f, 1.2f, mid - 36f, mid + 36f, soft, none, 18, .3f);
                Arc(kit, .72f, .88f, mid - 36f, mid + 36f, none, soft, 18, .3f);
                Arc(kit, 1.07f, 1.1f, mid - 20f, mid + 20f, body, body, 10, .4f);
                Barb(kit, i * 90f, .7f, .2f, .15f, .085f, white, body);
                Barb(kit, i * 90f, .9f, .16f, .12f, .07f, body, soft);
            }
            Mesh ring = kit.ToMesh("Colosseum omen ring");
            // ...whose edges and points are picked out white-hot...
            kit = new MeshKit { Gradient = 0f };
            for (int i = 0; i < 4; i++)
            {
                float mid = i * 90f + 45f;
                Arc(kit, .925f, .955f, mid - 31f, mid + 31f, white, white, 16, .14f);
                var way = new Vector3(Mathf.Cos((mid - 39f) * Mathf.Deg2Rad), 0f, Mathf.Sin((mid - 39f) * Mathf.Deg2Rad));
                Gem(kit, way * .94f, way, .05f, .035f, white);
                way = new Vector3(Mathf.Cos((mid + 39f) * Mathf.Deg2Rad), 0f, Mathf.Sin((mid + 39f) * Mathf.Deg2Rad));
                Gem(kit, way * .94f, way, .05f, .035f, white);
            }
            Mesh core = kit.ToMesh("Colosseum omen core");
            // ...an inner wheel that turns the other way: a fine line, a ring of ticks and eight jewels...
            kit = new MeshKit { Gradient = 0f };
            var fine = new Color32(255, 255, 255, 200);
            MapMesh.Band(kit, Vector3.zero, .6f, .625f, fine, fine, 48);
            MapMesh.Band(kit, Vector3.zero, .34f, .355f, soft, soft, 36);
            for (int i = 0; i < 32; i++)
            {
                float turn = i * (360f / 32f) * Mathf.Deg2Rad;
                var way = new Vector3(Mathf.Cos(turn), 0f, Mathf.Sin(turn));
                bool major = i % 4 == 0;
                MapMesh.Stroke(kit, way * (major ? .47f : .53f), way * .6f, major ? .034f : .02f, major ? white : fine);
                if (i % 4 == 2) Gem(kit, way * .43f, way, .06f, .034f, white);
            }
            for (int i = 0; i < 3; i++) Arc(kit, .385f, .41f, i * 120f, i * 120f + 78f, fine, fine, 12, .35f);
            Mesh runes = kit.ToMesh("Colosseum omen runes");
            // ...thin rings that close in on it from outside, one after another...
            kit = new MeshKit { Gradient = 0f };
            MapMesh.Band(kit, Vector3.zero, .95f, .985f, none, white, 56);
            MapMesh.Band(kit, Vector3.zero, .985f, 1f, white, white, 56);
            MapMesh.Band(kit, Vector3.zero, 1f, 1.09f, soft, none, 56);
            Mesh pulse = kit.ToMesh("Colosseum omen pulse");
            // ...the sand lit up inside it, brightest round the edge...
            kit = new MeshKit { Gradient = 0f };
            kit.Disc(Vector3.zero, .55f, .55f, new Color32(255, 255, 255, 60), new Color32(255, 255, 255, 105), 40);
            MapMesh.Band(kit, Vector3.zero, .55f, .86f, new Color32(255, 255, 255, 105), new Color32(255, 255, 255, 190), 40);
            MapMesh.Band(kit, Vector3.zero, .86f, 1.3f, new Color32(255, 255, 255, 150), none, 40);
            Mesh glow = kit.ToMesh("Colosseum omen glow");
            // ...a shaft of light standing on it, in streaks, fading out upwards...
            kit = new MeshKit { Gradient = 0f };
            const int Streaks = 24;
            for (int i = 0; i < Streaks; i++)
            {
                float t0 = (float)i / Streaks * Mathf.PI * 2f, t1 = (float)(i + 1) / Streaks * Mathf.PI * 2f;
                Vector3 d0 = new Vector3(Mathf.Cos(t0), 0f, Mathf.Sin(t0)) * .5f, d1 = new Vector3(Mathf.Cos(t1), 0f, Mathf.Sin(t1)) * .5f;
                // Every streak is its own height and strength; the same few numbers each time, so it is one look.
                float tall = .45f + .55f * Mathf.Abs(Mathf.Sin(i * 2.4f + .7f));
                byte foot = (byte)(i % 3 == 0 ? 205 : i % 3 == 1 ? 95 : 140);
                var low = new Color32(255, 255, 255, foot);
                var waist = new Color32(255, 255, 255, (byte)(foot * .45f));
                Vector3 mid = Vector3.up * (tall * .4f), top = Vector3.up * tall;
                MapMesh.Sheet(kit, d0, d1, d1 + mid, d0 + mid, low, low, waist, waist);
                MapMesh.Sheet(kit, d0 + mid, d1 + mid, d1 + top, d0 + top, waist, waist, none, none);
            }
            MapMesh.Wall(kit, Vector3.zero, .47f, .22f, new Color32(255, 255, 255, 170), none, 24);
            Mesh shaft = kit.ToMesh("Colosseum omen shaft");
            // ...and flecks of light climbing off it.
            kit = new MeshKit { Gradient = 0f };
            for (int i = 0; i < 14; i++)
            {
                float turn = i * 2.39996f, far = .35f + .6f * Mathf.Abs(Mathf.Sin(i * 1.7f + .3f));
                float high = 1.1f * Mathf.Abs(Mathf.Sin(i * 3.1f + 1.1f)), tall = .16f + .2f * Mathf.Abs(Mathf.Sin(i * 5.3f)), wide = .016f + .012f * (i % 3);
                var foot = new Vector3(Mathf.Cos(turn) * far, high, Mathf.Sin(turn) * far);
                // Two blades at right angles, so that a fleck shows from any side.
                for (int k = 0; k < 2; k++)
                {
                    Vector3 side = (k == 0 ? new Vector3(-Mathf.Sin(turn), 0f, Mathf.Cos(turn)) : new Vector3(Mathf.Cos(turn), 0f, Mathf.Sin(turn))) * wide;
                    Vector3 belly = foot + Vector3.up * (tall * .35f), tip = foot + Vector3.up * tall;
                    kit.Quad(foot, belly - side, tip, belly + side, none, white, none, white);
                }
            }
            Mesh rise = kit.ToMesh("Colosseum omen flecks");

            Transform marks = new GameObject("Map event omens").transform;
            marks.SetParent(Root, false);
            for (int i = 0; i < MostOmens; i++)
            {
                var omen = new Omen { Root = new GameObject("Omen").transform };
                omen.Root.SetParent(marks, false);
                omen.Glow = Kit.SpawnStroke("Omen glow", omen.Root, glow, Stroke).GetComponent<MeshRenderer>();
                omen.Pulse = Kit.SpawnStroke("Omen pulse", omen.Root, pulse, Stroke).GetComponent<MeshRenderer>();
                omen.Runes = Kit.SpawnStroke("Omen runes", omen.Root, runes, Stroke).GetComponent<MeshRenderer>();
                omen.Ring = Kit.SpawnStroke("Omen ring", omen.Root, ring, Stroke).GetComponent<MeshRenderer>();
                // The white-hot edges ride on the outer wheel.
                omen.Core = Kit.SpawnStroke("Omen core", omen.Ring.transform, core, Stroke).GetComponent<MeshRenderer>();
                GameObject light = Kit.SpawnStroke("Omen shaft", omen.Root, shaft, Stroke);
                omen.Shaft = light.transform;
                omen.Light = light.GetComponent<MeshRenderer>();
                omen.Rise = Kit.SpawnStroke("Omen flecks", omen.Root, rise, Stroke).GetComponent<MeshRenderer>();
                omen.Glow.transform.localPosition = Vector3.up * .034f;
                omen.Pulse.transform.localPosition = Vector3.up * .036f;
                omen.Runes.transform.localPosition = Vector3.up * .038f;
                omen.Ring.transform.localPosition = Vector3.up * .04f;
                omen.Core.transform.localPosition = Vector3.up * .003f;
                // Drawn after the shade that lies across the whole view, so that they stand out of it.
                omen.Glow.sortingOrder = 13;
                omen.Pulse.sortingOrder = 13;
                omen.Runes.sortingOrder = 14;
                omen.Ring.sortingOrder = 14;
                omen.Core.sortingOrder = 15;
                omen.Light.sortingOrder = 15;
                omen.Rise.sortingOrder = 16;
                omen.Root.gameObject.SetActive(false);
                omens.Add(omen);
            }
        }

        bool BuildVeil()
        {
            Camera lens = game.Lens;
            if (lens == null) return false;
            veil = new GameObject("Map event veil").transform;
            veil.SetParent(lens.transform, false);
            // Shade over everything, deeper towards the edges...
            var kit = new MeshKit { Gradient = 0f };
            Color32 thin = new Color32(8, 6, 16, 150), deep = new Color32(8, 6, 16, 255);
            Frame(kit, .5f, thin, deep);
            kit.Quad(new Vector3(-.5f, -.5f, 0f), new Vector3(-.5f, .5f, 0f), new Vector3(.5f, .5f, 0f), new Vector3(.5f, -.5f, 0f), thin);
            veilShade = Kit.SpawnStroke("Veil shade", veil, kit.ToMesh("Colosseum event shade"), Stroke).GetComponent<MeshRenderer>();
            // ...and the event's own colour glowing in round the rim of the view.
            kit = new MeshKit { Gradient = 0f };
            Frame(kit, .64f, new Color32(255, 255, 255, 0), new Color32(255, 255, 255, 255));
            veilGlow = Kit.SpawnStroke("Veil glow", veil, kit.ToMesh("Colosseum event glow"), Stroke).GetComponent<MeshRenderer>();
            // Over everything on the court (which keeps to single figures), under the marks on the sand.
            veilShade.sortingOrder = 10;
            veilGlow.sortingOrder = 11;
            veil.gameObject.SetActive(false);
            return true;
        }

        /// <summary>A frame in the plane of the view: from a rectangle so far out from the middle (1 is the edge) to the edge itself.</summary>
        static void Frame(MeshKit kit, float inner, Color32 inside, Color32 outside)
        {
            Vector3[] rim = { new Vector3(-1f, -1f, 0f), new Vector3(-1f, 1f, 0f), new Vector3(1f, 1f, 0f), new Vector3(1f, -1f, 0f) };
            for (int i = 0; i < 4; i++)
            {
                Vector3 a = rim[i], b = rim[(i + 1) % 4];
                kit.Quad(a * inner, b * inner, b, a, inside, inside, outside, outside);
            }
        }

        /// <summary>True while an event is in the middle of something the match has to wait for.</summary>
        public bool Busy
        {
            get
            {
                foreach (MapFeature feature in order)
                {
                    if (feature.Live && feature.Busy) return true;
                }
                return false;
            }
        }

        /// <summary>
        /// True from the moment a turn's end sets the events off until the next turn begins: whatever is thrown
        /// about in that time is nobody's doing, and heroes knocking into one another are not hurt by it.
        /// </summary>
        public bool Neutral { get; private set; }

        public void BeginTurn(int team)
        {
            turnEnded = false;
            Neutral = false;
            foreach (MapFeature feature in order)
            {
                if (feature.Live) feature.TurnBegins();
            }
        }

        /// <summary>
        /// A turn has been played out. Each event has its say, once a turn; true if any of them has started
        /// something, in which case the game lets it play out and comes back.
        /// </summary>
        public bool EndTurn(int team)
        {
            if (turnEnded) return false;
            turnEnded = true;
            bool waiting = false;
            foreach (MapFeature feature in order)
            {
                if (feature.Live && feature.TurnEnds()) waiting = true;
            }
            Neutral = waiting;
            return waiting;
        }

        /// <summary>The side that broke the supply crate may have won another turn: true once, for that side.</summary>
        public bool TakeExtraTurn(int team)
        {
            if (extraTurn != team) return false;
            extraTurn = -1;
            return true;
        }

        internal void GrantExtraTurn(int team) { extraTurn = team; }

        public void Tick(float delta)
        {
            float time = Time.time;
            foreach (MapFeature feature in order)
            {
                if (feature.Live) feature.Tick(delta, time);
            }
            TickShow(delta);
        }

        public void Step(float delta)
        {
            foreach (MapFeature feature in order)
            {
                if (feature.Live) feature.Step(delta);
            }
        }

        /// <summary>A hero has run into something that is not a hero; true if it was an event's, and the event has dealt with it.</summary>
        public bool Struck(ColosseumDoll doll, Vector3 point, Vector3 normal, Vector3 relative)
        {
            foreach (MapFeature feature in order)
            {
                if (feature.Live && feature.Struck(doll, point, normal, relative)) return true;
            }
            return false;
        }

        /// <summary>Something has burst on the court, set off by this side.</summary>
        public void Blast(Vector3 at, float radius, int team)
        {
            foreach (MapFeature feature in order)
            {
                if (feature.Live) feature.Blast(at, radius, team);
            }
        }

        public void ShieldHeld(ColosseumDoll doll, int held)
        {
            foreach (MapFeature feature in order) feature.ShieldHeld(doll, held);
        }

        /// <summary>For the computer's side: true if there is a supply crate standing, and where.</summary>
        public bool Prize(out Vector3 at)
        {
            var crate = (MapCrate)features[ColosseumEventTable.Crate];
            at = crate.At;
            return crate.Live && crate.Standing;
        }

        // ------------------------------------------------------------------ what the events ask

        internal int TurnSide { get { return game.TurnSide; } }

        /// <summary>How much of its usual force a sling has just now: 1, or less while something holds the heroes back (the tide).</summary>
        internal float SlingScale { get; set; } = 1f;

        /// <summary>
        /// What a pull of this strength is worth just now. Held back, the same pull sends a hero off so much
        /// slower: this is the strength that would have done that on dry ground.
        /// </summary>
        public float Sling(float power)
        {
            if (SlingScale >= 1f) return power;
            float slowest = ColosseumGame.SlingSpeed(0f), fastest = ColosseumGame.SlingSpeed(1f);
            return Mathf.Clamp01((ColosseumGame.SlingSpeed(power) * SlingScale - slowest) / (fastest - slowest));
        }
        internal float Chance() { return (float)dice.NextDouble(); }
        internal float Between(float from, float to) { return from + (to - from) * (float)dice.NextDouble(); }
        internal int Pick(int count) { return dice.Next(count); }

        /// <summary>
        /// Somewhere to put something of this size. The spot is in the south team's half, between zNear and zFar
        /// from the half-way line and no further than xReach from the middle; a mirrored thing has a twin at the
        /// same spot turned about the middle of the court, so that neither team has the better of it, and both
        /// have to be free. Free means clear of the walls, of everything else the events have put down, of the
        /// lane the power gate runs in (for a solid thing) and, as long as that can be managed, of the heroes.
        /// A covering of the ground (floor: whose it is) is kept clear of everything else only while there is room:
        /// in the end it may lie under other things, and other things may be stood on it.
        /// </summary>
        internal bool Place(float radius, float xReach, float zNear, float zFar, bool solid, bool mirrored, out Vector3 spot, bool clearOfHeroes = true,
            object floor = null)
        {
            for (int tries = 0; tries < 90; tries++)
            {
                bool mindHeroes = clearOfHeroes && tries < 60, apart = tries < 50;
                var at = new Vector3(Between(-xReach, xReach), 0f, -Between(zNear, zFar));
                if (!mirrored && dice.Next(2) == 0) at = -at;
                if (!Free(at, radius, solid, mindHeroes, apart, floor)) continue;
                if (mirrored && (!Free(-at, radius, solid, mindHeroes, apart, floor) || at.magnitude < radius + .3f)) continue;
                spot = at;
                return true;
            }
            spot = Vector3.zero;
            return false;
        }

        internal bool Free(Vector3 at, float radius, bool solid, bool mindHeroes, bool apart = true, object floor = null)
        {
            if (Mathf.Abs(at.x) > ColosseumGame.HalfWidth - radius - .25f || Mathf.Abs(at.z) > ColosseumGame.HalfDepth - radius - .25f) return false;
            if (solid && gateInPlay && Mathf.Abs(at.z) < radius + 1.35f) return false;
            foreach (Claim claim in claims)
            {
                // Ground coverings and the things that stand on the ground are only kept apart while that can be done;
                // two sheets of the same covering never overlap.
                bool same = floor != null && claim.Owner == floor;
                if (!apart && !same && (claim.Soft || floor != null)) continue;
                if (Kit.Flat(claim.At - at).magnitude < claim.Radius + radius + .35f) return false;
            }
            if (!mindHeroes) return true;
            foreach (ColosseumDoll doll in dolls)
            {
                if (doll == null || doll.Lost) continue;
                if (Kit.Flat(doll.transform.position - at).magnitude < radius + ColosseumDoll.Radius * doll.Bulk + (solid ? .6f : .25f)) return false;
            }
            return true;
        }

        /// <summary>This ground is taken: nothing else is put down on it until it is given back.</summary>
        internal void Take(Vector3 at, float radius, object owner, bool soft = false)
        {
            claims.Add(new Claim { At = Kit.Flat(at), Radius = radius, Owner = owner, Soft = soft });
        }

        internal void GiveBack(object owner)
        {
            claims.RemoveAll(claim => claim.Owner == owner);
        }

        /// <summary>
        /// As fast as the hardest sling there is. Nothing an event does sends a hero off faster than this, however
        /// the sheet is set: much faster, and it would be through the walls before they could stop it.
        /// </summary>
        internal const float TopSpeed = 27f;

        /// <summary>True for a hero that the things on the court can act on: on its feet or tumbling, in the arena, and not being carried.</summary>
        internal static bool InPlay(ColosseumDoll doll)
        {
            return doll != null && !doll.Lost && doll.Body != null && !doll.Body.isKinematic && doll.Shape.enabled;
        }

        internal void Hurt(ColosseumDoll victim, int amount, string word, Color color) { game.EventHurt(victim, amount, word, color); }
        internal void Shake(float amount, float slow = 0f) { game.EventShake(amount, slow); }
        internal void Thrill(float amount) { game.EventThrill(amount); }
        internal void Warped(ColosseumDoll doll) { game.EventWarped(doll); }
    }

    /// <summary>
    /// Shapes the events are drawn with that the mesh kit does not have. They are for the see-through material
    /// (Kit.StrokeMaterial), which draws both sides of whatever it is given, so nothing here is doubled.
    /// </summary>
    static class MapMesh
    {
        public static Color32 Tone(int hex, int alpha)
        {
            return new Color32((byte)(hex >> 16), (byte)(hex >> 8), (byte)hex, (byte)Mathf.Clamp(alpha, 0, 255));
        }

        /// <summary>A flat four-cornered sheet with a colour at each corner.</summary>
        public static void Sheet(MeshKit kit, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color32 ca, Color32 cb, Color32 cc, Color32 cd)
        {
            kit.Quad(a, b, c, d, ca, cb, cc, cd);
        }

        public static void Sheet(MeshKit kit, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color32 color)
        {
            Sheet(kit, a, b, c, d, color, color, color, color);
        }

        /// <summary>A ring lying on the ground that runs from one colour at its inner edge to another at its outer edge.</summary>
        public static void Band(MeshKit kit, Vector3 center, float inner, float outer, Color32 inside, Color32 outside, int seg = 40)
        {
            for (int i = 0; i < seg; i++)
            {
                float t0 = (float)i / seg * Mathf.PI * 2f, t1 = (float)(i + 1) / seg * Mathf.PI * 2f;
                var d0 = new Vector3(Mathf.Cos(t0), 0f, Mathf.Sin(t0));
                var d1 = new Vector3(Mathf.Cos(t1), 0f, Mathf.Sin(t1));
                Sheet(kit, center + d0 * inner, center + d1 * inner, center + d1 * outer, center + d0 * outer, inside, inside, outside, outside);
            }
        }

        /// <summary>An upright wall of light in a ring: one colour along its foot, another along its top.</summary>
        public static void Wall(MeshKit kit, Vector3 center, float radius, float height, Color32 foot, Color32 top, int seg = 32)
        {
            for (int i = 0; i < seg; i++)
            {
                float t0 = (float)i / seg * Mathf.PI * 2f, t1 = (float)(i + 1) / seg * Mathf.PI * 2f;
                Vector3 d0 = new Vector3(Mathf.Cos(t0), 0f, Mathf.Sin(t0)) * radius, d1 = new Vector3(Mathf.Cos(t1), 0f, Mathf.Sin(t1)) * radius;
                Sheet(kit, center + d0, center + d1, center + d1 + Vector3.up * height, center + d0 + Vector3.up * height, foot, foot, top, top);
            }
        }

        /// <summary>A straight strip on the ground from one point to another, so wide.</summary>
        public static void Stroke(MeshKit kit, Vector3 from, Vector3 to, float wide, Color32 color)
        {
            Vector3 along = to - from;
            if (along.sqrMagnitude < 1e-8f) return;
            Vector3 side = Vector3.Cross(Vector3.up, along.normalized) * (wide * .5f);
            Sheet(kit, from - side, from + side, to + side, to - side, color);
        }
    }
}
