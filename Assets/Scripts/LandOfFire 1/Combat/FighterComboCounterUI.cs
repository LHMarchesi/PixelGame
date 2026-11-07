// Land of Fire · Global combo counter UI.
// Bind one view to the fighter receiving the combo. It reads the shared
// ComboSystem history, so normal attacks, Movesets and Auto Combos share it.
using UnityEngine;
using UnityEngine.UI;

namespace LandOfFire.BunnyStep
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Text))]
    public sealed class FighterComboCounterUI : MonoBehaviour
    {
        [Header("Binding")]
        [Tooltip("The fighter receiving the hits represented by this counter. Place this UI wherever the combo counter should appear on the HUD.")]
        [SerializeField] private FighterMotor2D comboTarget;

        [Tooltip("Optional. If empty, the Text component on this GameObject is used.")]
        [SerializeField] private Text counterText;

        [Tooltip("Optional. If empty, a CanvasGroup is added to this GameObject automatically.")]
        [SerializeField] private CanvasGroup canvasGroup;

        [Tooltip("Optional transform to animate. If empty, the Text RectTransform is used.")]
        [SerializeField] private RectTransform animatedRect;

        [Header("Animation")]
        [SerializeField, Min(1f)] private float popScale = 1.38f;
        [SerializeField, Min(0.01f)] private float popDuration = 0.14f;
        [SerializeField, Min(0.01f)] private float fadeOutDuration = 0.18f;
        [SerializeField, Min(0f)] private float popRisePixels = 7f;

        private int displayedCount = -1;
        private float animationElapsed;
        private bool fadingOut;
        private Vector3 baseScale = Vector3.one;
        private Vector2 baseAnchoredPosition;
        private bool initialized;

        private void Awake()
        {
            Initialize();
            SetVisible(false);
        }

        private void OnEnable()
        {
            Initialize();
            displayedCount = -1;
            RefreshCounter();
        }

        private void Update()
        {
            RefreshCounter();
            Animate(Time.unscaledDeltaTime);
        }

        private void Initialize()
        {
            if (initialized)
                return;

            if (counterText == null)
                counterText = GetComponent<Text>();

            if (counterText == null)
            {
                Debug.LogError("FighterComboCounterUI requires a Unity UI Text component.", this);
                enabled = false;
                return;
            }

            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>();

            if (canvasGroup == null)
                canvasGroup = gameObject.AddComponent<CanvasGroup>();

            // This is a HUD readout only; it must never intercept menu or
            // gameplay UI pointer events placed behind it.
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
            counterText.raycastTarget = false;

            if (animatedRect == null)
                animatedRect = counterText.rectTransform;

            baseScale = animatedRect.localScale;
            baseAnchoredPosition = animatedRect.anchoredPosition;
            counterText.text = string.Empty;
            initialized = true;
        }

        private void RefreshCounter()
        {
            if (!initialized)
                return;

            int currentCount = comboTarget != null
                ? FighterCombatResolver.GetComboHitCount(comboTarget)
                : 0;

            if (currentCount == displayedCount)
                return;

            int previousCount = displayedCount;
            displayedCount = currentCount;
            animationElapsed = 0f;

            if (currentCount > 0)
            {
                counterText.text = currentCount.ToString();
                counterText.enabled = true;
                fadingOut = false;
                canvasGroup.alpha = 1f;
                animatedRect.localScale = baseScale * popScale;
                animatedRect.anchoredPosition =
                    baseAnchoredPosition + Vector2.up * popRisePixels;
            }
            else if (previousCount > 0)
            {
                fadingOut = true;
                counterText.enabled = true;
                canvasGroup.alpha = 1f;
            }
            else
            {
                SetVisible(false);
            }
        }

        private void Animate(float deltaTime)
        {
            if (!initialized)
                return;

            if (displayedCount <= 0 && !fadingOut)
                return;

            animationElapsed += Mathf.Max(0f, deltaTime);

            if (fadingOut)
            {
                float progress = Mathf.Clamp01(animationElapsed / Mathf.Max(0.01f, fadeOutDuration));
                canvasGroup.alpha = 1f - progress;
                animatedRect.localScale = baseScale * Mathf.Lerp(1f, 0.88f, progress);
                animatedRect.anchoredPosition = baseAnchoredPosition +
                    Vector2.up * (popRisePixels * 0.15f * (1f - progress));

                if (progress >= 1f)
                    SetVisible(false);

                return;
            }

            float popProgress = Mathf.Clamp01(animationElapsed / Mathf.Max(0.01f, popDuration));
            // Ease-out-back: a short overshoot gives each increment a readable punch.
            const float overshoot = 1.35f;
            float t = popProgress - 1f;
            float eased = 1f + (overshoot + 1f) * t * t * t + overshoot * t * t;
            animatedRect.localScale = baseScale * Mathf.Lerp(popScale, 1f, eased);
            animatedRect.anchoredPosition = baseAnchoredPosition +
                Vector2.up * Mathf.Lerp(popRisePixels, 0f, popProgress);
            canvasGroup.alpha = 1f;
        }

        private void SetVisible(bool visible)
        {
            if (counterText != null)
                counterText.enabled = visible;

            if (canvasGroup != null)
                canvasGroup.alpha = visible ? 1f : 0f;

            if (animatedRect != null)
            {
                animatedRect.localScale = baseScale;
                animatedRect.anchoredPosition = baseAnchoredPosition;
            }

            fadingOut = false;
        }

        private void OnDisable()
        {
            if (!initialized)
                return;

            SetVisible(false);
            displayedCount = -1;
        }

        private void OnValidate()
        {
            popScale = Mathf.Max(1f, popScale);
            popDuration = Mathf.Max(0.01f, popDuration);
            fadeOutDuration = Mathf.Max(0.01f, fadeOutDuration);
            popRisePixels = Mathf.Max(0f, popRisePixels);
        }
    }
}
