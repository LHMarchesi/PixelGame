using System.Collections.Generic;
using UnityEngine;

namespace LandOfFire.BunnyStep
{
    /// <summary>
    /// Procedural presentation for important combat events.
    /// This component does not decide combat outcomes; the shared resolver
    /// calls it only after a combat result such as Clash or Parry is confirmed.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CombatFeedbackController : MonoBehaviour
    {
        private sealed class ClashEffect
        {
            public GameObject Root;
            public Transform FlashTransform;
            public SpriteRenderer FlashRenderer;
            public Transform RingTransform;
            public SpriteRenderer RingRenderer;
            public ParticleSystem Sparks;
            public Color RingColor;
            public Color FlashColor;
            public float Elapsed;
            public float Duration;
            public float FlashDuration;
            public float FlashStartScale;
            public float FlashEndScale;
            public float RingStartScale;
            public float RingEndScale;
        }

        private sealed class FloatingCallout
        {
            public GameObject Root;
            public TextMesh Text;
            public Color Color;
            public Vector3 StartPosition;
            public float Elapsed;
            public float Duration;
            public float RiseDistance;
            public float StartScale;
            public float EndScale;
        }

        private static CombatFeedbackController instance;
        private static Sprite softDiscSprite;
        private static Sprite ringSprite;
        private static Material particleMaterial;

        [Header("Clash - Combat Response")]
        [SerializeField, Min(0)]
        [Tooltip("Hitstop aplicado a ambos luchadores cuando ocurre un Clash.")]
        private int clashHitstopTicks = 4;

        [Header("Clash - Visual")]
        [SerializeField]
        private Color clashColor = new Color(0.35f, 0.78f, 1f, 1f);

        [SerializeField]
        private Color flashColor = Color.white;

        [SerializeField, Min(0.05f)]
        private float clashEffectDuration = 0.36f;

        [SerializeField, Min(0.01f)]
        private float flashDuration = 0.085f;

        [SerializeField, Min(0f)]
        private float clashVerticalOffset = 0.55f;

        [SerializeField, Min(0.01f)]
        private float flashStartScale = 0.35f;

        [SerializeField, Min(0.01f)]
        private float flashEndScale = 1.75f;

        [SerializeField, Min(0.01f)]
        private float ringStartScale = 0.25f;

        [SerializeField, Min(0.01f)]
        private float ringEndScale = 2.65f;

        [SerializeField, Min(0)]
        private int sortingOrderOffset = 30;

        [Header("Clash - Sparks")]
        [SerializeField, Min(0)]
        private int sparkCount = 16;

        [SerializeField, Min(0.01f)]
        private float sparkLifetimeMin = 0.16f;

        [SerializeField, Min(0.01f)]
        private float sparkLifetimeMax = 0.30f;

        [SerializeField, Min(0f)]
        private float sparkSpeedMin = 2f;

        [SerializeField, Min(0f)]
        private float sparkSpeedMax = 5f;

        [SerializeField, Min(0.001f)]
        private float sparkSizeMin = 0.045f;

        [SerializeField, Min(0.001f)]
        private float sparkSizeMax = 0.11f;

        [Header("Clash - Camera")]
        [SerializeField]
        private FightCameraController fightCamera;

        [SerializeField, Min(0f)]
        private float cameraShakeAmplitude = 0.22f;

        [SerializeField, Min(0f)]
        private float cameraShakeDuration = 0.16f;

        [Header("Parry - Combat Response")]
        [SerializeField, Min(0)]
        [Tooltip("Hitstop aplicado a ambos luchadores al confirmar un Perfect Parry.")]
        private int perfectParryHitstopTicks = 3;

        [SerializeField, Min(0)]
        [Tooltip("Hitstop aplicado a ambos luchadores al confirmar un Parry imperfecto.")]
        private int imperfectParryHitstopTicks = 1;

        [SerializeField, Min(0f)]
        private float perfectParryShakeAmplitude = 0.12f;

        [SerializeField, Min(0f)]
        private float perfectParryShakeDuration = 0.12f;

        [SerializeField, Min(0f)]
        private float imperfectParryShakeAmplitude = 0.045f;

        [SerializeField, Min(0f)]
        private float imperfectParryShakeDuration = 0.08f;

        [Header("Parry - Visual")]
        [SerializeField]
        private Color perfectParryColor = new Color(0.38f, 0.88f, 1f, 1f);

        [SerializeField]
        private Color imperfectParryColor = new Color(0.36f, 0.62f, 0.88f, 1f);

        [SerializeField]
        private Color parryFlashColor = Color.white;

        [SerializeField, Min(0.05f)]
        private float perfectParryEffectDuration = 0.23f;

        [SerializeField, Min(0.05f)]
        private float imperfectParryEffectDuration = 0.16f;

        [SerializeField, Min(0)]
        private int perfectParrySparkCount = 10;

        [SerializeField, Min(0)]
        private int imperfectParrySparkCount = 5;

        [Header("Combat Callouts")]
        [SerializeField]
        private bool showCombatCallouts = true;

        [SerializeField]
        private Font calloutFont;

        [SerializeField, Min(8)]
        private int calloutFontSize = 64;

        [SerializeField, Min(0.01f)]
        private float calloutCharacterSize = 0.075f;

        [SerializeField, Min(0f)]
        private float calloutVerticalOffset = 2.0f;

        [SerializeField, Min(0.01f)]
        private float calloutRiseDistance = 0.45f;

        [SerializeField, Min(0.05f)]
        private float calloutDuration = 0.72f;

        [SerializeField, Min(0.01f)]
        private float calloutStartScale = 0.72f;

        [SerializeField, Min(0.01f)]
        private float calloutEndScale = 1.0f;

        [SerializeField, Min(0)]
        private int calloutSortingOrderOffset = 100;

        [SerializeField]
        private Color clashCalloutColor = new Color(0.52f, 0.88f, 1f, 1f);

        [SerializeField]
        private Color parryCalloutColor = new Color(0.40f, 0.72f, 1f, 1f);

        [SerializeField]
        private Color perfectParryCalloutColor = new Color(0.75f, 0.97f, 1f, 1f);

        [SerializeField]
        private Color guardBreakCalloutColor = new Color(1f, 0.38f, 0.22f, 1f);

        private readonly List<ClashEffect> activeEffects =
            new List<ClashEffect>(4);

        private readonly List<FloatingCallout> activeCallouts =
            new List<FloatingCallout>(8);

        public static int ClashHitstopTicks
        {
            get
            {
                CombatFeedbackController controller = EnsureInstance();
                return controller != null
                    ? Mathf.Max(0, controller.clashHitstopTicks)
                    : 0;
            }
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            EnsureProceduralAssets();
        }

        private void OnDestroy()
        {
            if (instance == this)
                instance = null;
        }

        private void Update()
        {
            float deltaTime = Time.unscaledDeltaTime;
            UpdateActiveEffects(deltaTime);
            UpdateCallouts(deltaTime);
        }

        /// <summary>
        /// Called only by FighterCombatResolver after both attacks have been
        /// cancelled as a confirmed Clash.
        /// </summary>
        public static void TriggerClash(
            FighterMotor2D first,
            FighterMotor2D second)
        {
            if (first == null || second == null)
                return;

            CombatFeedbackController controller = EnsureInstance();
            if (controller == null)
                return;

            controller.PlayClash(first, second);
        }

        /// <summary>
        /// Called only after the shared resolver confirms a Parry. Perfect and
        /// imperfect Parries have distinct response intensity and visuals.
        /// </summary>
        public static void TriggerParry(
            FighterMotor2D defender,
            FighterMotor2D attacker,
            bool perfect)
        {
            if (defender == null || attacker == null)
                return;

            CombatFeedbackController controller = EnsureInstance();
            if (controller == null)
                return;

            controller.PlayParry(defender, attacker, perfect);
        }

        public static void TriggerGuardBreak(FighterMotor2D defender)
        {
            if (defender == null)
                return;

            CombatFeedbackController controller = EnsureInstance();
            if (controller == null)
                return;

            controller.PlayGuardBreak(defender);
        }

        private static CombatFeedbackController EnsureInstance()
        {
            if (instance != null)
                return instance;

            instance = FindObjectOfType<CombatFeedbackController>();
            if (instance != null)
                return instance;

            GameObject feedbackObject =
                new GameObject("[Land of Fire] Combat Feedback");
            feedbackObject.hideFlags = HideFlags.HideInHierarchy;
            DontDestroyOnLoad(feedbackObject);
            instance = feedbackObject.AddComponent<CombatFeedbackController>();
            return instance;
        }

        private void PlayClash(
            FighterMotor2D first,
            FighterMotor2D second)
        {
            int hitstop = Mathf.Max(0, clashHitstopTicks);
            if (hitstop > 0)
            {
                first.ApplyHitstop(hitstop);
                second.ApplyHitstop(hitstop);
            }

            Vector2 midpoint =
                (first.BodyPosition + second.BodyPosition) * 0.5f;
            midpoint.y += clashVerticalOffset;
            SpawnCallout(
                "CLASH!",
                midpoint + Vector2.up * 0.72f,
                first,
                clashCalloutColor,
                calloutDuration,
                1.05f);

            SpawnCombatEffect(
                midpoint,
                first,
                second,
                "Clash Feedback Effect",
                clashColor,
                flashColor,
                clashEffectDuration,
                flashDuration,
                flashStartScale,
                flashEndScale,
                ringStartScale,
                ringEndScale,
                sparkCount,
                sparkLifetimeMin,
                sparkLifetimeMax,
                sparkSpeedMin,
                sparkSpeedMax,
                sparkSizeMin,
                sparkSizeMax);

            FightCameraController cameraController = GetFightCamera();
            if (cameraController != null && cameraShakeAmplitude > 0f &&
                cameraShakeDuration > 0f)
            {
                cameraController.AddImpactShake(
                    cameraShakeAmplitude,
                    cameraShakeDuration);
            }
        }

        private void PlayParry(
            FighterMotor2D defender,
            FighterMotor2D attacker,
            bool perfect)
        {
            int hitstop = perfect
                ? Mathf.Max(0, perfectParryHitstopTicks)
                : Mathf.Max(0, imperfectParryHitstopTicks);

            if (hitstop > 0)
            {
                defender.ApplyHitstop(hitstop);
                attacker.ApplyHitstop(hitstop);
            }

            Vector2 midpoint =
                (defender.BodyPosition + attacker.BodyPosition) * 0.5f;
            midpoint.y += clashVerticalOffset;

            Color effectColor = perfect
                ? perfectParryColor
                : imperfectParryColor;
            float effectDuration = perfect
                ? perfectParryEffectDuration
                : imperfectParryEffectDuration;
            int particles = perfect
                ? perfectParrySparkCount
                : imperfectParrySparkCount;

            SpawnCombatEffect(
                midpoint,
                defender,
                attacker,
                perfect ? "Perfect Parry Feedback Effect" : "Parry Feedback Effect",
                effectColor,
                parryFlashColor,
                effectDuration,
                perfect ? 0.055f : 0.045f,
                perfect ? 0.16f : 0.13f,
                perfect ? 1.10f : 0.82f,
                perfect ? 0.14f : 0.12f,
                perfect ? 1.65f : 1.12f,
                particles,
                0.10f,
                0.20f,
                perfect ? 1.8f : 1.1f,
                perfect ? 3.8f : 2.4f,
                0.025f,
                perfect ? 0.075f : 0.055f);

            SpawnCallout(
                perfect ? "PERFECT PARRY!" : "PARRY",
                defender.BodyPosition + Vector2.up * calloutVerticalOffset,
                defender,
                perfect ? perfectParryCalloutColor : parryCalloutColor,
                perfect ? calloutDuration : calloutDuration * 0.82f,
                perfect ? 1.12f : 0.92f);

            float shakeAmplitude = perfect
                ? perfectParryShakeAmplitude
                : imperfectParryShakeAmplitude;
            float shakeDuration = perfect
                ? perfectParryShakeDuration
                : imperfectParryShakeDuration;

            FightCameraController cameraController = GetFightCamera();
            if (cameraController != null && shakeAmplitude > 0f &&
                shakeDuration > 0f)
            {
                cameraController.AddImpactShake(
                    shakeAmplitude,
                    shakeDuration);
            }
        }

        private void PlayGuardBreak(FighterMotor2D defender)
        {
            SpawnCallout(
                "GUARD BREAK!",
                defender.BodyPosition + Vector2.up * calloutVerticalOffset,
                defender,
                guardBreakCalloutColor,
                calloutDuration,
                1.08f);
        }

        private void SpawnCallout(
            string label,
            Vector3 position,
            FighterMotor2D sortingReference,
            Color color,
            float duration,
            float sizeMultiplier)
        {
            if (!showCombatCallouts || string.IsNullOrEmpty(label))
                return;

            Font font = calloutFont != null ? calloutFont : ResolveCalloutFont();
            if (font == null)
                return;

            GameObject root = new GameObject("Combat Callout - " + label);
            root.layer = 0;
            root.transform.position = position;

            TextMesh text = root.AddComponent<TextMesh>();
            text.text = label;
            text.font = font;
            text.fontSize = Mathf.Max(8, calloutFontSize);
            text.characterSize = Mathf.Max(0.01f, calloutCharacterSize);
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.fontStyle = FontStyle.Bold;
            text.richText = false;
            text.color = color;

            MeshRenderer renderer = root.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                int sortingLayerId = 0;
                int sortingLayerValue = int.MinValue;
                int sortingOrder = 0;

                if (sortingReference != null)
                {
                    SpriteRenderer[] sprites =
                        sortingReference.GetComponentsInChildren<SpriteRenderer>(true);
                    for (int i = 0; i < sprites.Length; i++)
                    {
                        SpriteRenderer sprite = sprites[i];
                        if (sprite == null)
                            continue;

                        int layerValue = SortingLayer.GetLayerValueFromID(
                            sprite.sortingLayerID);
                        if (layerValue > sortingLayerValue ||
                            (layerValue == sortingLayerValue &&
                             sprite.sortingOrder > sortingOrder))
                        {
                            sortingLayerValue = layerValue;
                            sortingLayerId = sprite.sortingLayerID;
                            sortingOrder = sprite.sortingOrder;
                        }
                    }
                }

                renderer.sortingLayerID = sortingLayerId;
                renderer.sortingOrder = sortingOrder +
                    Mathf.Max(0, calloutSortingOrderOffset);
            }

            activeCallouts.Add(new FloatingCallout
            {
                Root = root,
                Text = text,
                Color = color,
                StartPosition = position,
                Duration = Mathf.Max(0.05f, duration),
                RiseDistance = Mathf.Max(0.01f, calloutRiseDistance),
                StartScale = Mathf.Max(0.01f, calloutStartScale),
                EndScale = Mathf.Max(0.01f, calloutEndScale) *
                           Mathf.Max(0.1f, sizeMultiplier)
            });
        }

