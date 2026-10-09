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
//  Erdkugel: Zoom wirkt auf die Höhe über der Oberfläche – mit Detailkarte bis
//  auf wenige hundert Meter (Straßen erkennbar). Drehen, Verschieben und
//  Auto-Rotation werden nah am Boden entsprechend feiner; Winkel werden in
//  doppelter Genauigkeit geführt, damit das Bild auch dann nicht ruckelt.
//  Alle Bewegungen laufen mit ungeskalierter Zeit – auch bei Pause und Zeitraffer.
// =============================================================================
using System;
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
        // Winkel in doppelter Genauigkeit (nah an der Erdoberfläche entsprechen 0,00001° wenigen Metern)
        double _tYaw, _tPitch, _yaw, _pitch;
        float _distance;
        Camera _cam;
        float _defaultNear = -1f;
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
            _tYaw = _yaw = yaw; _tPitch = _pitch = pitch; _distance = distance;
            _startYaw = yaw; _startPitch = pitch; _startDistance = distance;
            _cam = GetComponent<Camera>();
            if (_cam != null) _defaultNear = _cam.nearClipPlane;
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
            _tYaw = _startYaw;
            _tPitch = _startPitch;
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
            bool globe = !focused && simG != null && simG.IsGlobe;
            float surfR = 0f, minAlt = 0f, fine = 1f;
            if (globe)
            {
                // Höhe über der tatsächlich gezeichneten Oberfläche unter der Kamera
                float planetR = simG.Planet.Radius;
                Vector3 rel = transform.position - simG.Planet.Center;
                Vector3 dir = rel.sqrMagnitude > 1e-6f ? rel.normalized : Vector3.up;
                var env = World.PlanetEnvironment.Active;
                surfR = env != null ? env.MeshRadius(dir) : planetR;
                minAlt = UserSettings.Graphics.detailMap ? World.PlanetDetailMap.MinCameraAltitude(planetR) : 3f;
                minD = surfR + minAlt;
                // Nah am Boden fein steuern: Drehwinkel ∝ Höhe
                fine = Mathf.Clamp((distance - surfR) / (planetR * 2.2f), 1e-6f, 1f);
            }

#if ENABLE_INPUT_SYSTEM
            Controls.ZsInput.Ensure();
            Mouse mouse = Mouse.current;
            if (mouse != null && !Controls.ZsInput.Blocked)
            {
                Vector2 delta = mouse.delta.ReadValue();
                if (Controls.ZsInput.Held(Controls.ZsInput.OrbitHold))
                {
                    _tYaw += (double)delta.x * rotateSpeed * fine;
                    _tPitch = Clamp(_tPitch + (invertY ? delta.y : -delta.y) * (double)rotateSpeed * fine, -89.0, 89.0);
                    userInput = true;
                }
                if (Controls.ZsInput.Held(Controls.ZsInput.PanHold))
                {
                    float panRef = globe ? Mathf.Max(minAlt, distance - surfR) : distance;
                    _pivotOffset -= (transform.right * delta.x + transform.up * delta.y) * (panSpeed * panRef * 0.05f);
                    userInput = true;
                }
                float scroll = PointerOverUi ? 0f : mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.01f)
                {
                    if (globe)
                    {
                        // Erdkugel: Höhe über der Oberfläche skalieren (doppelte Schrittweite – sonst dauert der Weg bis zur Straße zu lang)
                        float alt = Mathf.Max(minAlt, distance - surfR) * (1f - Mathf.Sign(scroll) * Mathf.Min(0.6f, zoomSpeed * 2f));
                        distance = Mathf.Clamp(surfR + alt, minD, maxDistance);
                    }
                    else distance = Mathf.Clamp(distance * (1f - Mathf.Sign(scroll) * zoomSpeed), minD, maxDistance);
                    userInput = true;
                }
            }

            // Tastatur & Gamepad (frei belegbar, siehe Einstellungen › Tastenbelegung)
            Vector2 orbit = Controls.ZsInput.ReadVector(Controls.ZsInput.Orbit);
            float zoom = Controls.ZsInput.ReadAxis(Controls.ZsInput.Zoom);
            Vector2 pan = Controls.ZsInput.ReadVector(Controls.ZsInput.Pan);
            if (orbit.sqrMagnitude > 0.0004f)
            {
                _tYaw -= (double)orbit.x * keyRotateSpeed * dt * fine;
                _tPitch = Clamp(_tPitch + (invertY ? -orbit.y : orbit.y) * (double)keyRotateSpeed * 0.6 * dt * fine, -89.0, 89.0);
                userInput = true;
            }
            if (Mathf.Abs(zoom) > 0.02f)
            {
                if (globe)
                {
                    float alt = Mathf.Max(minAlt, distance - surfR) * (1f - zoom * 3f * dt);
                    distance = Mathf.Clamp(surfR + alt, minD, maxDistance);
                }
                else distance = Mathf.Clamp(distance * (1f - zoom * 1.5f * dt), minD, maxDistance);
                userInput = true;
            }
            if (pan.sqrMagnitude > 0.0004f)
            {
                float panRef = globe ? Mathf.Max(minAlt, distance - surfR) : distance;
                _pivotOffset += (transform.right * pan.x + transform.up * pan.y) * (stickPanSpeed * panRef * dt);
                userInput = true;
            }
            if (Controls.ZsInput.Pressed(Controls.ZsInput.ResetView)) { ResetView(); userInput = true; }
