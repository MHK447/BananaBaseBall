using System;
using System.Collections.Generic;
using UnityEngine;

namespace SlingArena
{
    public sealed class ArenaCombat : MonoBehaviour
    {
        public const int UnitsPerSide = 3;
        public const float HalfWidth = 3.55f;
        public const float HalfDepth = 5.35f;
        public const float TurnLimit = 2f;
        public const float AimTurnLimit = 22f;

        const float BladeOffset = .34f;
        const float BladeLength = 1.02f;
        const float AxeEdgeLength = .983f;
        const float HammerHeadLength = 1.016f;
        const float HipHeight = 1.04f;
        const float BladeHeight = .31f;
        const float HandoffTime = .28f;
        const float EnemyAimTime = .8f;
        const float AimWarningTime = 5f;
        const float DrawPoseRate = 11f;
        const float MaximumFlightTime = 4f;
        const float FlightDamping = 1.3f;
        const float IdleDamping = 7f;
        const float RicochetTime = 1.05f;
        const float ContactSkin = .028f;
        const float LimbDamping = .18f;
        const float MinimumLaunchSpeed = 4.25f;
        const float MaximumLaunchSpeed = 22f;
        const float MaximumPlanarSpeed = 24f;

        // Matches the mitten center drawn below each forearm in ArenaDollArt.
        static readonly Vector3 PalmOffset = new(0f, -.221616f, .034f);
        static readonly Vector3 DominantGripOffset = new(.046f, 0f, .15f);

        readonly struct WeaponSection
        {
            public readonly float Start;
            public readonly float End;
            public readonly float LateralCenter;
            public readonly float HalfWidth;
            public readonly float HalfHeight;

            public WeaponSection(float start, float end, float lateralCenter, float halfWidth, float halfHeight)
            {
                Start = start;
                End = end;
                LateralCenter = lateralCenter;
                HalfWidth = halfWidth;
                HalfHeight = halfHeight;
            }
        }

        // Close-fitting sweep sections match each weapon's painted head.
        static readonly WeaponSection[] SwordSections =
        {
            new(.252f, .797f, 0f, .142f, .034f),
            new(.797f, .924f, 0f, .072f, .026f),
            new(.924f, BladeLength, 0f, .022f, .013f)
        };

        static readonly WeaponSection[] AxeSections =
        {
            new(.53f, .62f, .195f, .105f, .050f),
            new(.62f, .68f, .133f, .178f, .061f),
            new(.68f, .82f, .090f, .215f, .061f),
            new(.82f, .86f, .129f, .173f, .061f),
            new(.86f, AxeEdgeLength, .125f, .159f, .054f)
        };

        static readonly WeaponSection[] HammerSections =
        {
            new(.70f, .73f, 0f, .184f, .092f),
            new(.73f, .985f, 0f, .231f, .116f),
            new(.985f, HammerHeadLength, 0f, .184f, .092f)
        };

        enum Phase { Aim, Flight, Handoff, Finished }

        sealed class Toy
        {
            public readonly List<Rigidbody> Bodies = new();
            public readonly List<Pose> GripPoses = new();
            public readonly List<Pose> EmptyHandPoses = new();
            public readonly Dictionary<Collider, ArenaArt.Part> Colliders = new();
            public readonly List<ConfigurableJoint> Joints = new();
            public GameObject Rig;
            public Rigidbody Hips;
            public Rigidbody Chest;
            public Rigidbody RightForearm;
            public Quaternion RightForearmRest;
            public BoxCollider HipCollider;
            public BoxCollider ChestCollider;
            public Transform WeaponMount;
            public ArenaArt.WeaponStyle Weapon;
            public float GripProgress;
            public bool IsPlayer;
            public int Health;
            public int Damage;
            public float Reach;
            public float Yaw;
            public Vector3 Direction;
            public float Lean;
            public Vector3 LeanDirection;
            public Vector3 PreviousPosition;
            public Vector3 PreviousChestPosition;
            public Vector3 PreviousVelocity;
            public Vector3 PreviousDirection;
            public bool Braced;
            public bool ClearedCollisions;
            public bool Standing;
            public int LastHitTurn;
            public float RicochetRemaining;
            public int RicochetDamage;
        }

        struct ContactHit
        {
            public Toy Target;
            public ArenaArt.Part Part;
            public Vector3 Point;
            public Vector3 Normal;
            public float Fraction;
        }

        readonly Collider[] overlaps = new Collider[48];
        readonly RaycastHit[] sweeps = new RaycastHit[48];
        readonly Toy[] player = new Toy[UnitsPerSide];
        readonly Toy[] enemy = new Toy[UnitsPerSide];

        Transform world;
        PhysicsMaterial toySurface;
        Toy visibleWeapon;
        Phase phase = Phase.Finished;
        bool paused;
        bool bladeHit;
        int roundNumber;
        int turnNumber;
        float phaseTime;
        float flightTime;

        public event Action<bool> RoundFinished;
        public event Action<Vector3, int, bool, bool, ArenaArt.Part> Impact;
        public event Action<Vector3, Vector3, float, bool> Launched;

        public bool IsPlayerTurn { get; private set; }
        public int ActiveUnitIndex { get; private set; } = -1;
        public bool InputReady => IsPlayerTurn && phase == Phase.Aim && !paused;
        public bool TurnInMotion => phase == Phase.Flight;
        public bool TurnHandoff => phase == Phase.Handoff;
        public float TurnTimeRemaining => phase == Phase.Aim || phase == Phase.Flight ? phaseTime : 0f;
        internal bool TurnWarning => InputReady && phaseTime <= AimWarningTime;
        internal Vector3 LastImpactDirection { get; private set; }

        Toy Active => GetUnit(IsPlayerTurn, ActiveUnitIndex);
        Toy[] Rivals => IsPlayerTurn ? enemy : player;

