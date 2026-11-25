using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.UI;
using TMPro;

namespace SlingArena
{
    [DisallowMultipleComponent]
    public sealed class BladeShowdown : MonoBehaviour
    {
        private const int BaseHealth = 100;
        private const int BaseDamage = 34;
        private const float MaximumPull = 2.0f;
        private const float MinimumPull = 0.18f;
        private const float UnitPickRadius = 0.49f;
        private const string PullPrompt = "HOLD A HERO & PULL BACK";
        private const float CameraPitch = 59f;
        private const float CourtOverviewHalfHeight = 7.70f;
        private const float CourtOverviewHalfWidth = 4.45f;
        private const int MousePointer = -1;
        private const int NoPointer = -2;

        [SerializeField] private TMP_FontAsset outfitFont;
        [SerializeField] private Material blackOutline;
        [SerializeField] private Shader feedbackShader;

        private enum MatchScreen { Ready, Battle, Result }

        private Camera arenaCamera;
        private ArenaCombat combat;
        private ArenaHud hud;
        private ArenaFeedback feedback;
        private MatchScreen screen;
        private bool paused;
        private bool showingKnockout;
        private float chainStatusUntil;
        private int chainHits;
        private int pointer = NoPointer;
        private Vector3 holdPoint;
        private Vector2 aim;
        private float power;
        private Vector3 cameraRest;
        private float shakeStart;
        private float shakeLength;
        private float shakeMagnitude;
        private readonly int[] playerHealth = new int[ArenaCombat.UnitsPerSide];
        private readonly int[] enemyHealth = new int[ArenaCombat.UnitsPerSide];

        private void Awake()
        {
            Application.targetFrameRate = 60;
            if (Application.isMobilePlatform)
            {
                Screen.orientation = ScreenOrientation.Portrait;
            }

            var world = new GameObject("Toy arena").transform;
            world.SetParent(transform, false);
            arenaCamera = CreateCamera();
            ConfigureLighting();
            ArenaArt.BuildEnvironment(world);

            combat = gameObject.AddComponent<ArenaCombat>();
            combat.Initialize(world);
            combat.Launched += OnLaunched;
            combat.Impact += OnImpact;
            combat.RoundFinished += OnRoundFinished;

            feedback = new ArenaFeedback(world, arenaCamera, outfitFont, blackOutline, feedbackShader);
            hud = gameObject.AddComponent<ArenaHud>();
            hud.Initialize(outfitFont, blackOutline);
            hud.FightPressed += OnFightPressed;
            hud.PassPressed += OnPassPressed;
            hud.RestartPressed += RestartMatch;
            hud.PauseChanged += SetPause;
            StartMatch();
        }

        private void OnDestroy()
        {
            if (paused)
                Time.timeScale = 1f;
            feedback?.Dispose();
        }

        private void Update()
        {
            PositionCamera();
            feedback.Tick(Time.deltaTime);
            UpdateUnitMarkers();
            bool isStriking = combat.TryGetBladeTip(out Vector3 bladeTip);
            feedback.SetWeaponTrail(isStriking && !paused, bladeTip, combat.IsPlayerTurn,
                combat.GetUnitWeapon(combat.IsPlayerTurn, combat.ActiveUnitIndex));
            if (screen != MatchScreen.Battle)
                return;

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                SetPause(!paused);
                return;
            }

            if (paused)
                return;

            if (!showingKnockout && combat.InputReady)
                ReadPointer();
            else if (pointer != NoPointer)
                CancelPull();

            string status = showingKnockout ? "KNOCKOUT!" :
                Time.time < chainStatusUntil ? (chainHits > 1 ? $"CHAIN x{chainHits}!" : "CHAIN HIT!") :
                combat.TurnInMotion ? (combat.IsPlayerTurn ? "SLING STRIKE" : "RIVAL STRIKE") :
                combat.InputReady ? (pointer == NoPointer ? PullPrompt :
                    power > 0f ? "RELEASE TO LAUNCH" : "PULL BACK TO AIM") :
                combat.TurnHandoff ? "TURN ENDING" : "RIVAL AIMING";

            RefreshHud(status);
        }

