// -----------------------------------------------------------------------------
// ZellSimulation – 3D-Zell-Evolution mit Immunsystem
// Copyright © 2026 Michael Bogen. Alle Rechte vorbehalten.
// Datei: FpsOverlay.cs
// -----------------------------------------------------------------------------
//  FPS-Anzeige unten rechts im Spielfeld (Einstellungen › Grafik & Anzeige ›
//  FPS-Anzeige, Umschalten mit F3). Zeigt Bildrate und Bildzeit (Mittel der
//  letzten halben Sekunde), auf Wunsch mit Details: min./max. Bildrate der
//  letzten Sekunde, Physik-Schritte pro Bild, Zellen, Viren, Zeitraffer und
//  Landschafts-Qualität. Die Bewertung steht als Wort dabei (nicht nur Farbe).
// -----------------------------------------------------------------------------
using System.Text;
using UnityEngine;
using UnityEngine.UIElements;
using ZellSimulation.Settings;
using ZellSimulation.Simulation;

namespace ZellSimulation.UI
{
    public sealed class FpsOverlay : VisualElement
    {
        readonly Label _main, _details;
        readonly StringBuilder _sb = new StringBuilder(160);
        SimulationManager _manager;
        float _acc, _timer;
        int _frames;
        float _windowMin = float.MaxValue, _windowMax, _windowTime;
        float _shownMin, _shownMax;

        static readonly string[] s_Quality = { "aus", "niedrig", "mittel", "hoch" };

        public FpsOverlay()
        {
            style.position = Position.Absolute;
            style.right = 12;
            style.bottom = 12;
            style.paddingLeft = 10;
            style.paddingRight = 10;
            style.paddingTop = 5;
            style.paddingBottom = 6;
            style.backgroundColor = ZsUiKit.PanelBg(0.86f);
            ZsUiKit.SetRadius(this, 6f);
            ZsUiKit.SetBorder(this, 1f, ZsUiKit.GoldDark);
            pickingMode = PickingMode.Ignore;
            style.display = DisplayStyle.None;
            tooltip = "Bildrate (FPS) und Bildzeit. Umschalten mit F3, dauerhaft unter Einstellungen › Grafik & Anzeige.";

            _main = ZsUiKit.MakeText("– FPS", 15, ZsUiKit.Text, true, TextAnchor.MiddleRight);
            _main.style.whiteSpace = WhiteSpace.NoWrap;
            _main.pickingMode = PickingMode.Ignore;
            Add(_main);
            _details = ZsUiKit.MakeText("", 11, ZsUiKit.TextMuted, false, TextAnchor.MiddleRight);
            _details.style.whiteSpace = WhiteSpace.Normal;
            _details.pickingMode = PickingMode.Ignore;
            Add(_details);
        }

        public void Bind(SimulationManager m) => _manager = m;

        /// <summary>Pro Frame mit der ungeskalierten Bildzeit aufrufen.</summary>
        public void Tick(float unscaledDt)
        {
            GraphicsSettingsData g = UserSettings.Graphics;
            bool show = g.showFps;
            DisplayStyle want = show ? DisplayStyle.Flex : DisplayStyle.None;
            if (style.display != want) style.display = want;
            if (!show || unscaledDt <= 0f) return;

            _acc += unscaledDt;
            _frames++;
            float fpsNow = 1f / unscaledDt;
            _windowMin = Mathf.Min(_windowMin, fpsNow);
            _windowMax = Mathf.Max(_windowMax, fpsNow);
            _windowTime += unscaledDt;
            if (_windowTime >= 1f)
            {
                _shownMin = _windowMin;
                _shownMax = _windowMax;
                _windowMin = float.MaxValue;
                _windowMax = 0f;
                _windowTime = 0f;
            }

            _timer -= unscaledDt;
            if (_timer > 0f) return;
            _timer = 0.5f;

            float avgDt = _frames > 0 ? _acc / _frames : unscaledDt;
            float fps = avgDt > 0f ? 1f / avgDt : 0f;
            _acc = 0f;
            _frames = 0;

            string verdict;
            Color col;
            if (fps >= 50f) { verdict = "flüssig"; col = new Color(0.45f, 0.9f, 0.45f); }
            else if (fps >= 28f) { verdict = "spielbar"; col = new Color(1f, 0.82f, 0.3f); }
            else { verdict = "ruckelt"; col = ZsUiKit.Danger; }
            _main.text = fps.ToString("0") + " FPS · " + (avgDt * 1000f).ToString("0.0") + " ms · " + verdict;
            _main.style.color = col;

            if (!g.fpsDetails || _manager == null)
            {
                _details.style.display = DisplayStyle.None;
                return;
            }
            _details.style.display = DisplayStyle.Flex;
            _sb.Clear();
            if (_shownMax > 0f)
                _sb.Append("min ").Append(_shownMin.ToString("0")).Append(" / max ").Append(_shownMax.ToString("0")).Append(" FPS (1 s)\n");
            _sb.Append("Physik: ").Append(_manager.PhysicsStepsLastFrame).Append(" Schritte/Bild à ")
               .Append((Time.fixedDeltaTime * 1000f).ToString("0")).Append(" ms\n");
            _sb.Append("Zellen ").Append(_manager.Cells.Count).Append(" · Viren ").Append(_manager.VirusSystem != null ? _manager.VirusSystem.Count : 0)
               .Append(" · Zeitraffer ").Append(_manager.Paused ? "Pause" : _manager.TimeScale.ToString("0") + "×");
            if (!_manager.Paused && _manager.EffectiveTimeScale < _manager.TimeScale * 0.95f)
                _sb.Append(" (gedrosselt auf ").Append(_manager.EffectiveTimeScale.ToString("0.0")).Append("×)");
            _sb.Append('\n');
            _sb.Append("Landschaft: ").Append(s_Quality[Mathf.Clamp(g.environmentQuality, 0, 3)]);
            _details.text = _sb.ToString();
        }
    }
}
