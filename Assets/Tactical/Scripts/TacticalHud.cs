using System.Collections.Generic;
using UnityEngine;

namespace FpsTactical
{
    /// <summary>
    /// IMGUI HUD of a tactical match: round timer, score and alive counts per side, bomb status with plant / defuse
    /// progress, round and match banners, kill feed, the Tab scoreboard, the spectator bar and the class-select panel
    /// shown during freeze time. Everything is laid out in virtual pixels and scaled so a phone-sized screen stays readable.
    /// </summary>
    [DefaultExecutionOrder(-5)]
    public sealed class TacticalHud : MonoBehaviour
    {
        private const float BarHeight = 46f;
        private const float TopY = 26f;
        private const float KillFeedSeconds = 7f;

        private static readonly Color Panel = new Color(0.03f, 0.04f, 0.06f, 0.78f);
        private static readonly Color Gold = new Color(1f, 0.85f, 0.25f);
        private static readonly Color Good = new Color(0.4f, 0.95f, 0.5f);
        private static readonly Color Bad = new Color(1f, 0.35f, 0.3f);

        private readonly Dictionary<int, GUIStyle> styles = new Dictionary<int, GUIStyle>();
        private readonly List<TacticalParticipant> rows = new List<TacticalParticipant>();

        private TacticalMatch match;
        private float vw = 1f;
        private float vh = 1f;
        private float bannerStart = -100f;
        private float bannerLength;
        private Color bannerColor = Color.white;
        private CombatTeam lastPlayerSide = CombatTeam.None;

        private bool classSelectOpen;
        private bool pageShown;
        private ILoadoutMenuPage shownPage;
        private FirstPersonCamera look;
        private WeaponSwitcher switcher;
        private LoadoutPauseMenu pauseMenu;

        public float Scale { get; private set; } = 1f;
        public string BannerTitle { get; private set; }
        public string BannerSubtitle { get; private set; }

        /// <summary>Shows the scoreboard without holding Tab (tests and screenshots).</summary>
        public bool ForceScoreboard { get; set; }

        /// <summary>True while the class-select panel is on screen.</summary>
        public bool IsClassSelectOpen => classSelectOpen && match != null && match.Model != null
            && match.Model.Phase == TacticalPhase.ClassSelect;

        public bool BannerActive => !string.IsNullOrEmpty(BannerTitle) && Time.unscaledTime - bannerStart < bannerLength;

        public bool ScoreboardVisible => match != null && match.Model != null
            && (ForceScoreboard || Input.GetKey(KeyCode.Tab) || match.Model.Phase == TacticalPhase.MatchEnd);

        public void Bind(TacticalMatch owner)
        {
            match = owner;
            match.PhaseEntered += OnPhaseEntered;
            match.Model.RoundEnded += OnRoundEnded;
        }

        private void OnDestroy()
        {
            if (match != null)
            {
                match.PhaseEntered -= OnPhaseEntered;
                if (match.Model != null)
                    match.Model.RoundEnded -= OnRoundEnded;
            }

            SetClassSelectActive(false);
        }

        /// <summary>The player pressed READY, or the page asked to continue: skips the rest of freeze time.</summary>
        public void Ready()
        {
            if (match != null && match.Model != null && match.Model.Phase == TacticalPhase.ClassSelect)
                match.Model.SkipFreeze();
        }

        #region Events

        private void OnPhaseEntered(TacticalPhase phase)
        {
            TacticalMatchModel model = match.Model;
            CombatTeam side = match.PlayerSide;
            switch (phase)
            {
                case TacticalPhase.ClassSelect:
                {
                    bool swapped = lastPlayerSide != CombatTeam.None && lastPlayerSide != side && model.RoundNumber > 1;
                    lastPlayerSide = side;
                    string title = swapped ? "SIDES SWAPPED" : "ROUND " + model.RoundNumber;
                    string sub = "YOU " + (side == CombatTeam.Attackers ? "ATTACK" : "DEFEND") + "  -  " + TacticalHudText.RoleHint(side);
                    ShowBanner(title, sub, CombatTeams.ColorOf(side), 3.4f);
                    classSelectOpen = match.Player != null;
                    break;
                }
                case TacticalPhase.Live:
                    classSelectOpen = false;
                    ShowBanner("GO", match.PlayerIsCarrier ? "YOU HAVE THE BOMB" : string.Empty, Good, 1.2f);
                    break;
                case TacticalPhase.MatchEnd:
                {
                    bool won = model.MatchWinnerSquad == TacticalMatch.PlayerSquad;
                    string score = model.Score(TacticalMatch.PlayerSquad) + " - " + model.Score(1 - TacticalMatch.PlayerSquad);
                    ShowBanner(TacticalHudText.MatchTitle(won), score, won ? Good : Bad, Mathf.Max(2f, match.Rules.matchEndSeconds));
                    break;
                }
            }
        }

