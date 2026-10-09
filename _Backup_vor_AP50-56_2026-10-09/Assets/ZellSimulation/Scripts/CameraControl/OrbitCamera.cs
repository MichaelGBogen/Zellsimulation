// =============================================================================
//  ZellSimulation – OrbitCamera.cs
//  Einfache Orbit-Kamera um die Simulations-Box (neues Input System):
//    • Rechte Maustaste ziehen  → drehen
//    • Mausrad                  → zoomen
//    • Mittlere Maustaste       → verschieben
//    • Ohne Eingabe             → langsame automatische Rotation
// =============================================================================
using UnityEngine;
using ZellSimulation.Settings;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace ZellSimulation.CameraControl
{
    [DisallowMultipleComponent]
    public sealed class OrbitCamera : MonoBehaviour
    {
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
        [Tooltip("Empfindlichkeit, Zoom, Auto-Rotation und Y-Invertierung aus den Benutzereinstellungen (Settings-Screen) übernehmen.")]
        [SerializeField] bool useUserSettings = true;

        [Tooltip("Vertikale Drehrichtung umkehren (wird bei 'Use User Settings' überschrieben).")]
        [SerializeField] bool invertY;

        [Tooltip("Glättung der Bewegung.")]
        [SerializeField, Range(1f, 30f)] float smoothing = 10f;

        Vector3 _pivotOffset;
        float _yaw, _pitch, _distance;

        void Start()
        {
            if (target == null && Simulation.SimulationManager.Instance != null)
                pivot = Simulation.SimulationManager.Instance.Environment.boundsCenter;
            _yaw = yaw; _pitch = pitch; _distance = distance;
        }

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
            }

#if ENABLE_INPUT_SYSTEM
            Mouse mouse = Mouse.current;
            if (mouse != null)
            {
                Vector2 delta = mouse.delta.ReadValue();
                if (mouse.rightButton.isPressed)
                {
                    yaw += delta.x * rotateSpeed;
                    pitch = Mathf.Clamp(pitch + (invertY ? delta.y : -delta.y) * rotateSpeed, -89f, 89f);
                    userInput = true;
                }
                if (mouse.middleButton.isPressed)
                {
                    _pivotOffset -= (transform.right * delta.x + transform.up * delta.y) * (panSpeed * distance * 0.05f);
                    userInput = true;
                }
                float scroll = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.01f)
                {
                    distance = Mathf.Clamp(distance * (1f - Mathf.Sign(scroll) * zoomSpeed), minDistance, maxDistance);
                    userInput = true;
                }
            }
#endif
            if (!userInput) yaw += autoRotateSpeed * dt;

            float k = 1f - Mathf.Exp(-smoothing * dt);
            _yaw = Mathf.LerpAngle(_yaw, yaw, k);
            _pitch = Mathf.Lerp(_pitch, pitch, k);
            _distance = Mathf.Lerp(_distance, distance, k);

            Vector3 center = (target != null ? target.position : pivot) + _pivotOffset;
            Quaternion rot = Quaternion.Euler(_pitch, _yaw, 0f);
            transform.SetPositionAndRotation(center - rot * Vector3.forward * _distance, rot);
        }
    }
}