        private void StartMatch()
        {
            StopAllCoroutines();
            CancelPull();
            screen = MatchScreen.Ready;
            showingKnockout = false;
            SetPause(false);
            chainStatusUntil = 0f;
            chainHits = 0;
            feedback.Clear();
            combat.BeginRound(1, BaseHealth, 1f, BaseDamage, BaseHealth, 1f, BaseDamage);
            PositionCamera();
            RefreshHud(string.Empty);
            hud.ShowReady();
        }

        private void RestartMatch()
        {
            StartMatch();
            OnFightPressed();
        }

        private void OnFightPressed()
        {
            if (screen != MatchScreen.Ready)
                return;

            feedback.PlaySelect();
            screen = MatchScreen.Battle;
            PositionCamera();
            combat.SetPaused(false);
            hud.ShowBattle();
            RefreshHud(PullPrompt);
        }

        private void OnPassPressed()
        {
            if (screen != MatchScreen.Battle || paused || !combat.TryPassTurn())
                return;

            CancelPull();
            feedback.PlaySelect();
            RefreshHud("TURN ENDING");
        }

        private void RefreshHud(string status)
        {
            for (int i = 0; i < ArenaCombat.UnitsPerSide; i++)
            {
                playerHealth[i] = combat.GetUnitHealth(true, i);
                enemyHealth[i] = combat.GetUnitHealth(false, i);
            }
            bool canPass = screen == MatchScreen.Battle && !paused && !showingKnockout && combat.InputReady;
            float time = canPass ? Mathf.Clamp01(combat.TurnTimeRemaining / ArenaCombat.AimTurnLimit) : 0f;
            hud.SetBattle(combat.IsPlayerTurn, canPass, time, status,
                playerHealth, BaseHealth, enemyHealth, BaseHealth, combat.ActiveUnitIndex);
        }

        private void UpdateUnitMarkers()
        {
            bool showSelection = screen == MatchScreen.Battle && !showingKnockout;
            bool showAllySelection = pointer != NoPointer || combat.TurnInMotion;
            for (int i = 0; i < ArenaCombat.UnitsPerSide; i++)
            {
                bool active = showSelection && combat.ActiveUnitIndex == i;
                feedback.SetUnitMarker(true, i, combat.GetUnitPosition(true, i), combat.GetUnitHealth(true, i),
                    BaseHealth, active && combat.IsPlayerTurn && showAllySelection);
                feedback.SetUnitMarker(false, i, combat.GetUnitPosition(false, i), combat.GetUnitHealth(false, i),
                    BaseHealth, active && !combat.IsPlayerTurn);
            }
        }

        private void OnLaunched(Vector3 origin, Vector3 direction, float strength, bool player)
        {
            chainHits = 0;
            chainStatusUntil = 0f;
            feedback.ShowLaunch(origin, direction, strength, player);
            ShakeCamera(Mathf.Lerp(.014f, .042f, strength), .14f);
        }

        private void OnImpact(Vector3 point, int damage, bool victimIsPlayer, bool chain, ArenaArt.Part part)
        {
            if (chain)
            {
                chainHits++;
                chainStatusUntil = Time.time + 0.65f;
            }
            ArenaArt.WeaponStyle weapon = combat.GetUnitWeapon(combat.IsPlayerTurn, combat.ActiveUnitIndex);
            feedback.ShowImpact(point, damage, chain, part, weapon, combat.LastImpactDirection);
            bool heavy = !chain && weapon == ArenaArt.WeaponStyle.Hammer;
            ShakeCamera(heavy ? .115f : chain ? .052f : .073f, heavy ? .23f : .18f);
        }

        private void OnRoundFinished(bool playerWon)
        {
            if (screen != MatchScreen.Battle || showingKnockout)
                return;

            showingKnockout = true;
            CancelPull();
            StartCoroutine(ShowResult(playerWon));
        }

        private IEnumerator ShowResult(bool playerWon)
        {
            yield return new WaitForSeconds(0.85f);
            screen = MatchScreen.Result;
            feedback.PlayResult(playerWon);
            hud.ShowResult(playerWon);
        }

        private void SetPause(bool value)
        {
            if (value && (screen != MatchScreen.Battle || showingKnockout))
                return;

            if (value)
                CancelPull();
            paused = value;
            Time.timeScale = value ? 0f : 1f;
            combat.SetPaused(value || screen != MatchScreen.Battle || showingKnockout);
            hud.ShowPause(value);
        }