        private Font ResolveCalloutFont()
        {
            if (calloutFont != null)
                return calloutFont;

            calloutFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (calloutFont == null)
                calloutFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (calloutFont == null)
            {
                try
                {
                    calloutFont = Font.CreateDynamicFontFromOSFont(
                        "Arial",
                        Mathf.Max(8, calloutFontSize));
                }
                catch
                {
                    calloutFont = null;
                }
            }

            return calloutFont;
        }

        private void UpdateCallouts(float deltaTime)
        {
            for (int i = activeCallouts.Count - 1; i >= 0; i--)
            {
                FloatingCallout callout = activeCallouts[i];
                if (callout.Root == null || callout.Text == null)
                {
                    activeCallouts.RemoveAt(i);
                    continue;
                }

                callout.Elapsed += deltaTime;
                float progress = Mathf.Clamp01(
                    callout.Elapsed / Mathf.Max(0.0001f, callout.Duration));
                float riseProgress = 1f - Mathf.Pow(1f - progress, 2f);
                callout.Root.transform.position = callout.StartPosition +
                    Vector3.up * (callout.RiseDistance * riseProgress);

                float popProgress = Mathf.Clamp01(callout.Elapsed / 0.10f);
                float pop = Mathf.Lerp(
                    callout.StartScale,
                    callout.EndScale,
                    Mathf.SmoothStep(0f, 1f, popProgress));
                callout.Root.transform.localScale = Vector3.one * pop;

                float fadeStart = 0.62f;
                float alpha = progress <= fadeStart
                    ? 1f
                    : 1f - Mathf.InverseLerp(fadeStart, 1f, progress);
                Color current = callout.Color;
                current.a *= alpha;
                callout.Text.color = current;

                if (progress >= 1f)
                {
                    Destroy(callout.Root);
                    activeCallouts.RemoveAt(i);
                }
            }
        }

