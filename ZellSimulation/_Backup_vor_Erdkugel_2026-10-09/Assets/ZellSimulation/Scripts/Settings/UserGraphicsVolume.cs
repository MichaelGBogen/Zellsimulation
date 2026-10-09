// -----------------------------------------------------------------------------
// ZellSimulation – 3D-Zell-Evolution mit Immunsystem
// Copyright © 2026 Michael Bogen. Alle Rechte vorbehalten.
// Datei: UserGraphicsVolume.cs
// -----------------------------------------------------------------------------
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace ZellSimulation.Settings
{
    /// <summary>
    /// Globales HDRP-Volume mit den Grafik-Overrides des Benutzers (Bloom, Vignette,
    /// Motion Blur, Belichtung, SSAO, Schattendistanz).
    ///
    /// Jede Szene bekommt eine eigene Instanz mit eigenem Laufzeit-Profil. Profil und
    /// Komponenten werden zusammen mit dem GameObject zerstört. Das verhindert, dass ein
    /// über Szenen hinweg geteiltes Profil zerstörte Komponenten enthält, was in
    /// HDRP zu einer NullReferenceException im VolumeManager führen würde.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("")] // nur zur Laufzeit erzeugt
    public sealed class UserGraphicsVolume : MonoBehaviour
    {
        const string ObjectName = "ZS_UserGraphicsVolume";

        public static UserGraphicsVolume Current { get; private set; }

        public VolumeProfile Profile { get; private set; }

        Volume _volume;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Current = null;

        /// <summary>Liefert das Volume der aktuellen Szene (legt es bei Bedarf an).</summary>
        public static UserGraphicsVolume Ensure()
        {
            if (Current != null && Current.IsValid) return Current;
            if (Current != null) Destroy(Current.gameObject); // beschädigt → neu anlegen
            return new GameObject(ObjectName).AddComponent<UserGraphicsVolume>();
        }

        /// <summary>Profil und alle Komponenten sind noch vorhanden.</summary>
        public bool IsValid
        {
            get
            {
                if (Profile == null || _volume == null) return false;
                var list = Profile.components;
                for (int i = 0; i < list.Count; i++)
                    if (list[i] == null) return false;
                return true;
            }
        }

        void Awake()
        {
            Current = this;

            Profile = ScriptableObject.CreateInstance<VolumeProfile>();
            Profile.name = "ZS_UserGraphicsProfile";
            Profile.hideFlags = HideFlags.DontSave;
            Profile.Add<Bloom>(false);
            Profile.Add<Vignette>(false);
            Profile.Add<MotionBlur>(false);
            Profile.Add<Exposure>(false);
            Profile.Add<ScreenSpaceAmbientOcclusion>(false);
            Profile.Add<HDShadowSettings>(false);

            // Komponenten vor dem automatischen Entladen (UnloadUnusedAssets) schützen –
            // sie werden in OnDestroy selbst aufgeräumt.
            foreach (VolumeComponent c in Profile.components)
                if (c != null) c.hideFlags |= HideFlags.DontSave;

            _volume = gameObject.AddComponent<Volume>();
            _volume.isGlobal = true;
            _volume.priority = 1000f;
            _volume.sharedProfile = Profile;
        }

        void OnDestroy()
        {
            if (Current == this) Current = null;
            if (Profile == null) return;

            foreach (VolumeComponent c in Profile.components)
                if (c != null) Destroy(c);
            Destroy(Profile);
            Profile = null;
        }
    }
}
