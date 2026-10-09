// -----------------------------------------------------------------------------
// ZellSimulation – 3D-Zell-Evolution mit Immunsystem
// Copyright © 2026 Michael Bogen. Alle Rechte vorbehalten.
// Datei: RuntimeTooltip.cs
// -----------------------------------------------------------------------------
using UnityEngine;
using UnityEngine.UIElements;
using ZellSimulation.Settings;

namespace ZellSimulation.UI
{
    /// <summary>
    /// Tooltips für UI Toolkit zur Laufzeit. Die eingebaute Eigenschaft
    /// <c>VisualElement.tooltip</c> wird im Spiel nicht angezeigt (nur in Editor-Fenstern) –
    /// diese Klasse übernimmt das: Sie beobachtet den Mauszeiger im gesamten Panel,
    /// sucht das nächste Element (oder Elternelement) mit gesetztem <c>tooltip</c>
    /// und blendet nach der eingestellten Verzögerung eine Erläuterung ein.
    /// Zusätzlich kann Text manuell angezeigt werden (z. B. Infos zu Zellen im Spielfeld).
    /// </summary>
    public sealed class RuntimeTooltip
    {
        readonly VisualElement _root;
        readonly VisualElement _box;
        readonly Label _title;
        readonly Label _text;

        VisualElement _hoverElement;
        string _pendingText;
        float _hoverSince;
        Vector2 _pointer;
        bool _manual;
        IVisualElementScheduledItem _tick;

        /// <summary>True, wenn sich der Zeiger über einem Bedienelement (nicht dem leeren Hintergrund) befindet.</summary>
        public bool PointerOverUi { get; private set; }

        public RuntimeTooltip(VisualElement root)
        {
            _root = root;

            _box = new VisualElement { name = "zs-tooltip" };
            _box.pickingMode = PickingMode.Ignore;
            _box.style.position = Position.Absolute;
            _box.style.maxWidth = 380;
            _box.style.paddingLeft = 12;
            _box.style.paddingRight = 12;
            _box.style.paddingTop = 8;
            _box.style.paddingBottom = 9;
            _box.style.backgroundColor = new Color(0.06f, 0.07f, 0.10f, 0.97f);
            ZsUiKit.SetRadius(_box, 7f);
            ZsUiKit.SetBorder(_box, 1f, ZsUiKit.Gold);
            _box.style.display = DisplayStyle.None;

            _title = ZsUiKit.MakeText("", 13, ZsUiKit.GoldLight, true);
            _title.pickingMode = PickingMode.Ignore;
            _title.style.marginBottom = 3;
            _text = ZsUiKit.MakeText("", 13, ZsUiKit.Text);
            _text.pickingMode = PickingMode.Ignore;
            _box.Add(_title);
            _box.Add(_text);
            root.Add(_box);

            root.RegisterCallback<PointerMoveEvent>(OnPointerMove, TrickleDown.TrickleDown);
            root.RegisterCallback<PointerLeaveEvent>(_ => { PointerOverUi = false; HideHover(); });
            root.RegisterCallback<PointerDownEvent>(_ => HideHover(), TrickleDown.TrickleDown);

            _tick = root.schedule.Execute(Tick).Every(50);
        }

        /// <summary>Tooltip-Ebene nach vorn holen (nach dem Hinzufügen neuer Overlays aufrufen).</summary>
        public void BringToFront() => _box.BringToFront();

        // =================================================================
        void OnPointerMove(PointerMoveEvent evt)
        {
            _pointer = _root.WorldToLocal(evt.position);
            var target = evt.target as VisualElement;
            PointerOverUi = target != null && target != _root;

            VisualElement withTip = FindTooltipElement(target);
            if (withTip != _hoverElement)
            {
                _hoverElement = withTip;
                _pendingText = withTip != null ? withTip.tooltip : null;
                _hoverSince = Time.unscaledTime;
                if (!_manual) _box.style.display = DisplayStyle.None;
            }
            else if (_box.resolvedStyle.display == DisplayStyle.Flex && !_manual)
            {
                PlaceAt(_pointer);
            }
        }

        VisualElement FindTooltipElement(VisualElement e)
        {
            while (e != null && e != _root)
            {
                if (!string.IsNullOrEmpty(e.tooltip)) return e;
                e = e.parent;
            }
            return null;
        }

        void Tick()
        {
            if (_manual) return;
            ControlSettingsData c = UserSettings.Controls;
            if (!c.tooltipsEnabled || _hoverElement == null || string.IsNullOrEmpty(_pendingText))
            {
                if (_box.resolvedStyle.display == DisplayStyle.Flex) _box.style.display = DisplayStyle.None;
                return;
            }
            if (_box.resolvedStyle.display == DisplayStyle.None && Time.unscaledTime - _hoverSince >= c.tooltipDelay)
                Show(null, _pendingText, _pointer, false);
        }

        void HideHover()
        {
            _hoverElement = null;
            _pendingText = null;
            if (!_manual) _box.style.display = DisplayStyle.None;
        }

        // =================================================================
        //  Manuelle Anzeige (z. B. Spielfeld-Infos)
        // =================================================================
        /// <summary>Zeigt einen Tooltip an einer Panel-Position (überschreibt Hover-Tooltips).</summary>
        public void ShowManual(string title, string text, Vector2 panelPosition)
        {
            Show(title, text, panelPosition, true);
        }

        public void HideManual()
        {
            if (!_manual) return;
            _manual = false;
            _box.style.display = DisplayStyle.None;
        }

        void Show(string title, string text, Vector2 pos, bool manual)
        {
            _manual = manual;

            // Erste Zeile vor „ – “ oder „: “ nicht als Titel interpretieren – Titel nur explizit.
            bool hasTitle = !string.IsNullOrEmpty(title);
            _title.style.display = hasTitle ? DisplayStyle.Flex : DisplayStyle.None;
            if (hasTitle) _title.text = title;
            _text.text = text;
            _box.style.display = DisplayStyle.Flex;
            _box.BringToFront();
            PlaceAt(pos);
        }

        void PlaceAt(Vector2 p)
        {
            float w = _root.resolvedStyle.width;
            float h = _root.resolvedStyle.height;
            const float offset = 18f;

            if (w > 0f && p.x > w * 0.62f)
            {
                _box.style.left = StyleKeyword.Auto;
                _box.style.right = Mathf.Max(4f, w - p.x + offset * 0.5f);
            }
            else
            {
                _box.style.right = StyleKeyword.Auto;
                _box.style.left = p.x + offset;
            }

            if (h > 0f && p.y > h * 0.68f)
            {
                _box.style.top = StyleKeyword.Auto;
                _box.style.bottom = Mathf.Max(4f, h - p.y + offset * 0.5f);
            }
            else
            {
                _box.style.bottom = StyleKeyword.Auto;
                _box.style.top = p.y + offset;
            }
        }
    }
}