        private FightCameraController GetFightCamera()
        {
            if (fightCamera == null)
                fightCamera = FindObjectOfType<FightCameraController>();

            return fightCamera;
        }

        private void SpawnCombatEffect(
            Vector2 position,
            FighterMotor2D first,
            FighterMotor2D second,
            string effectName,
            Color effectColor,
            Color effectFlashColor,
            float duration,
            float localFlashDuration,
            float localFlashStartScale,
            float localFlashEndScale,
            float localRingStartScale,
            float localRingEndScale,
            int localSparkCount,
            float localSparkLifetimeMin,
            float localSparkLifetimeMax,
            float localSparkSpeedMin,
            float localSparkSpeedMax,
            float localSparkSizeMin,
            float localSparkSizeMax)
        {
            EnsureProceduralAssets();

            SpriteRenderer referenceRenderer =
                first.GetComponentInChildren<SpriteRenderer>();
            if (referenceRenderer == null)
                referenceRenderer = second.GetComponentInChildren<SpriteRenderer>();

            int sortingLayer = referenceRenderer != null
                ? referenceRenderer.sortingLayerID
                : 0;
            int sortingOrder = referenceRenderer != null
                ? referenceRenderer.sortingOrder + sortingOrderOffset
                : sortingOrderOffset;

            float z = Mathf.Min(first.transform.position.z,
                                second.transform.position.z) - 0.1f;

            GameObject root = new GameObject(effectName);
            root.transform.position = new Vector3(position.x, position.y, z);

            SpriteRenderer flashRenderer =
                CreateSpriteChild(
                    root.transform,
                    "Impact Flash",
                    softDiscSprite,
                    sortingLayer,
                    sortingOrder + 2,
                    effectFlashColor,
                    out Transform flashTransform);

            SpriteRenderer ringRenderer =
                CreateSpriteChild(
                    root.transform,
                    "Impact Ring",
                    ringSprite,
                    sortingLayer,
                    sortingOrder + 1,
                    effectColor,
                    out Transform ringTransform);

            ParticleSystem sparks =
                CreateSparkSystem(
                    root.transform,
                    sortingLayer,
                    sortingOrder + 3,
                    localSparkCount,
                    effectColor,
                    effectFlashColor,
                    localSparkLifetimeMin,
                    localSparkLifetimeMax,
                    localSparkSpeedMin,
                    localSparkSpeedMax,
                    localSparkSizeMin,
                    localSparkSizeMax);

            ClashEffect effect = new ClashEffect
            {
                Root = root,
                FlashTransform = flashTransform,
                FlashRenderer = flashRenderer,
                RingTransform = ringTransform,
                RingRenderer = ringRenderer,
                Sparks = sparks,
                RingColor = effectColor,
                FlashColor = effectFlashColor,
                Duration = Mathf.Max(0.05f, duration),
                FlashDuration = Mathf.Clamp(
                    localFlashDuration,
                    0.01f,
                    Mathf.Max(0.05f, duration)),
                FlashStartScale = Mathf.Max(0.01f, localFlashStartScale),
                FlashEndScale = Mathf.Max(0.01f, localFlashEndScale),
                RingStartScale = Mathf.Max(0.01f, localRingStartScale),
                RingEndScale = Mathf.Max(0.01f, localRingEndScale)
            };

            activeEffects.Add(effect);
        }

