using System;
using System.Collections;
using REHozy.CarryableTools;
using UnityEngine;

[DisallowMultipleComponent]
[AddComponentMenu("REHozy/Quests/Quest World Scale Transition")]
public sealed class QuestWorldScaleTransition : MonoBehaviour
{
    [SerializeField] private float duration = 0.35f;

    private Vector3 _targetScale;
    private Coroutine _routine;
    private Collider[] _colliders;
    private bool[] _colliderWasEnabled;

    public bool IsPlaying => _routine != null;

    private void Awake()
    {
        _targetScale = transform.localScale;
        CacheColliders();
    }

    public void ApplyInstantShown()
    {
        StopRoutine();
        gameObject.SetActive(true);
        transform.localScale = _targetScale;
        SetCollidersEnabled(true);
    }

    public void ApplyInstantHidden()
    {
        StopRoutine();
        PrepareToolForHide();
        transform.localScale = Vector3.zero;
        SetCollidersEnabled(false);
        gameObject.SetActive(false);
    }

    public void PlayShow(Action onComplete = null)
    {
        StopRoutine();
        gameObject.SetActive(true);
        SetCollidersEnabled(false);
        transform.localScale = Vector3.zero;
        _routine = StartCoroutine(AnimateScale(Vector3.zero, _targetScale, () =>
        {
            SetCollidersEnabled(true);
            _routine = null;
            onComplete?.Invoke();
        }));
    }

    public void PlayHide(Action onComplete = null)
    {
        StopRoutine();
        PrepareToolForHide();
        SetCollidersEnabled(false);
        _routine = StartCoroutine(AnimateScale(transform.localScale, Vector3.zero, () =>
        {
            transform.localScale = Vector3.zero;
            gameObject.SetActive(false);
            _routine = null;
            onComplete?.Invoke();
        }));
    }

    private IEnumerator AnimateScale(Vector3 from, Vector3 to, Action onComplete)
    {
        var t = 0f;
        var dur = Mathf.Max(duration, 0.01f);

        while (t < 1f)
        {
            t += Time.deltaTime / dur;
            var eased = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
            transform.localScale = Vector3.Lerp(from, to, eased);
            yield return null;
        }

        transform.localScale = to;
        onComplete?.Invoke();
    }

    private void PrepareToolForHide()
    {
        var core = GetComponent<CarryableToolCore>();
        if (core != null && core.State != CarryableToolState.OnGround)
        {
            core.SnapToHomeGround();
        }
    }

    private void CacheColliders()
    {
        _colliders = GetComponentsInChildren<Collider>(true);
        _colliderWasEnabled = new bool[_colliders.Length];
        for (var i = 0; i < _colliders.Length; i++)
        {
            _colliderWasEnabled[i] = _colliders[i] != null && _colliders[i].enabled;
        }
    }

    private void SetCollidersEnabled(bool enabled)
    {
        if (_colliders == null || _colliders.Length == 0)
        {
            CacheColliders();
        }

        for (var i = 0; i < _colliders.Length; i++)
        {
            if (_colliders[i] == null)
            {
                continue;
            }

            if (enabled && _colliderWasEnabled != null && i < _colliderWasEnabled.Length)
            {
                _colliders[i].enabled = _colliderWasEnabled[i];
            }
            else
            {
                _colliders[i].enabled = enabled;
            }
        }
    }

    private void StopRoutine()
    {
        if (_routine != null)
        {
            StopCoroutine(_routine);
            _routine = null;
        }
    }
}