        public void Initialize(Transform worldParent)
        {
            world = worldParent != null ? worldParent : transform;
        }

        public void SetPaused(bool pause)
        {
            paused = pause;
        }

        public Vector3 GetUnitPosition(bool isPlayer, int index)
        {
            Toy toy = GetUnit(isPlayer, index);
            return toy == null ? Vector3.zero : toy.Hips.transform.position;
        }

        public int GetUnitHealth(bool isPlayer, int index) => GetUnit(isPlayer, index)?.Health ?? 0;

        public ArenaArt.WeaponStyle GetUnitWeapon(bool isPlayer, int index) =>
            GetUnit(isPlayer, index)?.Weapon ?? WeaponForSlot(index);

        internal static ArenaArt.WeaponStyle WeaponForSlot(int index) => index switch
        {
            0 => ArenaArt.WeaponStyle.Axe,
            2 => ArenaArt.WeaponStyle.Hammer,
            _ => ArenaArt.WeaponStyle.Sword
        };

        internal bool TryPickUnit(Ray ray, out bool isPlayer, out int index)
        {
            isPlayer = false;
            index = -1;
            float nearest = float.PositiveInfinity;
            for (int side = 0; side < 2; side++)
            {
                bool playerSide = side == 0;
                Toy[] team = GetTeam(playerSide);
                for (int i = 0; i < team.Length; i++)
                {
                    Toy toy = team[i];
                    if (toy == null || toy.Health <= 0)
                        continue;

                    foreach (Collider collider in toy.Colliders.Keys)
                    {
                        if (!collider.Raycast(ray, out RaycastHit hit, nearest) || hit.distance >= nearest)
                            continue;

                        nearest = hit.distance;
                        isPlayer = playerSide;
                        index = i;
                    }
                }
            }
            return index >= 0;
        }

        internal bool TryGetBladeTip(out Vector3 tip)
        {
            Toy toy = Active;
            if (phase != Phase.Flight || bladeHit || toy == null)
            {
                tip = default;
                return false;
            }

            float forward = toy.Weapon switch
            {
                ArenaArt.WeaponStyle.Axe => AxeEdgeLength,
                ArenaArt.WeaponStyle.Hammer => HammerHeadLength,
                _ => BladeLength
            };
            Vector3 side = toy.Weapon == ArenaArt.WeaponStyle.Axe
                ? Vector3.Cross(Vector3.up, toy.Direction) * .18f
                : Vector3.zero;
            tip = toy.Hips.transform.position + Vector3.up * BladeHeight + side +
                toy.Direction * (BladeOffset + forward * toy.Reach);
            return true;
        }

        public int GetTeamHealth(bool isPlayer)
        {
            int total = 0;
            foreach (Toy toy in GetTeam(isPlayer))
                total += toy?.Health ?? 0;
            return total;
        }

        Toy[] GetTeam(bool isPlayer) => isPlayer ? player : enemy;

        Toy GetUnit(bool isPlayer, int index)
        {
            return index < 0 || index >= UnitsPerSide ? null : GetTeam(isPlayer)[index];
        }

        public void BeginRound(int round, int playerHealth, float playerReach, int playerDamage,
            int enemyHealth, float enemyReach, int enemyDamage)
        {
            DisposeTeam(player);
            DisposeTeam(enemy);
            visibleWeapon = null;
            if (world == null)
                Initialize(transform);
            if (toySurface == null)
            {
                toySurface = new PhysicsMaterial("SlingArena toy bodies")
                {
                    dynamicFriction = .1f,
                    staticFriction = .12f,
                    bounciness = .22f,
                    frictionCombine = PhysicsMaterialCombine.Minimum
                };
            }

            roundNumber = round;
            turnNumber = 0;
            LastImpactDirection = Vector3.zero;
            for (int slot = 0; slot < UnitsPerSide; slot++)
            {
                player[slot] = MakeToy(true, slot, playerHealth, playerReach, playerDamage);
                enemy[slot] = MakeToy(false, slot, enemyHealth, enemyReach, enemyDamage);
            }
            StartTurn(true);
        }

        public bool SelectPlayerUnit(int index)
        {
            Toy toy = GetUnit(true, index);
            if (!InputReady || toy == null || toy.Health <= 0)
                return false;

            if (Active != null && Active != toy)
                FaceRival(Active);
            ActiveUnitIndex = index;
            FaceRival(toy);
            RefreshWeaponVisibility();
            return true;
        }

        internal void ClearPlayerSelection()
        {
            if (!IsPlayerTurn || phase != Phase.Aim || ActiveUnitIndex < 0)
                return;

            FaceRival(Active);
            ActiveUnitIndex = -1;
            RefreshWeaponVisibility();
        }

        internal bool TryPassTurn()
        {
            if (!InputReady)
                return false;

            BeginHandoff();
            return true;
        }

        public void AimSelectedUnit(Vector2 direction)
        {
            Toy toy = Active;
            if (!InputReady || toy == null)
                return;

            Vector3 heading = new(direction.x, 0f, direction.y);
            if (heading.sqrMagnitude >= .0001f)
                toy.Direction = heading.normalized;
            else
                FaceRival(toy);
        }

        public bool TryLaunch(Vector2 worldDirectionXZ, float normalizedPower)
        {
            Toy toy = Active;
            if (!InputReady || toy == null || worldDirectionXZ.sqrMagnitude < .0001f || !(normalizedPower >= .05f))
                return false;

            Launch(toy, new Vector3(worldDirectionXZ.x, 0f, worldDirectionXZ.y), normalizedPower);
            return true;
        }