        private SpriteRenderer CreateSpriteChild(
            Transform parent,
            string objectName,
            Sprite sprite,
            int sortingLayer,
            int sortingOrder,
            Color color,
            out Transform childTransform)
        {
            GameObject child = new GameObject(objectName);
            child.transform.SetParent(parent, false);

            SpriteRenderer renderer = child.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingLayerID = sortingLayer;
            renderer.sortingOrder = sortingOrder;

            childTransform = child.transform;
            return renderer;
        }

        private ParticleSystem CreateSparkSystem(
            Transform parent,
            int sortingLayer,
            int sortingOrder,
            int localSparkCount,
            Color effectColor,
            Color effectFlashColor,
            float localLifetimeMin,
            float localLifetimeMax,
            float localSpeedMin,
            float localSpeedMax,
            float localSizeMin,
            float localSizeMax)
        {
            if (localSparkCount <= 0 || GetParticleMaterial() == null)
                return null;

            GameObject sparkObject = new GameObject("Clash Sparks");
            sparkObject.transform.SetParent(parent, false);

            ParticleSystem particles =
                sparkObject.AddComponent<ParticleSystem>();

            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = particles.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = Mathf.Max(0.05f, localLifetimeMax);
            main.maxParticles = Mathf.Max(1, localSparkCount);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 0f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(
                Mathf.Min(localLifetimeMin, localLifetimeMax),
                Mathf.Max(localLifetimeMin, localLifetimeMax));
            main.startSpeed = new ParticleSystem.MinMaxCurve(
                Mathf.Min(localSpeedMin, localSpeedMax),
                Mathf.Max(localSpeedMin, localSpeedMax));
            main.startSize = new ParticleSystem.MinMaxCurve(
                Mathf.Min(localSizeMin, localSizeMax),
                Mathf.Max(localSizeMin, localSizeMax));
            main.startColor = new ParticleSystem.MinMaxGradient(
                effectColor,
                effectFlashColor);

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.enabled = false;

            ParticleSystem.ShapeModule shape = particles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.035f;
            shape.arc = 360f;

            ParticleSystem.ColorOverLifetimeModule colorOverLifetime =
                particles.colorOverLifetime;
            colorOverLifetime.enabled = true;
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(
                CreateSparkGradient(effectColor, effectFlashColor));

            ParticleSystem.SizeOverLifetimeModule sizeOverLifetime =
                particles.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(
                1f,
                AnimationCurve.EaseInOut(0f, 1f, 1f, 0f));

            ParticleSystemRenderer particleRenderer =
                particles.GetComponent<ParticleSystemRenderer>();
            particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            particleRenderer.sortingLayerID = sortingLayer;
            particleRenderer.sortingOrder = sortingOrder;
            particleRenderer.sharedMaterial = GetParticleMaterial();

            particles.Play(true);
            particles.Emit(Mathf.Max(0, localSparkCount));
            return particles;
        }