        private void ReadPointer()
        {
            if (Input.touchCount > 0)
            {
                if (pointer == MousePointer)
                    CancelPull();

                int heldFinger = pointer;
                for (int i = 0; i < Input.touchCount; i++)
                {
                    Touch touch = Input.GetTouch(i);
                    if (heldFinger == NoPointer)
                    {
                        if (touch.phase == TouchPhase.Began)
                            BeginPull(touch.position, touch.fingerId);
                        if (pointer != NoPointer)
                            return;
                        continue;
                    }

                    if (heldFinger != touch.fingerId)
                        continue;

                    if (touch.phase == TouchPhase.Canceled)
                        CancelPull();
                    else if (touch.phase == TouchPhase.Ended)
                        ReleasePull(touch.position);
                    else
                        UpdatePull(touch.position);
                    return;
                }
                if (pointer != NoPointer)
                    CancelPull();
                return;
            }

            if (pointer >= 0)
                CancelPull();

            if (Input.GetMouseButtonDown(0))
                BeginPull(Input.mousePosition, MousePointer);
            if (pointer == MousePointer && Input.GetMouseButtonUp(0))
                ReleasePull(Input.mousePosition);
            else if (pointer == MousePointer && Input.GetMouseButton(0))
                UpdatePull(Input.mousePosition);
        }

        private void BeginPull(Vector2 screenPoint, int pointerId)
        {
            if (pointer != NoPointer || IsOverButton(screenPoint))
                return;

            if (!PointerOnPlane(screenPoint, out holdPoint))
                return;

            int unit = PickPlayerUnit(screenPoint);
            if (unit < 0 || !combat.SelectPlayerUnit(unit))
                return;

            pointer = pointerId;
            aim = Vector2.zero;
            power = 0f;
            feedback.ShowWeaponDraw(combat.GetUnitPosition(true, unit), combat.GetUnitWeapon(true, unit));
        }

        private int PickPlayerUnit(Vector2 screenPoint)
        {
            Ray ray = arenaCamera.ScreenPointToRay(screenPoint);
            if (combat.TryPickUnit(ray, out bool isPlayer, out int index))
                return isPlayer ? index : -1;

            int closest = -1;
            float pixelsPerUnit = arenaCamera.pixelHeight / (2f * arenaCamera.orthographicSize);
            float radius = UnitPickRadius * pixelsPerUnit;
            float best = radius * radius;
            for (int i = 0; i < ArenaCombat.UnitsPerSide; i++)
            {
                if (combat.GetUnitHealth(true, i) <= 0)
                    continue;
                Vector3 chest = combat.GetUnitPosition(true, i) + Vector3.up * 0.42f;
                Vector2 projected = arenaCamera.WorldToScreenPoint(chest);
                float distance = (projected - screenPoint).sqrMagnitude;
                if (distance > best)
                    continue;
                best = distance;
                closest = i;
            }
            return closest;
        }

        private bool IsOverButton(Vector2 screenPoint)
        {
            var events = EventSystem.current;
            if (events == null)
                return false;
            var data = new PointerEventData(events) { position = screenPoint };
            var hits = new System.Collections.Generic.List<RaycastResult>();
            events.RaycastAll(data, hits);
            foreach (var hit in hits)
            {
                if (hit.gameObject.GetComponentInParent<Selectable>() != null)
                    return true;
            }
            return false;
        }

        private bool PointerOnPlane(Vector2 screenPoint, out Vector3 point)
        {
            Ray ray = arenaCamera.ScreenPointToRay(screenPoint);
            var plane = new Plane(Vector3.up, Vector3.zero);
            if (plane.Raycast(ray, out float distance))
            {
                point = ray.GetPoint(distance);
                return true;
            }

            point = default;
            return false;
        }

