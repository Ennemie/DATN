using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// NEW FEATURE: Reusable UI Death/Failure Spread Effect.
/// 
/// Purpose:
/// - Drives the UI/DeathSpread shader through material property "_Spread".
/// - 0 -> 1 during spread-in.
/// - Holds fully black.
/// - 1 -> 0 during spread-out.
/// - Toggles object groups only when each phase starts.
/// - Calls a gameplay callback while the screen is fully covered, so Player can
///   restore checkpoint state invisibly behind the black screen.
///
/// References:
/// - PlayerProperties
/// - CheckpointManager
/// - UI Image using UI/DeathSpread material
///
/// The script intentionally uses a runtime material instance so changing
/// _Spread does not modify the shared material asset in the Project window.
/// </summary>
public class DeathSpreadEffectController : MonoBehaviour
{
    [Header("Effect Root")]
    [Tooltip("Canvas/GameObject that contains the death Image. This object may start inactive.")]
    [SerializeField] private GameObject effectRoot;

    [Tooltip("UI Image using the UI/DeathSpread shader.")]
    [SerializeField] private Image effectImage;

    [Header("Timing")]
    [Min(0f)]
    [SerializeField] private float spreadInDuration = 1.5f;

    [Min(0f)]
    [SerializeField] private float holdDuration = 2f;

    [Tooltip("Defaults to the same duration as Spread In.")]
    [Min(0f)]
    [SerializeField] private float spreadOutDuration = 1.5f;

    [Header("Spread")]
    [Range(0f, 1f)]
    [SerializeField] private float initialSpread = 0f;

    [Header("At Fade In / Spread In Start")]
    [SerializeField] private GameObject[] setActiveTrueOnSpreadInStart;
    [SerializeField] private GameObject[] setActiveFalseOnSpreadInStart;

    [Header("At Fade Out / Spread Out Start")]
    [SerializeField] private GameObject[] setActiveTrueOnSpreadOutStart;
    [SerializeField] private GameObject[] setActiveFalseOnSpreadOutStart;

    [Header("Debug")]
    [SerializeField] private bool logDebug = true;

    private static readonly int SpreadProperty = Shader.PropertyToID("_Spread");

    private Material runtimeMaterial;
    private Coroutine sequenceRoutine;
    private bool isPlaying;

    public bool IsPlaying => isPlaying;

    private void Awake()
    {
        EnsureRuntimeMaterial();
        SetSpread(initialSpread);

        if (effectRoot != null)
            effectRoot.SetActive(false);
    }

    public void PlaySequence(Action onFullyCovered = null, Action onSequenceFinished = null)
    {
        if (isPlaying)
            return;

        if (!EnsureRuntimeMaterial())
        {
            Debug.LogError(
                "[DeathSpreadEffectController] Cannot play because the effect Image/material is missing.",
                this
            );
            return;
        }

        sequenceRoutine = StartCoroutine(
            PlaySequenceRoutine(onFullyCovered, onSequenceFinished)
        );
    }

    public void StopAndReset()
    {
        if (sequenceRoutine != null)
        {
            StopCoroutine(sequenceRoutine);
            sequenceRoutine = null;
        }

        isPlaying = false;
        SetSpread(0f);

        if (effectRoot != null)
            effectRoot.SetActive(false);
    }

    private IEnumerator PlaySequenceRoutine(
        Action onFullyCovered,
        Action onSequenceFinished)
    {
        isPlaying = true;

        if (effectRoot != null)
            effectRoot.SetActive(true);

        SetSpread(0f);

        // Phase 1:
        // Spread 0 -> 1 and the "enter" object toggles happen exactly once.
        ApplyActiveChanges(
            setActiveTrueOnSpreadInStart,
            setActiveFalseOnSpreadInStart
        );

        if (logDebug)
            Debug.Log(
                "[DeathSpreadEffectController] Spread In started.",
                this
            );

        yield return AnimateSpread(0f, 1f, spreadInDuration);

        // At this moment the screen is guaranteed fully black by the shader.
        if (onFullyCovered != null)
            onFullyCovered.Invoke();

        // Keep the covered state while gameplay restore completes.
        if (holdDuration > 0f)
            yield return new WaitForSeconds(holdDuration);

        // Phase 2:
        // The "exit" object toggles happen once, immediately before Spread 1 -> 0.
        ApplyActiveChanges(
            setActiveTrueOnSpreadOutStart,
            setActiveFalseOnSpreadOutStart
        );

        if (logDebug)
            Debug.Log(
                "[DeathSpreadEffectController] Spread Out started.",
                this
            );

        yield return AnimateSpread(1f, 0f, spreadOutDuration);

        SetSpread(0f);

        if (effectRoot != null)
            effectRoot.SetActive(false);

        isPlaying = false;
        sequenceRoutine = null;

        if (onSequenceFinished != null)
            onSequenceFinished.Invoke();
    }

    private IEnumerator AnimateSpread(
        float from,
        float to,
        float duration)
    {
        if (duration <= 0f)
        {
            SetSpread(to);
            yield break;
        }

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(elapsed / duration);

            // Smoothstep for a clean, editable UI transition.
            t = t * t * (3f - 2f * t);

            SetSpread(Mathf.Lerp(from, to, t));
            yield return null;
        }

        SetSpread(to);
    }

    private void ApplyActiveChanges(
        GameObject[] setTrue,
        GameObject[] setFalse)
    {
        if (setTrue != null)
        {
            for (int i = 0; i < setTrue.Length; i++)
            {
                if (setTrue[i] != null)
                    setTrue[i].SetActive(true);
            }
        }

        if (setFalse != null)
        {
            for (int i = 0; i < setFalse.Length; i++)
            {
                if (setFalse[i] != null)
                    setFalse[i].SetActive(false);
            }
        }
    }

    private bool EnsureRuntimeMaterial()
    {
        if (effectImage == null)
        {
            Debug.LogWarning(
                "[DeathSpreadEffectController] Effect Image is missing.",
                this
            );
            return false;
        }

        if (runtimeMaterial != null)
            return true;

        Material sourceMaterial = effectImage.material;

        if (sourceMaterial == null)
        {
            Debug.LogWarning(
                "[DeathSpreadEffectController] Effect Image has no material.",
                this
            );
            return false;
        }

        runtimeMaterial = new Material(sourceMaterial);
        runtimeMaterial.name = sourceMaterial.name + " (Runtime)";
        effectImage.material = runtimeMaterial;

        if (!runtimeMaterial.HasProperty(SpreadProperty))
        {
            Debug.LogError(
                "[DeathSpreadEffectController] Material shader does not expose _Spread.",
                this
            );
            return false;
        }

        return true;
    }

    private void SetSpread(float value)
    {
        if (runtimeMaterial == null)
            return;

        runtimeMaterial.SetFloat(
            SpreadProperty,
            Mathf.Clamp01(value)
        );
    }
}
