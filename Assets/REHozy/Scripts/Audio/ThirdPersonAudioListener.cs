using Unity.Cinemachine;
using UnityEngine;

namespace REHozy.Audio
{
    /// <summary>
    /// Keeps a single AudioListener at the gameplay focal point (Cinemachine tracking target),
    /// not on the distant orbit camera, so 3D SFX stay audible in third-person.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    [DisallowMultipleComponent]
    [AddComponentMenu("REHozy/Audio/Third Person Audio Listener")]
    public sealed class ThirdPersonAudioListener : MonoBehaviour
    {
        [SerializeField] private Transform listenAnchor;
        [SerializeField] private bool autoFindListenAnchor = true;

        AudioListener _listener;

        void Awake()
        {
            EnsureListenerAtAnchor();
        }

        void EnsureListenerAtAnchor()
        {
            var anchor = ResolveAnchor();
            if (anchor == null)
            {
                Debug.LogWarning(
                    "[ThirdPersonAudioListener] Listen anchor not found. "
                    + "Assign Tracking Target manually or keep AudioListener on Main Camera.");
                return;
            }

            _listener = anchor.GetComponent<AudioListener>();
            if (_listener == null)
            {
                _listener = anchor.gameObject.AddComponent<AudioListener>();
            }

            _listener.enabled = true;

            var listeners = FindObjectsByType<AudioListener>(FindObjectsSortMode.None);
            for (var i = 0; i < listeners.Length; i++)
            {
                if (listeners[i] != _listener)
                {
                    listeners[i].enabled = false;
                }
            }
        }

        Transform ResolveAnchor()
        {
            if (listenAnchor != null)
            {
                return listenAnchor;
            }

            if (!autoFindListenAnchor)
            {
                return null;
            }

            var vcams = FindObjectsByType<CinemachineCamera>(FindObjectsSortMode.None);
            for (var i = 0; i < vcams.Length; i++)
            {
                var trackingTarget = vcams[i].Target.TrackingTarget;
                if (trackingTarget != null)
                {
                    return trackingTarget;
                }
            }

            return null;
        }
    }
}