        void StartTurn(bool isPlayer)
        {
            IsPlayerTurn = isPlayer;
            ActiveUnitIndex = -1;
            turnNumber++;
            phase = Phase.Aim;
            phaseTime = isPlayer ? AimTurnLimit : EnemyAimTime;
            flightTime = 0f;
            PrepareTeam(player, enemy);
            PrepareTeam(enemy, player);
            if (!isPlayer)
                SelectRivalUnit();
            RefreshWeaponVisibility();
        }

        void BeginHandoff()
        {
            phase = Phase.Handoff;
            phaseTime = HandoffTime;
            ActiveUnitIndex = -1;
            RefreshWeaponVisibility();
        }

        void PrepareTeam(Toy[] team, Toy[] opposite)
        {
            foreach (Toy toy in team)
            {
                toy.RicochetRemaining = 0f;
                if (toy.Health == 0)
                {
                    if (!toy.ClearedCollisions)
                    {
                        IgnoreTeam(toy, team);
                        IgnoreTeam(toy, opposite);
                        toy.ClearedCollisions = true;
                    }
                    continue;
                }

                FaceRival(toy);
                StandToy(toy);
                RememberMotion(toy);
            }
        }

        // Preserve the stance while waiting. A launched or struck toy immediately returns to physics.
        static void StandToy(Toy toy)
        {
            if (!toy.Standing)
            {
                EndShove(toy);
                foreach (Rigidbody body in toy.Bodies)
                {
                    body.linearVelocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                    body.isKinematic = true;
                }
                toy.Standing = true;
                toy.Lean = 0f;
                toy.GripProgress = 0f;
                PlaceStandingPose(toy, true);
            }
        }