        private void UpdatePull(Vector2 screenPoint)
        {
            if (pointer == NoPointer)
                return;

            int selected = combat.ActiveUnitIndex;
            if (selected < 0 || !PointerOnPlane(screenPoint, out Vector3 point))
            {
                CancelPull();
                return;
            }

            Vector3 pull = holdPoint - point;
            Vector2 force = new Vector2(pull.x, pull.z);
            float distance = force.magnitude;
            if (distance < MinimumPull)
            {
                if (power > 0f)
                    combat.AimSelectedUnit(Vector2.zero);
                aim = Vector2.zero;
                power = 0f;
                feedback.HideAim();
                return;
            }

            aim = force.normalized;
            power = Mathf.Clamp01(distance / MaximumPull);
            combat.AimSelectedUnit(aim);
            Vector3 origin = combat.GetUnitPosition(true, selected);
            feedback.SetAim(origin, aim, power, true, FindAimedRival(origin));
        }

        private Vector3? FindAimedRival(Vector3 origin)
        {
            const float targetRadius = 0.56f;
            float nearest = ArenaFeedback.GetAimDistance(origin, aim, power);
            Vector3? target = null;
            for (int i = 0; i < ArenaCombat.UnitsPerSide; i++)
            {
                if (combat.GetUnitHealth(false, i) <= 0)
                    continue;

                Vector3 rival = combat.GetUnitPosition(false, i);
                Vector2 offset = new Vector2(rival.x - origin.x, rival.z - origin.z);
                float along = Vector2.Dot(offset, aim);
                float across = Mathf.Abs(offset.x * aim.y - offset.y * aim.x);
                if (along <= 0f || along >= nearest || across > targetRadius)
                    continue;

                nearest = along;
                target = rival;
            }
            return target;
        }

        private void ReleasePull(Vector2 screenPoint)
        {
            UpdatePull(screenPoint);
            if (power > 0f)
                combat.TryLaunch(aim, power);
            CancelPull();
        }

        private void CancelPull()
        {
            if (pointer != NoPointer)
                combat.ClearPlayerSelection();
            pointer = NoPointer;
            aim = Vector2.zero;
            power = 0f;
            feedback.HideAim();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus && pointer != NoPointer)
                CancelPull();
        }

        private void OnApplicationPause(bool suspended)
        {
            if (suspended && pointer != NoPointer)
                CancelPull();
        }

        private Camera CreateCamera()
        {
            var cameraObject = new GameObject("Arena Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.transform.SetParent(transform, false);
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.GetComponent<Camera>();
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color32(101, 156, 105, 255);
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 80f;
            camera.allowMSAA = true;
            camera.allowHDR = false;
            Quaternion angle = Quaternion.Euler(CameraPitch, 0f, 0f);
            Vector3 focus = new Vector3(0f, 0.23f, 0.20f);
            cameraRest = focus - angle * Vector3.forward * 18f;
            camera.transform.SetPositionAndRotation(cameraRest, angle);
            return camera;
        }

        private void PositionCamera()
        {
            arenaCamera.orthographicSize = Mathf.Max(CourtOverviewHalfHeight, CourtOverviewHalfWidth / arenaCamera.aspect);
            float elapsed = Time.time - shakeStart;
            Vector3 offset = Vector3.zero;
            if (elapsed < shakeLength)
            {
                float fade = 1f - elapsed / shakeLength;
                Transform view = arenaCamera.transform;
                offset = (view.right * Mathf.Sin(elapsed * 116f) + view.up * Mathf.Sin(elapsed * 84f) * .58f)
                    * (shakeMagnitude * fade * fade);
            }
            arenaCamera.transform.position = cameraRest + offset;
        }

        private void ShakeCamera(float magnitude, float duration)
        {
            shakeStart = Time.time;
            shakeMagnitude = magnitude;
            shakeLength = duration;
        }

        private void ConfigureLighting()
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color32(200, 226, 248, 255);
            RenderSettings.ambientEquatorColor = new Color32(171, 181, 154, 255);
            RenderSettings.ambientGroundColor = new Color32(163, 120, 79, 255);
            var light = new GameObject("Warm arena daylight", typeof(Light)).GetComponent<Light>();
            light.transform.SetParent(transform, false);
            light.transform.rotation = Quaternion.Euler(55f, -37f, 0f);
            light.type = LightType.Directional;
            light.color = new Color32(255, 239, 198, 255);
            light.intensity = 1.32f;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.73f;
            light.shadowBias = 0.04f;
            light.shadowNormalBias = 0.3f;
            RenderSettings.sun = light;
        }
    }
}