#endif
            distance = Mathf.Clamp(distance, minD, maxDistance);

            // Automatische Rotation nur, wenn die Simulation läuft und nichts fokussiert ist
            var sim = Simulation.SimulationManager.Instance;
            bool paused = sim != null && sim.Paused;
            if (!userInput && !paused && !focused) _tYaw += (double)autoRotateSpeed * dt * fine;

            // Winkel klein halten (Genauigkeit)
            if (_tYaw > 360.0 || _tYaw < -360.0)
            {
                double wrap = Math.Floor(_tYaw / 360.0) * 360.0;
                _tYaw -= wrap;
                _yaw -= wrap;
            }
            yaw = (float)_tYaw;
            pitch = (float)_tPitch;

            float k = 1f - Mathf.Exp(-smoothing * dt);
            _yaw += (_tYaw - _yaw) * k;
            _pitch += (_tPitch - _pitch) * k;
            _distance = Mathf.Lerp(_distance, distance, k);
            if (globe && Mathf.Abs(_distance - distance) < minAlt * 0.05f) _distance = distance;

            Vector3 desiredCenter = (focused ? _focus.transform.position : (target != null ? target.position : pivot)) + _pivotOffset;
            if (!_centerInit) { _center = desiredCenter; _centerInit = true; }
            _center = Vector3.Lerp(_center, desiredCenter, focused ? Mathf.Max(k, 1f - Mathf.Exp(-20f * dt)) : k);

            // Blickrichtung in doppelter Genauigkeit (entspricht Quaternion.Euler(pitch, yaw, 0) · vorwärts)
            double p = _pitch * (Math.PI / 180.0), y = _yaw * (Math.PI / 180.0);
            var fwd = new Vector3((float)(Math.Cos(p) * Math.Sin(y)), (float)(-Math.Sin(p)), (float)(Math.Cos(p) * Math.Cos(y)));
            Quaternion rot = Quaternion.LookRotation(fwd, Vector3.up);
            transform.SetPositionAndRotation(_center - fwd * _distance, rot);

            // Nahe Clipping-Ebene an die Höhe anpassen (sonst schneidet sie die Oberfläche ab)
            if (_cam != null && _defaultNear > 0f)
            {
                float near = _defaultNear;
                if (globe) near = Mathf.Clamp((_distance - surfR) * 0.3f, 0.0005f, _defaultNear);
                if (!Mathf.Approximately(_cam.nearClipPlane, near)) _cam.nearClipPlane = near;
            }
        }

        static double Clamp(double v, double min, double max) => v < min ? min : v > max ? max : v;
    }
}