        private void OnRoundEnded(RoundResult result)
        {
            bool won = result.WinnerSquad == TacticalMatch.PlayerSquad;
            ShowBanner(TacticalHudText.RoundTitle(won), TacticalMatchModel.Describe(result.Reason), won ? Good : Bad,
                Mathf.Max(2f, match.Rules.roundEndSeconds));
        }

        private void ShowBanner(string title, string subtitle, Color color, float seconds)
        {
            BannerTitle = title;
            BannerSubtitle = subtitle;
            bannerColor = color;
            bannerStart = Time.unscaledTime;
            bannerLength = seconds;
        }

        #endregion

        #region Class select state

        private void Update()
        {
            if (match == null || !match.IsRunning)
                return;

            bool show = IsClassSelectOpen && !PauseMenuOpen();
            SetClassSelectActive(show);
            if (show && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)))
                Ready();
        }

        private bool PauseMenuOpen()
        {
            if (pauseMenu == null)
                pauseMenu = FindAnyObjectByType<LoadoutPauseMenu>();
            return pauseMenu != null && pauseMenu.IsOpen;
        }

        private void SetClassSelectActive(bool show)
        {
            if (show)
            {
                ResolvePlayerParts();
                if (look != null)
                    look.SetCursorLock(false);
                if (switcher != null)
                    switcher.SetSwitchingEnabled(false);
                if (!pageShown)
                {
                    shownPage = LoadoutMenuPages.Primary;
                    if (shownPage != null)
                        shownPage.OnPageShown();
                    pageShown = true;
                }

                return;
            }

            if (!pageShown)
                return;

            pageShown = false;
            if (shownPage != null && (!(shownPage is Object unityObject) || unityObject != null))
                shownPage.OnPageHidden();
            shownPage = null;

            if (PauseMenuOpen())
                return;
            if (switcher != null)
                switcher.SetSwitchingEnabled(true);
            if (look != null)
                look.SetCursorLock(true);
        }

        private void ResolvePlayerParts()
        {
            if (match.Player == null)
                return;
            if (look == null)
                look = match.Player.GetComponentInChildren<FirstPersonCamera>(true);
            if (switcher == null)
                switcher = match.Player.GetComponentInChildren<WeaponSwitcher>(true);
        }

        #endregion

        #region Drawing helpers

        private GUIStyle Style(int size, TextAnchor anchor, bool bold)
        {
            int key = size * 100 + (int)anchor * 2 + (bold ? 1 : 0);
            if (styles.TryGetValue(key, out GUIStyle style) && style != null)
                return style;

            style = new GUIStyle(GUI.skin.label)
            {
                fontSize = size,
                alignment = anchor,
                fontStyle = bold ? FontStyle.Bold : FontStyle.Normal,
                wordWrap = false,
                clipping = TextClipping.Overflow
            };
            styles[key] = style;
            return style;
        }

        private static void Fill(Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
        }

        private void Label(Rect rect, string text, int size, TextAnchor anchor, Color color, bool bold = true)
        {
            if (string.IsNullOrEmpty(text))
                return;
            GUIStyle style = Style(size, anchor, bold);
            style.normal.textColor = new Color(0f, 0f, 0f, color.a * 0.85f);
            GUI.Label(new Rect(rect.x + 1f, rect.y + 1f, rect.width, rect.height), text, style);
            style.normal.textColor = color;
            GUI.Label(rect, text, style);
        }

        private static Color Tint(Color color, float factor, float alpha)
        {
            return new Color(color.r * factor, color.g * factor, color.b * factor, alpha);
        }

        #endregion

        private void OnGUI()
        {
            if (match == null || match.Model == null || !match.IsRunning)
                return;

            GUI.depth = -10;
            Scale = TacticalHudText.ResolveScale(Screen.width, Screen.height);
            vw = Screen.width / Scale;
            vh = Screen.height / Scale;
            Matrix4x4 previousMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(Scale, Scale, 1f));

            if (Event.current.type == EventType.Repaint)
            {
                DrawTopBar();
                DrawBombChip();
                DrawProgress();
                DrawKillFeed();
                DrawSpectatorBar();
                if (!IsClassSelectOpen)
                    DrawBanner();
                if (ScoreboardVisible)
                    DrawScoreboard();
            }

            if (IsClassSelectOpen && !PauseMenuOpen())
                DrawClassSelect();

            GUI.matrix = previousMatrix;
        }

        #region Top bar

        private void DrawTopBar()
        {
            TacticalMatchModel model = match.Model;
            const float blockW = 132f;
            const float centerW = 116f;
            float x0 = (vw - (blockW * 2f + centerW)) * 0.5f;
            const float y = TopY;

            int mine = TacticalMatch.PlayerSquad;
            DrawTeamBlock(new Rect(x0, y, blockW, BarHeight), mine, model.SideOf(mine), true);
            DrawTeamBlock(new Rect(x0 + blockW + centerW, y, blockW, BarHeight), 1 - mine, model.SideOf(1 - mine), false);
            DrawClock(new Rect(x0 + blockW, y, centerW, BarHeight));
        }

        private void DrawTeamBlock(Rect rect, int squad, CombatTeam side, bool mine)
        {
            Color color = CombatTeams.ColorOf(side);
            Fill(rect, Tint(color, 0.3f, 0.82f));
            Fill(new Rect(rect.x, rect.yMax - 3f, rect.width, 3f), color);

            string label = (mine ? "YOU  " : "ENEMY  ") + TacticalHudText.SideName(side);
            Label(new Rect(rect.x + 8f, rect.y + 2f, rect.width - 16f, 14f), label, 10, TextAnchor.MiddleLeft, new Color(1f, 1f, 1f, 0.85f));
            Label(new Rect(rect.x + 4f, rect.y + 14f, 46f, 28f), match.Model.Score(squad).ToString(), 26, TextAnchor.MiddleCenter, Color.white);

            int total = Mathf.Max(1, match.TotalCount(side));
            int alive = match.AliveCount(side);
            float spacing = Mathf.Min(16f, (rect.width - 62f) / total);
            for (int i = 0; i < total; i++)
            {
                Rect pip = new Rect(rect.x + 56f + i * spacing, rect.y + 24f, Mathf.Min(12f, spacing - 2f), 12f);
                Fill(pip, i < alive ? Color.white : new Color(0f, 0f, 0f, 0.55f));
            }
        }

        private void DrawClock(Rect rect)
        {
            TacticalMatchModel model = match.Model;
            Fill(rect, Panel);

            string time;
            string label;
            Color color = Color.white;
            switch (model.Phase)
            {
                case TacticalPhase.ClassSelect:
                    time = TacticalHudText.Clock(model.PhaseRemaining);
                    label = "FREEZE TIME";
                    color = Gold;
                    break;
                case TacticalPhase.Live:
                    if (model.Bomb.State == BombState.Planted)
                    {
                        time = TacticalHudText.Clock(model.Bomb.FuseRemaining);
                        label = "BOMB PLANTED";
                        color = Bad;
                    }
                    else
                    {
                        time = TacticalHudText.Clock(model.PhaseRemaining);
                        label = "ROUND " + model.RoundNumber;
                        if (model.PhaseRemaining <= 10f)
                            color = Bad;
                    }

                    break;
                default:
                    time = "0:00";
                    label = TacticalHudText.PhaseLabel(model.Phase);
                    if (model.Phase == TacticalPhase.RoundEnd || model.Phase == TacticalPhase.MatchEnd)
                        time = "--";
                    break;
            }

            Label(new Rect(rect.x, rect.y + 1f, rect.width, 28f), time, 24, TextAnchor.MiddleCenter, color);
            Label(new Rect(rect.x, rect.y + 28f, rect.width, 14f), label, 10, TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, 0.8f));
        }

        #endregion

        #region Bomb

        private void DrawBombChip()
        {
            TacticalMatchModel model = match.Model;
            if (model.Phase == TacticalPhase.ClassSelect || model.Phase == TacticalPhase.Inactive)
                return;

            TacticalBombModel bomb = model.Bomb;
            bool playerAttacks = match.PlayerSide == CombatTeam.Attackers;
            string carrierName = null;
            if (match.CarrierBot != null)
            {
                TacticalParticipant carrier = match.Stats.FindBot(match.CarrierBot);
                carrierName = carrier != null ? carrier.Name.ToUpperInvariant() : null;
            }

            string text = TacticalHudText.BombLine(bomb.State, bomb.Site, match.PlayerIsCarrier, carrierName, playerAttacks, bomb.FuseRemaining);
            Color color;
            switch (bomb.State)
            {
                case BombState.Planted:
                    color = Color.Lerp(Bad, Color.white, Mathf.PingPong(Time.unscaledTime * 2.5f, 1f) * 0.45f);
                    break;
                case BombState.Defused:
                    color = Good;
                    break;
                case BombState.Exploded:
                    color = Bad;
                    break;
                case BombState.Dropped:
                    color = new Color(1f, 0.6f, 0.2f);
                    break;
                default:
                    color = match.PlayerIsCarrier ? Gold : new Color(0.85f, 0.88f, 0.92f);
                    break;
            }

            float w = Mathf.Min(vw - 16f, 280f);
            Rect rect = new Rect((vw - w) * 0.5f, TopY + BarHeight + 4f, w, 22f);
            Fill(rect, Panel);
            Fill(new Rect(rect.x, rect.y, 3f, rect.height), color);
            Label(rect, text, 13, TextAnchor.MiddleCenter, color);

            if (bomb.State == BombState.Planted)
            {
                float remaining = 1f - bomb.FuseElapsed01;
                Fill(new Rect(rect.x, rect.yMax + 2f, rect.width, 4f), new Color(0f, 0f, 0f, 0.55f));
                Fill(new Rect(rect.x, rect.yMax + 2f, rect.width * Mathf.Clamp01(remaining), 4f), Bad);
            }
        }

        private void DrawProgress()
        {
            TacticalBombModel bomb = match.Model.Bomb;
            string prompt = match.ActionPrompt;
            bool planting = bomb.IsPlanting;
            bool defusing = bomb.IsDefusing;

            float w = Mathf.Min(vw - 40f, 260f);
            float x = (vw - w) * 0.5f;
            float y = vh * 0.66f;

            if (planting || defusing)
            {
                float progress = planting ? bomb.PlantProgress01 : bomb.DefuseProgress01;
                string label = TacticalHudText.ProgressLabel(planting, match.PlayerHoldingAction);
                Color color = planting ? Gold : new Color(0.45f, 0.75f, 1f);
                Label(new Rect(x, y - 20f, w, 18f), label, 13, TextAnchor.MiddleCenter, Color.white);
                Fill(new Rect(x - 2f, y - 2f, w + 4f, 18f), new Color(0f, 0f, 0f, 0.7f));
                Fill(new Rect(x, y, w * Mathf.Clamp01(progress), 14f), color);
                return;
            }

            if (!string.IsNullOrEmpty(prompt))
            {
                Fill(new Rect(x, y - 4f, w, 24f), Panel);
                Label(new Rect(x, y - 4f, w, 24f), prompt, 14, TextAnchor.MiddleCenter, Gold);
            }
        }

        #endregion

        #region Feed, banner, spectator

        private void DrawKillFeed()
        {
            IReadOnlyList<KillFeedEntry> feed = match.Stats.KillFeed;
            float y = vw < 640f ? TopY + BarHeight + 4f + 22f + 12f : 70f;
            float now = Time.unscaledTime;
            GUIStyle style = Style(12, TextAnchor.MiddleLeft, true);
            for (int i = 0; i < feed.Count; i++)
            {
                KillFeedEntry entry = feed[i];
                if (now - entry.Time > KillFeedSeconds)
                    continue;

                string killer = entry.Killer ?? string.Empty;
                string middle = string.IsNullOrEmpty(killer) ? string.Empty : "  >  ";
                string victim = entry.Victim + (entry.Headshot ? "  [HS]" : string.IsNullOrEmpty(killer) ? " died" : string.Empty);
                float kw = string.IsNullOrEmpty(killer) ? 0f : style.CalcSize(new GUIContent(killer)).x;
                float mw = style.CalcSize(new GUIContent(middle)).x;
                float vwd = style.CalcSize(new GUIContent(victim)).x;
                float total = kw + mw + vwd + 12f;
                float x = vw - 8f - total;

                Fill(new Rect(x, y, total, 20f), Panel);
                float cx = x + 6f;
                if (kw > 0f)
                {
                    Label(new Rect(cx, y, kw, 20f), killer, 12, TextAnchor.MiddleLeft, SquadColor(entry.KillerSquad));
                    cx += kw;
                }

                if (mw > 0f)
                    Label(new Rect(cx, y, mw, 20f), middle, 12, TextAnchor.MiddleLeft, new Color(1f, 1f, 1f, 0.7f));
                cx += mw;
                Label(new Rect(cx, y, vwd, 20f), victim, 12, TextAnchor.MiddleLeft, SquadColor(entry.VictimSquad));
                y += 22f;
            }
        }

        private Color SquadColor(int squad)
        {
            if (squad < 0)
                return Color.white;
            return CombatTeams.ColorOf(match.Model.SideOf(squad));
        }

        private void DrawBanner()
        {
            if (!BannerActive)
                return;

            float age = Time.unscaledTime - bannerStart;
            float alpha = Mathf.Clamp01(Mathf.Min(age / 0.15f, (bannerLength - age) / 0.45f));
            Color color = new Color(bannerColor.r, bannerColor.g, bannerColor.b, alpha);
            float y = vh * 0.2f;
            Fill(new Rect(0f, y - 8f, vw, BannerSubtitle != null && BannerSubtitle.Length > 0 ? 92f : 66f), new Color(0f, 0f, 0f, 0.4f * alpha));
            Label(new Rect(0f, y, vw, 52f), BannerTitle, 44, TextAnchor.MiddleCenter, color);
            if (!string.IsNullOrEmpty(BannerSubtitle))
                Label(new Rect(8f, y + 52f, vw - 16f, 26f), BannerSubtitle, 15, TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, alpha));
        }

        private void DrawSpectatorBar()
        {
            TacticalSpectator spectator = match.Spectator;
            if (spectator == null || !spectator.IsSpectating)
                return;

            float w = Mathf.Min(vw - 16f, 340f);
            Rect rect = new Rect((vw - w) * 0.5f, vh - 74f, w, 52f);
            Fill(rect, Panel);
            Label(new Rect(rect.x, rect.y + 2f, rect.width, 20f), "SPECTATING  " + spectator.TargetName, 15, TextAnchor.MiddleCenter, Color.white);
            Fill(new Rect(rect.x + 12f, rect.y + 26f, rect.width - 24f, 6f), new Color(0f, 0f, 0f, 0.6f));
            Fill(new Rect(rect.x + 12f, rect.y + 26f, (rect.width - 24f) * spectator.TargetHealth01, 6f), Good);
            Label(new Rect(rect.x, rect.y + 33f, rect.width, 16f), "LEFT / RIGHT CLICK: SWITCH PLAYER", 10, TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, 0.7f), false);
        }

        #endregion

        #region Scoreboard

        private void DrawScoreboard()
        {
            TacticalMatchModel model = match.Model;
            float w = Mathf.Min(vw - 16f, 520f);
            int mine = TacticalMatch.PlayerSquad;
            int[] order = { mine, 1 - mine };

            float height = 36f;
            for (int s = 0; s < 2; s++)
                height += 26f + 18f + 22f * Mathf.Max(1, CountSquad(order[s])) + 8f;

            float x = (vw - w) * 0.5f;
            float y = Mathf.Max(8f, (vh - height) * 0.5f);
            Fill(new Rect(x, y, w, height), new Color(0.02f, 0.03f, 0.05f, 0.9f));
            Label(new Rect(x, y + 4f, w, 28f), "SCOREBOARD   ROUND " + model.RoundNumber, 16, TextAnchor.MiddleCenter, Color.white);

            float cy = y + 36f;
            for (int s = 0; s < 2; s++)
            {
                int squad = order[s];
                CombatTeam side = model.SideOf(squad);
                Color color = CombatTeams.ColorOf(side);
                Fill(new Rect(x + 6f, cy, w - 12f, 26f), Tint(color, 0.35f, 0.9f));
                string title = TacticalHudText.SquadName(squad) + "  " + TacticalHudText.SideName(side) + (squad == mine ? "  (YOU)" : string.Empty);
                Label(new Rect(x + 14f, cy, w - 90f, 26f), title, 14, TextAnchor.MiddleLeft, Color.white);
                Label(new Rect(x + w - 70f, cy, 56f, 26f), model.Score(squad).ToString(), 18, TextAnchor.MiddleRight, Color.white);
                cy += 26f;

                Color dim = new Color(1f, 1f, 1f, 0.55f);
                Label(new Rect(x + 14f, cy, 180f, 18f), "PLAYER", 10, TextAnchor.MiddleLeft, dim);
                Label(new Rect(x + w - 190f, cy, 46f, 18f), "K", 10, TextAnchor.MiddleRight, dim);
                Label(new Rect(x + w - 140f, cy, 46f, 18f), "D", 10, TextAnchor.MiddleRight, dim);
                Label(new Rect(x + w - 90f, cy, 76f, 18f), "STATUS", 10, TextAnchor.MiddleRight, dim);
                cy += 18f;

                match.Stats.CollectSquad(squad, rows);
                for (int i = 0; i < rows.Count; i++)
                {
                    TacticalParticipant row = rows[i];
                    bool alive = match.IsAlive(row);
                    Color text = alive ? Color.white : new Color(1f, 1f, 1f, 0.45f);
                    if (row.IsPlayer)
                        Fill(new Rect(x + 6f, cy, w - 12f, 22f), new Color(1f, 1f, 1f, 0.1f));
                    Label(new Rect(x + 14f, cy, 180f, 22f), row.Name, 13, TextAnchor.MiddleLeft, text);
                    Label(new Rect(x + w - 190f, cy, 46f, 22f), row.Kills.ToString(), 13, TextAnchor.MiddleRight, text);
                    Label(new Rect(x + w - 140f, cy, 46f, 22f), row.Deaths.ToString(), 13, TextAnchor.MiddleRight, text);
                    string status = !alive ? "DEAD" : match.CarriesBomb(row) ? "BOMB" : "ALIVE";
                    Color statusColor = !alive ? new Color(1f, 0.45f, 0.4f, 0.8f) : status == "BOMB" ? Gold : Good;
                    Label(new Rect(x + w - 90f, cy, 76f, 22f), status, 11, TextAnchor.MiddleRight, statusColor);
                    cy += 22f;
                }

                if (rows.Count == 0)
                    cy += 22f;
                cy += 8f;
            }
        }

        private int CountSquad(int squad)
        {
            match.Stats.CollectSquad(squad, rows);
            return rows.Count;
        }

        #endregion

        #region Class select

        private void DrawClassSelect()
        {
            ILoadoutMenuPage page = shownPage ?? LoadoutMenuPages.Primary;
            float top = TopY + BarHeight + 10f;
            float w = Mathf.Min(vw - 12f, 780f);
            float h = Mathf.Max(220f, vh - top - 12f);
            Rect panel = new Rect((vw - w) * 0.5f, top, w, h);
            Fill(panel, new Color(0.03f, 0.04f, 0.06f, 0.93f));
            GUI.Box(panel, GUIContent.none);

            float innerW = w - 28f;
            float innerH = h - 20f;
            GUILayout.BeginArea(new Rect(panel.x + 14f, panel.y + 10f, innerW, innerH));

            GUIStyle title = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            title.normal.textColor = Gold;
            GUIStyle hint = new GUIStyle(GUI.skin.label) { fontSize = 12, alignment = TextAnchor.MiddleCenter, wordWrap = true };
            hint.normal.textColor = new Color(0.85f, 0.88f, 0.92f);
            GUIStyle button = new GUIStyle(GUI.skin.button) { fontSize = 18, fontStyle = FontStyle.Bold };

            GUILayout.Label("CHOOSE YOUR CLASS", title, GUILayout.Height(28f));
            string role = "ROUND " + match.Model.RoundNumber + "  -  YOU " + (match.PlayerSide == CombatTeam.Attackers ? "ATTACK" : "DEFEND")
                + ". " + TacticalHudText.RoleHint(match.PlayerSide) + ".";
            GUILayout.Label(role + " Pick your loadout, then click READY or press Enter; the round starts when the timer ends.", hint);
            GUILayout.Space(4f);

            float footer = 52f;
            float contentH = Mathf.Max(100f, innerH - 28f - 34f - footer - 14f);
            GUILayout.BeginVertical(GUILayout.Height(contentH));
            if (page != null)
                page.DrawPage(innerW, contentH);
            else
                GUILayout.Label("No loadout menu is available in this scene.", hint);
            GUILayout.EndVertical();

            GUILayout.FlexibleSpace();
            string ready = "READY   " + TacticalHudText.Clock(match.Model.PhaseRemaining);
            if (GUILayout.Button(ready, button, GUILayout.Height(44f)))
                Ready();
            GUILayout.EndArea();
        }

        #endregion
    }
}
