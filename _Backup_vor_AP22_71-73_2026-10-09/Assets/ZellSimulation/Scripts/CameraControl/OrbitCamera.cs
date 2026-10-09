// =============================================================================
//  ZellSimulation – OrbitCamera.cs
//  Orbit-Kamera um die Simulations-Box (neues Input System, alle Tasten frei
//  belegbar – Einstellungen › Tastenbelegung):
//    • Rechte Maustaste ziehen  → drehen   (Tastatur: W/A/S/D, Pfeile · Gamepad: rechter Stick)
//    • Mausrad                  → zoomen   (Tastatur: Q/E, Bild↓/Bild↑ · Gamepad: Trigger)
//    • Mittlere Maustaste       → verschieben (Gamepad: linker Stick)
//    • R                        → Ansicht zurücksetzen (Gamepad: linker Stick drücken)
//    • Ohne Eingabe             → langsame automatische Rotation
//                                  (nicht während der Pause – das Bild steht still)
//  Fokus: Die Kamera kann einer Zelle folgen (Taste F / Doppelklick im Spielfeld).
//  Dann ist sie Drehpunkt, und man kann bis dicht an das Lebewesen heranzoomen,
//  um es bei Pause von allen Seiten zu betrachten.
//  Alle Bewegungen laufen mit ungeskalierter Zeit – auch bei Pause und Zeitraffer.
// =============================================================================
using UnityEngine;
using ZellSimulation.Cells;
using ZellSimulation.Settings;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace ZellSimulation.CameraControl
{
    [DisallowMultipleComponent]
    public sealed class OrbitCamera : MonoBehaviour
    {
        /// <summary>Zuletzt aktivierte Orbit-Kamera (für UI-Zugriffe).</summary>
        public static OrbitCamera Main { get; private set; }

        [Tooltip("Drehpunkt. Leer = Mittelpunkt der Simulations-Boundary.")]
        [SerializeField] Transform target;

        [Tooltip("Fester Drehpunkt, falls kein Target gesetzt und kein SimulationManager vorhanden ist.")]
        [SerializeField] Vector3 pivot = new Vector3(0f, 25f, 0f);

        [SerializeField, Range(2f, 300f)] float distance = 75f;
        [SerializeField, Range(2f, 50f)] float minDistance = 10f;
        [SerializeField, Range(10f, 500f)] float maxDistance = 200f;
        [SerializeField, Range(-89f, 89f)] float pitch = 25f;
        [SerializeField] float yaw = 30f;

        [Tooltip("Drehgeschwindigkeit (Grad pro Pixel).")]
        [SerializeField, Range(0.01f, 1f)] float rotateSpeed = 0.2f;
        [Tooltip("Zoomfaktor je Mausrad-Raste.")]
        [SerializeField, Range(0.01f, 0.5f)] float zoomSpeed = 0.1f;
        [Tooltip("Verschiebe-Geschwindigkeit.")]
        [SerializeField, Range(0.001f, 0.2f)] float panSpeed = 0.03f;
        [Tooltip("Automatische Rotation ohne Eingabe (Grad/s).")]
        [SerializeField, Range(0f, 30f)] float autoRotateSpeed = 3f;
        [Tooltip("Drehgeschwindigkeit per Tastatur/Gamepad (Grad/s).")]
        [SerializeField, Range(10f, 360f)] float keyRotateSpeed = 90f;
        [Tooltip("Verschiebe-Tempo per Gamepad-Stick (relativ zum Abstand).")]
        [SerializeField, Range(0.05f, 2f)] float stickPanSpeed = 0.5f;
        [Tooltip("Empfindlichkeit, Zoom, Auto-Rotation und Y-Invertierung aus den Benutzereinstellungen (Settings-Screen) übernehmen.")]
        [SerializeField] bool useUserSettings = true;

        [Tooltip("Vertikale Drehrichtung umkehren (wird bei 'Use User Settings' überschrieben).")]
        [SerializeField] bool invertY;

        [Tooltip("Glättung der Bewegung.")]
        [SerializeField, Range(1f, 30f)] float smoothing = 10f;

        [Header("Fokus auf eine Zelle")]
        [Tooltip("Startabstand beim Fokussieren als Vielfaches des Zellradius.")]
        [SerializeField, Range(2f, 40f)] float focusDistanceFactor = 9f;
        [Tooltip("Kleinster Abstand im Fokus-Modus (m).")]
        [SerializeField, Range(0.2f, 10f)] float focusMinDistance = 0.6f;

        Vector3 _pivotOffset;
        float _yaw, _pitch, _distance;
        Vector3 _center;
        bool _centerInit;

        // Fokus
        CellController _focus;
        int _focusUid;
        float _savedDistance;
        Vector3 _savedPivotOffset;

        float _startYaw, _startPitch, _startDistance;

        /// <summary>Fokussierte Zelle (null = Übersicht).</summary>
        public CellController FocusCell => IsFocusValid ? _focus : null;
        public bool IsFocused => IsFocusValid;
        bool IsFocusValid => _focus != null && _focus.IsAlive && _focus.Uid == _focusUid && _focus.isActiveAndEnabled;

        void OnEnable() => Main = this;

        /// <summary>Von der UI gesetzt: Mauszeiger über einem Panel (Mausrad scrollt dann das Panel statt zu zoomen).</summary>
        public static bool PointerOverUi { get; set; }

        void OnDisable()
        {
            if (Main == this) Main = null;
        }

        void Start()
        {
            var sim0 = Simulation.SimulationManager.Instance;
            if (target == null && sim0 != null)
                pivot = sim0.Environment.boundsCenter;
            if (sim0 != null && sim0.IsGlobe)
            {
                // Erdkugel: um den Planeten kreisen, nicht in ihn hineinzoomen
                pivot = sim0.Planet.Center;
                distance = sim0.Planet.Radius * 3.2f;
                maxDistance = Mathf.Max(maxDistance, sim0.Planet.Radius * 8f);
                pitch = 18f;
            }
            _yaw = yaw; _pitch = pitch; _distance = distance;
            _startYaw = yaw; _startPitch = pitch; _startDistance = distance;
        }

        // ------------------------------------------------------------ Fokus-API
        /// <summary>Kamera folgt der Zelle; sie wird zum Drehpunkt.</summary>
        public void Focus(CellController cell)
        {
            if (cell == null || !cell.IsAlive) return;
            if (!IsFocusValid)
            {
                _savedDistance = distance;
                _savedPivotOffset = _pivotOffset;
            }
            _focus = cell;
            _focusUid = cell.Uid;
            _pivotOffset = Vector3.zero;
            distance = Mathf.Clamp(Mathf.Max(0.3f, cell.WorldRadius) * focusDistanceFactor, focusMinDistance, maxDistance);
        }

        /// <summary>Zurück zur Übersicht (vorheriger Abstand und Verschiebung).</summary>
        public void ClearFocus()
        {
            if (_focus == null) return;
            _focus = null;
            _focusUid = 0;
            distance = _savedDistance > 0f ? _savedDistance : _startDistance;
            _pivotOffset = _savedPivotOffset;
        }

        /// <summary>Ansicht auf die Startwerte zurücksetzen (Fokus wird aufgehoben).</summary>
        public void ResetView()
        {
            _focus = null;
            _focusUid = 0;
            yaw = _startYaw;
            pitch = _startPitch;
            distance = _startDistance;
            _pivotOffset = Vector3.zero;
        }

        // ------------------------------------------------------------ Takt
        void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime; // funktioniert auch bei Pause/Zeitraffer
            bool userInput = false;

            if (useUserSettings)
            {
                ControlSettingsData c = UserSettings.Controls;
                rotateSpeed = c.rotateSpeed;
                zoomSpeed = c.zoomSpeed;
                panSpeed = c.panSpeed;
                autoRotateSpeed = c.autoRotateSpeed;
                invertY = c.invertY;
                keyRotateSpeed = c.keyRotateSpeed;
                stickPanSpeed = c.stickPanSpeed;
            }

            // Fokus-Zelle gestorben → zurück zur Übersicht
            if (_focus != null && !IsFocusValid) ClearFocus();
            bool focused = _focus != null;
            float minD = focused ? focusMinDistance : minDistance;
            var simG = Simulation.SimulationManager.Instance;
            if (!focused && simG != null && simG.IsGlobe) minD = Mathf.Max(minD, simG.Planet.Radius + 3f);

#if ENABLE_INPUT_SYSTEM
            Controls.ZsInput.Ensure();
            Mouse mouse = Mouse.current;
            if (mouse != null && !Controls.ZsInput.Blocked)
            {
                Vector2 delta = mouse.delta.ReadValue();
                if (Controls.ZsInput.Held(Controls.ZsInput.OrbitHold))
                {
                    yaw += delta.x * rotateSpeed;
                    pitch = Mathf.Clamp(pitch + (invertY ? delta.y : -delta.y) * rotateSpeed, -89f, 89f);
                    userInput = true;
                }
                if (Controls.ZsInput.Held(Controls.ZsInput.PanHold))
                {
                    _pivotOffset -= (transform.right * delta.x + transform.up * delta.y) * (panSpeed * distance * 0.05f);
                    userInput = true;
                }
                float scroll = PointerOverUi ? 0f : mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.01f)
                {
                    distance = Mathf.Clamp(distance * (1f - Mathf.Sign(scroll) * zoomSpeed), minD, maxDistance);
                    userInput = true;
                }
            }

            // Tastatur & Gamepad (frei belegbar, siehe Einstellungen › Tastenbelegung)
            Vector2 orbit = Controls.ZsInput.ReadVector(Controls.ZsInput.Orbit);
            float zoom = Controls.ZsInput.ReadAxis(Controls.ZsInput.Zoom);
            Vector2 pan = Controls.ZsInput.ReadVector(Controls.ZsInput.Pan);
            if (orbit.sqrMagnitude > 0.0004f)
            {
                yaw -= orbit.x * keyRotateSpeed * dt;
                pitch = Mathf.Clamp(pitch + (invertY ? -orbit.y : orbit.y) * keyRotateSpeed * 0.6f * dt, -89f, 89f);
                userInput = true;
            }
            if (Mathf.Abs(zoom) > 0.02f)
            {
                distance = Mathf.Clamp(distance * (1f - zoom * 1.5f * dt), minD, maxDistance);
                userInput = true;
            }
            if (pan.sqrMagnitude > 0.0004f)
            {
                _pivotOffset += (transform.right * pan.x + transform.up * pan.y) * (stickPanSpeed * distance * dt);
                userInput = true;
            }
            if (Controls.ZsInput.Pressed(Controls.ZsInput.ResetView)) { ResetView(); userInput = true; }
#endif
            distance = Mathf.Clamp(distance, minD, maxDistance);

            // Automatische Rotation nur, wenn die Simulation läuft und nichts fokussiert ist
            var sim = Simulation.SimulationManager.Instance;
            bool paused = sim != null && sim.Paused;
            if (!userInput && !paused && !focused) yaw += autoRotateSpeed * dt;

            float k = 1f - Mathf.Exp(-smoothing * dt);
            _yaw = Mathf.LerpAngle(_yaw, yaw, k);
            _pitch = Mathf.Lerp(_pitch, pitch, k);
            _distance = Mathf.Lerp(_distance, distance, k);

            Vector3 desiredCenter = (focused ? _focus.transform.position : (target != null ? target.position : pivot)) + _pivotOffset;
            if (!_centerInit) { _center = desiredCenter; _centerInit = true; }
            _center = Vector3.Lerp(_center, desiredCenter, focused ? Mathf.Max(k, 1f - Mathf.Exp(-20f * dt)) : k);

            Quaternion rot = Quaternion.Euler(_pitch, _yaw, 0f);
            transform.SetPositionAndRotation(_center - rot * Vector3.forward * _distance, rot);
        }
    }
}
