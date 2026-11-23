using UnityEngine;
using UnityEngine.InputSystem;

namespace StarFall
{
    [RequireComponent(typeof(BattleController), typeof(GameAudio), typeof(GameUI))]
    public sealed partial class GameApplication : MonoBehaviour
    {
        [SerializeField] private Camera worldCamera;
        public MatchSession Session { get; } = new MatchSession();
        public BattleController Battle { get; private set; }
        public GameUI UI { get; private set; }
        public GameAudio Audio { get; private set; }
        private float resultRemaining;
        public bool SettingsOpen { get; private set; }

        private void Start()
        {
            Application.targetFrameRate = 120;
            Battle = GetComponent<BattleController>();
            Audio = GetComponent<GameAudio>();
            UI = GetComponent<GameUI>();
            Battle.Initialize(Session, Audio);
            InitializeProgress();
            UI.Initialize(this);
            Session.StateChanged += OnStateChanged;
            if (!Application.isEditor && PlayerPrefs.HasKey("StarFall.Fullscreen"))
                Screen.fullScreen = PlayerPrefs.GetInt("StarFall.Fullscreen") == 1;
            UI.ShowState(MatchState.MainMenu);
            if (!Progress.Ready || !string.IsNullOrEmpty(Progress.Error)) BlockSave(Progress.Load);
        }

        private void Update()
        {
            if (Battle == null || SaveBlocked) return;
            FitCamera();
            var keyboard = Keyboard.current;
            HandleTrainingInput(keyboard);
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                if (UI.CloseWorkshop()) { }
                else if (UI.CancelConfirmation()) { }
                else if (SettingsOpen) CloseSettings();
                else if (Session.State == MatchState.Playing || Session.State == MatchState.ClaimingReward) Session.Pause();
                else if (Session.State == MatchState.Paused) Session.Resume();
            }
            Vector2 movement = Vector2.zero;
            if (keyboard != null)
            {
                movement.x = (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed ? 1 : 0)
                    - (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed ? 1 : 0);
                movement.y = (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed ? 1 : 0)
                    - (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed ? 1 : 0);
            }
            Vector3? aimPoint = null;
            if (Mouse.current != null)
            {
                var mouse = Mouse.current.position.ReadValue();
                if (worldCamera.pixelRect.Contains(mouse))
                {
                    var ray = worldCamera.ScreenPointToRay(mouse);
                    if (new Plane(Vector3.up, Vector3.zero).Raycast(ray, out float distance)) aimPoint = ray.GetPoint(distance);
                }
            }
            Battle.Step(Time.deltaTime, movement, aimPoint, keyboard != null && keyboard.spaceKey.isPressed, keyboard != null && keyboard.qKey.wasPressedThisFrame);
            if (keyboard != null && keyboard.eKey.wasPressedThisFrame) Battle.TryOpenChest();
            if (Session.State == MatchState.Won || Session.State == MatchState.Lost)
            {
                resultRemaining -= Time.unscaledDeltaTime;
                if (resultRemaining <= 0 && !TestMode) ReturnToBase();
            }
            UI.Refresh(Session, resultRemaining);
        }

        private void FitCamera()
        {
            if (worldCamera == null) return;
            var size = Battle.Config.arenaSize;
            worldCamera.orthographicSize = Mathf.Max(size.y * 0.5f + 3,
                (size.x * 0.5f + 2) / Mathf.Max(0.2f, worldCamera.aspect));
        }

        public void NewGame()
        {
            if (Session.State != MatchState.MainMenu || SettingsOpen || SaveBlocked || !Progress.Ready) return;
            Session.ReturnToBase();
        }

        public void Resume() { if (!SaveBlocked) Session.Resume(); }
        public void ChooseBuild(BuildId id, int revision) { if (!SaveBlocked) Session.ChooseBuild(id, revision); }
        public void ReturnToMenu() => RequestReturn(false);

        public void OpenSettings()
        {
            if (Session.State != MatchState.MainMenu && Session.State != MatchState.Paused && Session.State != MatchState.HomeBase) return;
            SettingsOpen = true;
            UI.SetSettingsVisible(true);
        }

        public void CloseSettings()
        {
            Audio.Save();
            SettingsOpen = false;
            UI.SetSettingsVisible(false);
            UI.ShowState(Session.State);
        }

        public void SetFullscreen(bool enabled)
        {
            if (!Application.isEditor) Screen.fullScreen = enabled;
            PlayerPrefs.SetInt("StarFall.Fullscreen", enabled ? 1 : 0);
            PlayerPrefs.Save();
        }

        public void Quit()
        {
            Audio.Save();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void OnStateChanged(MatchState state)
        {
            Battle.SetPresentationActive(state == MatchState.Playing);
            if (state == MatchState.Won || state == MatchState.Lost)
            {
                resultRemaining = Battle.Config.resultDisplaySeconds;
                if (!SettleRun()) BlockSave(SettleRun);
                if (state == MatchState.Won) Audio.PlayVictory();
            }
            UI.ShowState(state);
        }

        private void OnDestroy() { Session.StateChanged -= OnStateChanged; Session.SoulsCollected -= SaveCollectedSouls; }
    }
}