        static void ReleaseStance(Toy toy)
        {
            if (!toy.Standing)
                return;

            toy.Standing = false;
            foreach (Rigidbody body in toy.Bodies)
            {
                body.isKinematic = false;
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
        }

        static void PlaceStandingPose(Toy toy, bool immediately)
        {
            Vector3 center = toy.Hips.position;
            center.y = HipHeight;
            Quaternion rotation = Quaternion.Euler(0f, toy.Yaw, 0f);
            for (int i = 0; i < toy.Bodies.Count; i++)
            {
                Rigidbody body = toy.Bodies[i];
                Pose grip = toy.GripPoses[i];
                Pose empty = toy.EmptyHandPoses[i];
                Vector3 position = center + rotation * Vector3.Lerp(empty.position, grip.position, toy.GripProgress);
                Quaternion orientation = rotation * Quaternion.Slerp(empty.rotation, grip.rotation, toy.GripProgress);
                if (immediately)
                {
                    body.position = position;
                    body.rotation = orientation;
                }
                else
                {
                    body.MovePosition(position);
                    body.MoveRotation(orientation);
                }
            }
        }

        static void IgnoreTeam(Toy toy, Toy[] team)
        {
            foreach (Toy other in team)
            {
                if (other == toy || other.Health == 0)
                    continue;

                foreach (Collider deadBody in toy.Colliders.Keys)
                    foreach (Collider aliveBody in other.Colliders.Keys)
                        Physics.IgnoreCollision(deadBody, aliveBody);
            }
        }

        void SelectRivalUnit()
        {
            int best = -1;
            float bestScore = float.PositiveInfinity;
            for (int index = 0; index < UnitsPerSide; index++)
            {
                Toy toy = enemy[index];
                if (toy.Health == 0)
                    continue;
                Toy target = ClosestLiving(toy, player);
                if (target == null)
                    continue;

                float distance = Flat(target.Hips.position - toy.Hips.position).magnitude;
                float score = distance + target.Health * .012f;
                if (score < bestScore)
                {
                    best = index;
                    bestScore = score;
                }
            }
            ActiveUnitIndex = best;
        }

        void LaunchRival()
        {
            Toy shooter = Active;
            if (shooter == null)
                return;
            Toy target = ClosestLiving(shooter, player);
            if (target == null)
                return;

            Vector3 direction = Flat(target.Hips.position - shooter.Hips.position);
            if (direction.sqrMagnitude < .0001f)
                direction = Vector3.back;
            float angle = Mathf.Sin(roundNumber * 2.11f + turnNumber * 1.73f) * 5f;
            float power = Mathf.Clamp(direction.magnitude / 8f + .24f, .30f, .97f);
            Launch(shooter, Quaternion.Euler(0f, angle, 0f) * direction, power);
        }

        void Launch(Toy toy, Vector3 direction, float power)
        {
            toy.Direction = direction.normalized;
            toy.Yaw = Mathf.Atan2(toy.Direction.x, toy.Direction.z) * Mathf.Rad2Deg;
            toy.GripProgress = 1f;
            PlaceStandingPose(toy, true);
            ReleaseStance(toy);

            float strength = Mathf.Clamp01(power);
            Vector3 velocity = toy.Direction * Mathf.Lerp(MinimumLaunchSpeed, MaximumLaunchSpeed, strength);
            // The shove moves the whole toy together and braces its joints during the approach.
            // Contact releases the stance into the same physical rig used for the recoil.
            toy.Braced = true;
            toy.Lean = 0f;
            toy.Hips.constraints = RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezeRotationX |
                RigidbodyConstraints.FreezeRotationZ;
            foreach (Rigidbody body in toy.Bodies)
            {
                body.useGravity = false;
                body.linearDamping = FlightDamping;
                body.linearVelocity = velocity;
            }
            RememberMotion(toy);
            phase = Phase.Flight;
            phaseTime = TurnLimit;
            flightTime = 0f;
            bladeHit = false;
            RefreshWeaponVisibility();
            PlaceVisibleWeapon();
            Launched?.Invoke(toy.Hips.position, toy.Direction, strength, toy.IsPlayer);
        }

        static void EndShove(Toy toy)
        {
            if (!toy.Braced)
                return;

            toy.Braced = false;
            toy.Hips.constraints = RigidbodyConstraints.FreezePositionY;
            foreach (Rigidbody body in toy.Bodies)
            {
                body.useGravity = body != toy.Hips;
                body.linearDamping = body == toy.Hips ? FlightDamping : LimbDamping;
            }
        }

        void FixedUpdate()
        {
            if (player[0] == null)
                return;

            float delta = Time.fixedDeltaTime;
            UpdateBodies(player, delta);
            UpdateBodies(enemy, delta);
            if (paused)
                return;

            switch (phase)
            {
                case Phase.Aim:
                    phaseTime = Mathf.Max(0f, phaseTime - delta);
                    if (phaseTime <= 0f)
                    {
                        if (IsPlayerTurn) BeginHandoff();
                        else LaunchRival();
                    }
                    break;
                case Phase.Flight:
                    UpdateFlight(delta);
                    break;
                case Phase.Handoff:
                    phaseTime -= delta;
                    if (phaseTime <= 0f)
                        StartTurn(!IsPlayerTurn);
                    break;
            }
        }

        void UpdateBodies(Toy[] team, float delta)
        {
            foreach (Toy toy in team)
            {
                if (toy.Health == 0 && toy.RicochetRemaining <= 0f)
                    continue;

                if (toy.Standing)
                {
                    float previousYaw = toy.Yaw;
                    float previousGrip = toy.GripProgress;
                    toy.GripProgress = Mathf.MoveTowards(previousGrip, toy == visibleWeapon ? 1f : 0f, delta * DrawPoseRate);
                    TurnTowardHeading(toy, delta);
                    if (Mathf.Abs(Mathf.DeltaAngle(previousYaw, toy.Yaw)) > .01f ||
                        !Mathf.Approximately(previousGrip, toy.GripProgress))
                        PlaceStandingPose(toy, false);
                    continue;
                }

                if (phase != Phase.Finished)
                    Bound(toy);
                if (toy.Health > 0)
                    Stabilize(toy, delta);
            }
        }

        void UpdateFlight(float delta)
        {
            Toy attacker = Active;
            if (attacker == null)
                return;

            phaseTime -= delta;
            flightTime += delta;
            if (!bladeHit)
                CheckWeapon(attacker);
            if (phase != Phase.Flight || Active != attacker)
                return;

            foreach (Toy toy in Rivals)
            {
                if (toy.RicochetRemaining <= 0f)
                    continue;
                toy.RicochetRemaining -= delta;
                CheckRicochet(toy);
                if (phase != Phase.Flight || Active != attacker)
                    return;
            }

            foreach (Toy toy in player) RememberMotion(toy);
            foreach (Toy toy in enemy) RememberMotion(toy);

            if (phaseTime <= 0f || flightTime >= MaximumFlightTime ||
                (flightTime >= .5f && !HasMomentum(player) && !HasMomentum(enemy)))
                BeginHandoff();
        }

        static bool HasMomentum(Toy[] team)
        {
            foreach (Toy toy in team)
            {
                if (!toy.Standing && (toy.Health > 0 || toy.RicochetRemaining > 0f) &&
                    Flat(toy.Hips.linearVelocity).sqrMagnitude > .48f * .48f)
                    return true;
            }
            return false;
        }

        void CheckWeapon(Toy attacker)
        {
            Vector3 current = attacker.Hips.position;
            if (Flat(current - attacker.PreviousPosition).sqrMagnitude < .000001f)
                return;

            WeaponSection[] sections = attacker.Weapon switch
            {
                ArenaArt.WeaponStyle.Axe => AxeSections,
                ArenaArt.WeaponStyle.Hammer => HammerSections,
                _ => SwordSections
            };
            float previousAngle = Heading(attacker.PreviousDirection);
            float currentAngle = Heading(attacker.Direction);
            int steps = Mathf.Clamp(Mathf.CeilToInt(Mathf.Abs(Mathf.DeltaAngle(previousAngle, currentAngle)) / 12f), 1, 6);
            for (int step = 0; step < steps; step++)
            {
                float from = (float)step / steps;
                float to = (float)(step + 1) / steps;
                Quaternion rotation = Quaternion.Euler(0f, Mathf.LerpAngle(previousAngle, currentAngle, to), 0f);
                Vector3 direction = rotation * Vector3.forward;
                Vector3 rootStart = Vector3.Lerp(attacker.PreviousPosition, current, from);
                Vector3 rootEnd = Vector3.Lerp(attacker.PreviousPosition, current, to);
                ContactHit contact = default;
                Vector3 right = rotation * Vector3.right;
                foreach (WeaponSection section in sections)
                {
                    float center = BladeOffset + (section.Start + section.End) * .5f * attacker.Reach;
                    Vector3 offset = Vector3.up * BladeHeight + direction * center + right * section.LateralCenter;
                    Vector3 extents = new(section.HalfWidth, section.HalfHeight, (section.End - section.Start) * .5f * attacker.Reach);
                    SweepBox(attacker, rootStart + offset, rootEnd + offset, extents, rotation, 0f, ref contact);
                }

                if (contact.Target == null)
                    continue;

                bladeHit = true;
                Vector3 impactPosition = Vector3.Lerp(rootStart, rootEnd, contact.Fraction);
                foreach (Rigidbody body in attacker.Bodies)
                    body.position += impactPosition - current;

                attacker.Direction = direction;
                float speed = Mathf.Max(Flat(attacker.PreviousVelocity).magnitude, Flat(attacker.Hips.linearVelocity).magnitude);
                EndShove(attacker);
                foreach (Rigidbody body in attacker.Bodies)
                    body.linearVelocity *= .26f;

                attacker.Chest.AddForceAtPosition(-direction * 1.15f, contact.Point, ForceMode.Impulse);

                Vector3 knockback = direction;
                float push = Mathf.Clamp(speed * .73f, 4.1f, 10.3f);
                if (attacker.Weapon == ArenaArt.WeaponStyle.Axe)
                {
                    float contactSide = Vector3.Dot(contact.Point - impactPosition, right);
                    knockback = Quaternion.Euler(0f, contactSide < -.035f ? -25f : 25f, 0f) * direction;
                }
                else if (attacker.Weapon == ArenaArt.WeaponStyle.Hammer)
                {
                    push = Mathf.Clamp(speed * .94f, 5.4f, 13.8f);
                }

                Strike(contact, attacker.Damage, knockback, push, false);
                return;
            }
        }

        void CheckRicochet(Toy carrier)
        {
            Vector3 velocity = carrier.PreviousVelocity;
            if (Flat(velocity).sqrMagnitude < 1.1f * 1.1f)
                return;

            ContactHit contact = default;
            SweepBody(carrier, carrier.Hips, carrier.HipCollider, carrier.PreviousPosition, ref contact);
            SweepBody(carrier, carrier.Chest, carrier.ChestCollider, carrier.PreviousChestPosition, ref contact);
            if (contact.Target == null)
                return;

            Vector3 separation = Flat(contact.Target.Hips.position - carrier.Hips.position).normalized;
            Vector3 normal = Flat(-contact.Normal).normalized;
            if (normal.sqrMagnitude < .1f || Vector3.Dot(normal, separation) < .35f)
                normal = separation;

            Vector3 rivalVelocity = contact.Target.Standing ? Vector3.zero : contact.Target.Hips.linearVelocity;
            float closingSpeed = Vector3.Dot(Flat(velocity - rivalVelocity), normal);
            if (closingSpeed < .65f)
                return;

            carrier.RicochetRemaining = 0f;
            carrier.Hips.linearVelocity = Vector3.Reflect(Flat(velocity), normal) * .28f + Vector3.up * velocity.y;
            bool heavy = Active.Weapon == ArenaArt.WeaponStyle.Hammer;
            Vector3 knockback = Vector3.Slerp(normal, Flat(velocity).normalized, heavy ? .48f : .28f).normalized;
            float carriedSpeed = heavy
                ? Mathf.Clamp(closingSpeed * .9f + 1.5f, 3.2f, 11.2f)
                : Mathf.Clamp(closingSpeed * .85f + 1.2f, 2.7f, 8.8f);
            Strike(contact, carrier.RicochetDamage, knockback, carriedSpeed, true);
        }

        void SweepBody(Toy carrier, Rigidbody body, BoxCollider shape, Vector3 previous, ref ContactHit contact)
        {
            Vector3 travel = body.position - previous;
            Vector3 momentum = Flat(carrier.PreviousVelocity).normalized;
            // Use the attempted movement when the solver stops the toy at an already touching pose.
            if (Vector3.Dot(travel, momentum) < ContactSkin)
                travel = momentum * ContactSkin;
            SweepBox(carrier, previous, previous + travel, shape.size * .5f, body.rotation, ContactSkin, ref contact);
        }

        void SweepBox(Toy source, Vector3 start, Vector3 end, Vector3 extents, Quaternion rotation, float skin,
            ref ContactHit earliest)
        {
            Vector3 travel = end - start;
            float distance = travel.magnitude;
            if (distance < .0001f)
                return;

            Vector3 direction = travel / distance;
            int count = Physics.BoxCastNonAlloc(start, extents, direction, sweeps, rotation, distance + skin,
                Physics.AllLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = sweeps[i];
                Toy target = ContactTarget(source, hit.collider, direction, out ArenaArt.Part part);
                float fraction = Mathf.Clamp01(hit.distance / distance);
                if (target == null || (earliest.Target != null && fraction >= earliest.Fraction))
                    continue;

                Vector3 point = hit.distance > .0001f ? hit.point : hit.collider.ClosestPoint(start);
                earliest = new ContactHit { Target = target, Part = part, Point = point, Normal = hit.normal, Fraction = fraction };
            }

            if (earliest.Target != null && earliest.Fraction <= 0f)
                return;

            // An overlap handles a blade already touching at the start of the fixed step.
            count = Physics.OverlapBoxNonAlloc(start, extents, overlaps, rotation, Physics.AllLayers,
                QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Collider collider = overlaps[i];
                Toy target = ContactTarget(source, collider, direction, out ArenaArt.Part part);
                if (target == null)
                    continue;
                earliest = new ContactHit { Target = target, Part = part, Point = collider.ClosestPoint(start),
                    Normal = (source.Hips.position - target.Hips.position).normalized, Fraction = 0f };
                return;
            }
        }

        Toy ContactTarget(Toy source, Collider collider, Vector3 direction, out ArenaArt.Part part)
        {
            foreach (Toy target in Rivals)
            {
                if (target == source || target.Health == 0 || target.LastHitTurn == turnNumber ||
                    !target.Colliders.TryGetValue(collider, out part))
                    continue;
                if (Vector3.Dot(Flat(target.Hips.position - source.Hips.position), direction) >= -.16f)
                    return target;
            }
            part = default;
            return null;
        }

        void Strike(ContactHit contact, int amount, Vector3 direction, float speed, bool isRicochet)
        {
            Toy target = contact.Target;
            int damage = Mathf.Min(target.Health, amount);
            target.Health -= damage;
            target.LastHitTurn = turnNumber;
            target.RicochetRemaining = RicochetTime;
            target.RicochetDamage = Mathf.Max(1, Mathf.RoundToInt(amount * (isRicochet ? .80f : .62f)));
            target.Lean = 1f;
            target.LeanDirection = direction;
            phaseTime = Mathf.Min(TurnLimit, MaximumFlightTime - flightTime);

            ReleaseStance(target);
            Vector3 impulse = direction * speed;
            if (target.Health == 0)
            {
                DropToy(target);
                target.Hips.AddForce(Vector3.up * 1.6f, ForceMode.VelocityChange);
                target.Hips.AddTorque(Vector3.Cross(Vector3.up, direction) * 4f, ForceMode.VelocityChange);
            }
            target.Hips.AddForce(impulse, ForceMode.VelocityChange);
            target.Chest.AddForceAtPosition(direction * 1.6f, contact.Point, ForceMode.Impulse);

            bool complete = GetTeamHealth(target.IsPlayer) == 0;
            if (complete)
            {
                phase = Phase.Finished;
                RefreshWeaponVisibility();
            }

            LastImpactDirection = direction;
            Impact?.Invoke(contact.Point, damage, target.IsPlayer, isRicochet, contact.Part);
            if (complete)
                RoundFinished?.Invoke(!target.IsPlayer);
        }

        static void DropToy(Toy toy)
        {
            toy.Hips.constraints = RigidbodyConstraints.None;
            toy.Hips.useGravity = true;
            toy.Hips.linearDamping = .7f;
            foreach (ConfigurableJoint joint in toy.Joints)
                joint.slerpDrive = default;
            toy.WeaponMount.gameObject.SetActive(false);
        }

        void FaceRival(Toy toy)
        {
            Toy target = ClosestLiving(toy, GetTeam(!toy.IsPlayer));
            if (target != null)
            {
                Vector3 delta = Flat(target.Hips.position - toy.Hips.position);
                if (delta.sqrMagnitude > .0001f)
                    toy.Direction = delta.normalized;
            }
        }

        static Toy ClosestLiving(Toy origin, Toy[] team)
        {
            Toy closest = null;
            float distance = float.PositiveInfinity;
            foreach (Toy candidate in team)
            {
                if (candidate.Health <= 0)
                    continue;
                float square = Flat(candidate.Hips.position - origin.Hips.position).sqrMagnitude;
                if (square < distance)
                {
                    closest = candidate;
                    distance = square;
                }
            }
            return closest;
        }

        void Stabilize(Toy toy, float delta)
        {
            if (phase == Phase.Flight && toy == Active)
            {
                Vector3 motion = Flat(toy.Hips.linearVelocity);
                if (motion.sqrMagnitude > .45f * .45f)
                    toy.Direction = motion.normalized;
            }

            TurnTowardHeading(toy, delta);
            toy.Lean = Mathf.MoveTowards(toy.Lean, 0f, delta * 2.3f);
            Quaternion yaw = Quaternion.Euler(0f, toy.Yaw, 0f);
            Quaternion lean = Quaternion.AngleAxis(toy.Lean * 38f, Vector3.Cross(Vector3.up, toy.LeanDirection));
            Quaternion difference = lean * yaw * Quaternion.Inverse(toy.Hips.rotation);
            difference.ToAngleAxis(out float angle, out Vector3 axis);
            if (angle > 180f) angle -= 360f;
            Vector3 torque = axis * (angle * Mathf.Deg2Rad * 49f) - toy.Hips.angularVelocity * 10f;
            toy.Hips.AddTorque(Vector3.ClampMagnitude(torque, 85f), ForceMode.Acceleration);
            toy.Hips.linearDamping = phase == Phase.Flight || phase == Phase.Handoff ? FlightDamping : IdleDamping;
        }

        static void TurnTowardHeading(Toy toy, float delta)
        {
            toy.Yaw = Mathf.MoveTowardsAngle(toy.Yaw, Heading(toy.Direction), delta * 500f);
        }

        static float Heading(Vector3 direction) => Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;

        static void Bound(Toy toy)
        {
            Vector3 position = toy.Hips.position;
            Vector3 velocity = toy.Hips.linearVelocity;
            float restitution = toy.Weapon switch
            {
                ArenaArt.WeaponStyle.Axe => .90f,
                ArenaArt.WeaponStyle.Hammer => .73f,
                _ => .84f
            };
            if (Mathf.Abs(position.x) > HalfWidth)
                velocity.x = -Mathf.Sign(position.x) * Mathf.Abs(velocity.x) * restitution;
            if (Mathf.Abs(position.z) > HalfDepth)
                velocity.z = -Mathf.Sign(position.z) * Mathf.Abs(velocity.z) * restitution;
            Vector3 corrected = new(Mathf.Clamp(position.x, -HalfWidth, HalfWidth), position.y,
                Mathf.Clamp(position.z, -HalfDepth, HalfDepth));
            Vector3 shift = corrected - position;
            Vector3 clampedVelocity = Vector3.ClampMagnitude(velocity, MaximumPlanarSpeed);
            Vector3 velocityChange = clampedVelocity - toy.Hips.linearVelocity;
            bool moved = shift.sqrMagnitude > .000001f;
            bool bounced = velocityChange.sqrMagnitude > .000001f;
            if (!moved && !bounced)
                return;

            if (toy.Braced)
            {
                foreach (Rigidbody body in toy.Bodies)
                {
                    if (moved) body.position += shift;
                    if (bounced) body.linearVelocity += velocityChange;
                }
            }
            else
            {
                if (moved) toy.Hips.position = corrected;
                if (bounced) toy.Hips.linearVelocity = clampedVelocity;
            }
        }

        static void RememberMotion(Toy toy)
        {
            toy.PreviousPosition = toy.Hips.position;
            toy.PreviousChestPosition = toy.Chest.position;
            toy.PreviousDirection = toy.Direction;
            toy.PreviousVelocity = toy.Standing ? Vector3.zero : toy.Hips.linearVelocity;
        }

        static Vector3 Flat(Vector3 vector) => new(vector.x, 0f, vector.z);

        void RefreshWeaponVisibility()
        {
            Toy equipped = phase == Phase.Aim || phase == Phase.Flight ? Active : null;
            if (visibleWeapon == equipped)
                return;

            if (visibleWeapon != null)
                visibleWeapon.WeaponMount.gameObject.SetActive(false);

            visibleWeapon = equipped;
            if (visibleWeapon != null)
            {
                PlaceVisibleWeapon();
                visibleWeapon.WeaponMount.gameObject.SetActive(true);
            }
        }

        void LateUpdate()
        {
            PlaceVisibleWeapon();
        }

        void PlaceVisibleWeapon()
        {
            Toy toy = visibleWeapon;
            if (toy == null)
                return;

            float draw = toy.Standing ? Mathf.SmoothStep(.20f, 1f, toy.GripProgress) : 1f;
            toy.WeaponMount.localScale = new Vector3(draw, draw, toy.Reach * draw);

            if (!toy.Standing && !toy.Braced)
            {
                Transform forearm = toy.RightForearm.transform;
                Quaternion gripFacing = forearm.rotation * Quaternion.Inverse(toy.RightForearmRest);
                Vector3 palm = forearm.TransformPoint(PalmOffset);
                toy.WeaponMount.SetPositionAndRotation(palm - gripFacing * DominantGripOffset, gripFacing);
                return;
            }

            Vector3 forward = toy.Standing ? toy.Hips.transform.forward : toy.Direction;
            Vector3 center = toy.Hips.transform.position + Vector3.up * BladeHeight;
            toy.WeaponMount.SetPositionAndRotation(center + forward * BladeOffset,
                Quaternion.LookRotation(forward, Vector3.up));
        }

        Toy MakeToy(bool isPlayer, int slot, int health, float reach, int damage)
        {
            float side = isPlayer ? -1f : 1f;
            Vector2 formation = slot switch
            {
                0 => new Vector2(-1.55f, 3.35f),
                1 => new Vector2(0f, 2.65f),
                _ => new Vector2(isPlayer ? 1.35f : .34f, 4.55f)
            };
            var toy = new Toy
            {
                Rig = new GameObject((isPlayer ? "Blue" : "Rival") + " Knight " + (slot + 1)),
                IsPlayer = isPlayer,
                Weapon = WeaponForSlot(slot),
                Yaw = isPlayer ? 0f : 180f,
                Direction = isPlayer ? Vector3.forward : Vector3.back,
                Reach = Mathf.Clamp(reach, .65f, 2.15f),
                Health = Mathf.Max(1, health),
                Damage = Mathf.Max(1, damage)
            };
            toy.Rig.transform.SetParent(world, false);
            toy.Rig.transform.SetPositionAndRotation(new Vector3(formation.x, HipHeight, formation.y * side),
                Quaternion.Euler(0f, toy.Yaw, 0f));

            toy.Hips = AddPart(toy, ArenaArt.Part.Hips, Vector3.zero, new Vector3(.46f, .33f, .37f), 4.5f, slot);
            toy.HipCollider = toy.Hips.GetComponent<BoxCollider>();
            toy.Hips.constraints = RigidbodyConstraints.FreezePositionY;
            toy.Hips.useGravity = false;
            toy.Hips.linearDamping = IdleDamping;
            toy.Chest = AddPart(toy, ArenaArt.Part.Torso, new Vector3(0f, .36f, 0f),
                new Vector3(.57f, .63f, .42f), 1.75f, slot, toy.Hips, new Vector3(0f, -.24f, 0f));
            toy.ChestCollider = toy.Chest.GetComponent<BoxCollider>();
            AddPart(toy, ArenaArt.Part.Head, new Vector3(0f, .90f, .015f),
                new Vector3(.65f, .62f, .55f), .72f, slot, toy.Chest, new Vector3(0f, -.28f, 0f));

            AddArm(toy, slot, false);
            AddArm(toy, slot, true);
            AddLeg(toy, slot, -1f, ArenaArt.Part.LeftThigh, ArenaArt.Part.LeftShin);
            AddLeg(toy, slot, 1f, ArenaArt.Part.RightThigh, ArenaArt.Part.RightShin);

            string mountName = toy.Weapon == ArenaArt.WeaponStyle.Sword ? "Forward sword" : "Forward " + toy.Weapon;
            toy.WeaponMount = new GameObject(mountName).transform;
            toy.WeaponMount.SetParent(toy.Rig.transform, false);
            toy.WeaponMount.localScale = new Vector3(1f, 1f, toy.Reach);
            ArenaDollArt.AttachWeapon(toy.WeaponMount, isPlayer, slot, toy.Weapon);
            toy.WeaponMount.gameObject.SetActive(false);
            return toy;
        }

        void AddArm(Toy toy, int slot, bool right)
        {
            float side = right ? 1f : -1f;
            Vector3 shoulder = new(.285f * side, .49f, .015f);
            Vector3 elbow = right ? new Vector3(.36f, .19f, .24f) : new Vector3(-.36f, .17f, .17f);
            Vector3 swordBase = new(0f, BladeHeight, BladeOffset);
            Vector3 palm = swordBase + (right ? DominantGripOffset : new Vector3(-.049f, -.012f, .03f));
            Quaternion upperPose = Quaternion.FromToRotation(Vector3.down, elbow - shoulder);
            Vector3 upperCenter = (shoulder + elbow) * .5f;
            Vector3 upperAnchor = Quaternion.Inverse(upperPose) * (shoulder - upperCenter);
            int upperIndex = toy.Bodies.Count;
            Rigidbody upper = AddPart(toy, right ? ArenaArt.Part.RightUpperArm : ArenaArt.Part.LeftUpperArm,
                upperCenter, new Vector3(.18f, .50f, .19f), .36f, slot, toy.Chest, upperAnchor, upperPose);

            Quaternion forearmPose = Quaternion.FromToRotation(Vector3.down, palm - elbow);
            Vector3 forearmCenter = palm - forearmPose * PalmOffset;
            Vector3 forearmAnchor = Quaternion.Inverse(forearmPose) * (elbow - forearmCenter);
            int forearmIndex = toy.Bodies.Count;
            Rigidbody forearm = AddPart(toy, right ? ArenaArt.Part.RightForearm : ArenaArt.Part.LeftForearm,
                forearmCenter, new Vector3(.19f, .54f, .22f), .29f, slot, upper, forearmAnchor, forearmPose);

            // Relax the arms around the same shoulder and elbow anchors used by the physics joints.
            Quaternion looseUpper = Quaternion.Euler(0f, 0f, side * 8f);
            Vector3 looseUpperCenter = shoulder - looseUpper * upperAnchor;
            Vector3 elbowOnUpper = Quaternion.Inverse(upperPose) * (elbow - upperCenter);
            Vector3 looseElbow = looseUpperCenter + looseUpper * elbowOnUpper;
            Quaternion looseForearm = Quaternion.Euler(-6f, 0f, side * 5f);
            toy.EmptyHandPoses[upperIndex] = new Pose(looseUpperCenter, looseUpper);
            toy.EmptyHandPoses[forearmIndex] = new Pose(looseElbow - looseForearm * forearmAnchor, looseForearm);
            if (right)
            {
                toy.RightForearm = forearm;
                toy.RightForearmRest = forearmPose;
            }
        }

        void AddLeg(Toy toy, int slot, float side, ArenaArt.Part thigh, ArenaArt.Part shin)
        {
            Rigidbody leg = AddPart(toy, thigh, new Vector3(.16f * side, -.33f, 0f),
                new Vector3(.22f, .47f, .26f), .57f, slot, toy.Hips, new Vector3(0f, .21f, 0f));
            AddPart(toy, shin, new Vector3(.16f * side, -.76f, .035f),
                new Vector3(.23f, .51f, .28f), .47f, slot, leg, new Vector3(0f, .22f, 0f));
        }

        Rigidbody AddPart(Toy toy, ArenaArt.Part part, Vector3 offset, Vector3 size, float mass, int slot,
            Rigidbody connected = null, Vector3 jointAnchor = default, Quaternion? restRotation = null)
        {
            var piece = new GameObject(part.ToString());
            var anchor = piece.transform;
            anchor.SetParent(toy.Rig.transform, false);
            anchor.localPosition = offset;
            anchor.localRotation = restRotation ?? Quaternion.identity;

            var body = piece.AddComponent<Rigidbody>();
            body.mass = mass;
            body.linearDamping = LimbDamping;
            body.angularDamping = .85f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            body.solverIterations = 12;
            body.solverVelocityIterations = 5;
            body.maxAngularVelocity = 24f;

            Collider collider;
            if (part == ArenaArt.Part.Hips || part == ArenaArt.Part.Torso)
            {
                var box = piece.AddComponent<BoxCollider>();
                box.size = size;
                collider = box;
            }
            else
            {
                var capsule = piece.AddComponent<CapsuleCollider>();
                capsule.radius = Mathf.Min(size.x, size.z) * .5f;
                capsule.height = size.y;
                collider = capsule;
            }

            collider.sharedMaterial = toySurface;
            toy.Bodies.Add(body);
            var rest = new Pose(offset, anchor.localRotation);
            toy.GripPoses.Add(rest);
            toy.EmptyHandPoses.Add(rest);
            foreach (Collider otherPart in toy.Colliders.Keys)
                Physics.IgnoreCollision(collider, otherPart);
            toy.Colliders.Add(collider, part);
            if (connected != null)
                ConnectPart(toy, body, connected, jointAnchor, part);
            ArenaDollArt.AttachPart(part, anchor, toy.IsPlayer, slot, size);
            return body;
        }

        static void ConnectPart(Toy toy, Rigidbody body, Rigidbody connected, Vector3 anchor, ArenaArt.Part part)
        {
            var joint = body.gameObject.AddComponent<ConfigurableJoint>();
            joint.connectedBody = connected;
            joint.anchor = anchor;
            joint.autoConfigureConnectedAnchor = false;
            joint.connectedAnchor = connected.transform.InverseTransformPoint(body.transform.TransformPoint(anchor));
            joint.xMotion = joint.yMotion = joint.zMotion = ConfigurableJointMotion.Locked;
            joint.angularXMotion = joint.angularYMotion = joint.angularZMotion = ConfigurableJointMotion.Limited;
            joint.lowAngularXLimit = new SoftJointLimit { limit = -56f };
            joint.highAngularXLimit = new SoftJointLimit { limit = 56f };
            joint.angularYLimit = new SoftJointLimit { limit = 42f };
            joint.angularZLimit = new SoftJointLimit { limit = 50f };
            joint.projectionMode = JointProjectionMode.PositionAndRotation;
            joint.projectionDistance = .08f;
            joint.projectionAngle = 15f;
            joint.rotationDriveMode = RotationDriveMode.Slerp;
            joint.slerpDrive = new JointDrive
            {
                positionSpring = part == ArenaArt.Part.Torso ? 72f : 19f,
                positionDamper = part == ArenaArt.Part.Torso ? 11f : 3.8f,
                maximumForce = 70f
            };
            toy.Joints.Add(joint);
        }

        void OnDestroy()
        {
            DisposeTeam(player);
            DisposeTeam(enemy);
            if (toySurface != null)
                Destroy(toySurface);
        }

        static void DisposeTeam(Toy[] team)
        {
            foreach (Toy toy in team)
            {
                if (toy?.Rig == null)
                    continue;
                toy.Rig.SetActive(false);
                Destroy(toy.Rig);
            }
        }
    }
}