        private Gradient CreateSparkGradient(
            Color effectColor,
            Color effectFlashColor)
        {
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(effectFlashColor, 0f),
                    new GradientColorKey(effectColor, 0.45f),
                    new GradientColorKey(effectColor, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(0.9f, 0.2f),
                    new GradientAlphaKey(0f, 1f)
                });
            return gradient;
        }

        private void UpdateActiveEffects(float deltaTime)
        {
            for (int i = activeEffects.Count - 1; i >= 0; i--)
            {
                ClashEffect effect = activeEffects[i];
                if (effect.Root == null)
                {
                    activeEffects.RemoveAt(i);
                    continue;
                }

                effect.Elapsed += deltaTime;
                float normalized = Mathf.Clamp01(
                    effect.Elapsed / Mathf.Max(0.0001f, effect.Duration));

                float ringProgress = 1f - Mathf.Pow(1f - normalized, 3f);
                float ringScale = Mathf.Lerp(
                    effect.RingStartScale,
                    effect.RingEndScale,
                    ringProgress);
                effect.RingTransform.localScale =
                    Vector3.one * ringScale;

                Color ringColor = effect.RingColor;
                ringColor.a = 1f - normalized;
                effect.RingRenderer.color = ringColor;

                float flashProgress = Mathf.Clamp01(
                    effect.Elapsed / Mathf.Max(0.0001f, effect.FlashDuration));
                float flashScale = Mathf.Lerp(
                    effect.FlashStartScale,
                    effect.FlashEndScale,
                    flashProgress);
                effect.FlashTransform.localScale =
                    Vector3.one * flashScale;

                Color currentFlashColor = effect.FlashColor;
                currentFlashColor.a = (1f - flashProgress) * (1f - flashProgress);
                effect.FlashRenderer.color = currentFlashColor;

                if (effect.Elapsed >= effect.Duration)
                {
                    Destroy(effect.Root);
                    activeEffects.RemoveAt(i);
                }
            }
        }

        private static void EnsureProceduralAssets()
        {
            if (softDiscSprite == null)
                softDiscSprite = CreateSoftDiscSprite(64);

            if (ringSprite == null)
                ringSprite = CreateRingSprite(96);
        }

        private static Sprite CreateSoftDiscSprite(int size)
        {
            Texture2D texture = new Texture2D(
                size,
                size,
                TextureFormat.RGBA32,
                false);
            texture.name = "Runtime Clash Glow Texture";
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.hideFlags = HideFlags.DontSave;

            Color[] pixels = new Color[size * size];
            Vector2 center = new Vector2(0.5f, 0.5f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 uv = new Vector2(
                        (x + 0.5f) / size,
                        (y + 0.5f) / size);
                    float distance = Vector2.Distance(uv, center) * 2f;
                    float alpha = Mathf.Pow(
                        Mathf.Clamp01(1f - distance),
                        2.2f);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(false, true);
            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                100f);
            sprite.name = "Runtime Clash Glow";
            sprite.hideFlags = HideFlags.DontSave;
            return sprite;
        }

        private static Sprite CreateRingSprite(int size)
        {
            Texture2D texture = new Texture2D(
                size,
                size,
                TextureFormat.RGBA32,
                false);
            texture.name = "Runtime Clash Ring Texture";
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.hideFlags = HideFlags.DontSave;

            Color[] pixels = new Color[size * size];
            Vector2 center = new Vector2(0.5f, 0.5f);
            const float ringRadius = 0.76f;
            const float ringThickness = 0.075f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 uv = new Vector2(
                        (x + 0.5f) / size,
                        (y + 0.5f) / size);
                    float distance = Vector2.Distance(uv, center) * 2f;
                    float edge = Mathf.Abs(distance - ringRadius);
                    float alpha = 1f - Mathf.SmoothStep(
                        ringThickness * 0.35f,
                        ringThickness,
                        edge);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(false, true);
            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                100f);
            sprite.name = "Runtime Clash Ring";
            sprite.hideFlags = HideFlags.DontSave;
            return sprite;
        }

        private static Material GetParticleMaterial()
        {
            if (particleMaterial != null)
                return particleMaterial;

            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null)
                shader = Shader.Find("Particles/Standard Unlit");
            if (shader == null)
                shader = Shader.Find("Sprites/Default");

            if (shader == null)
                return null;

            particleMaterial = new Material(shader)
            {
                name = "Runtime Clash Particle Material",
                hideFlags = HideFlags.DontSave,
                mainTexture = softDiscSprite != null
                    ? softDiscSprite.texture
                    : null,
                color = Color.white
            };

            if (particleMaterial.HasProperty("_BaseMap") &&
                softDiscSprite != null)
            {
                particleMaterial.SetTexture(
                    "_BaseMap",
                    softDiscSprite.texture);
            }

            return particleMaterial;
        }
    }
}
