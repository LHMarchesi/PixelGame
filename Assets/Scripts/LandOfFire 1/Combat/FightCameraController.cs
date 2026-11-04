using UnityEngine;
using Cinemachine;

namespace LandOfFire.BunnyStep
{
    public sealed class FightCameraController : MonoBehaviour
    {
        // ================================================================
        // PLAYERS
        // ================================================================

        [Header("Players")]

        [SerializeField]
        private Transform playerA;

        [SerializeField]
        private Transform playerB;

        // ================================================================
        // CAMERA
        // ================================================================

        [Header("Camera")]

        [SerializeField]
        private CinemachineVirtualCamera virtualCamera;

        [SerializeField]
        private Transform cameraTarget;

        // ================================================================
        // STAGE LIMITS
        // ================================================================

        [Header("Stage Limits")]

        [SerializeField]
        private Transform leftCameraLimit;

        [SerializeField]
        private Transform rightCameraLimit;

        // ================================================================
        // ZOOM
        // ================================================================

        [Header("Zoom")]

        [SerializeField]
        [Tooltip("Zoom más cercano permitido.")]
        private float minOrthographicSize = 4.5f;

        [SerializeField]
        [Tooltip("Zoom más alejado permitido.")]
        private float maxOrthographicSize = 8f;

        [SerializeField]
        [Tooltip("Espacio horizontal extra alrededor de los fighters.")]
        private float horizontalPadding = 2.5f;

        [SerializeField]
        [Tooltip("Espacio vertical extra alrededor de los fighters.")]
        private float verticalPadding = 2f;

        [SerializeField]
        [Tooltip("Diferencia necesaria para comenzar a cambiar el zoom.")]
        private float zoomHysteresis = .5f;

        // ================================================================
        // DEAD ZONE
        // ================================================================

        [Header("Dead Zone")]

        [SerializeField]
        [Tooltip(
            "Distancia horizontal que los fighters pueden " +
            "alejarse del centro antes de mover la cámara.")]
        private float horizontalDeadZone = 1f;

        [SerializeField]
        [Tooltip(
            "Distancia vertical que los fighters pueden " +
            "alejarse del centro antes de mover la cámara.")]
        private float verticalDeadZone = .75f;

        // ================================================================
        // POSITION SMOOTHING
        // ================================================================

        [Header("Position Smoothing")]

        [SerializeField]
        [Tooltip("Suavizado horizontal del centro de cámara.")]
        private float horizontalSmoothTime = .15f;

        [SerializeField]
        [Tooltip("Suavizado vertical del centro de cámara.")]
        private float verticalSmoothTime = .30f;

        // ================================================================
        // ZOOM SMOOTHING
        // ================================================================

        [Header("Zoom Smoothing")]

        [SerializeField]
        [Tooltip("Qué tan rápido se aleja la cámara.")]
        private float zoomOutSmoothTime = .12f;

        [SerializeField]
        [Tooltip("Qué tan lento vuelve a acercarse.")]
        private float zoomInSmoothTime = .40f;

        // ================================================================
        // RUNTIME
        // ================================================================

        private float horizontalVelocity;
        private float verticalVelocity;
        private float zoomVelocity;

        private float desiredCenterX;
        private float desiredCenterY;

        // ================================================================
        // IMPACT SHAKE
        // ================================================================

        [Header("Impact Shake")]

        [SerializeField]
        private bool enableImpactShake = true;

        [SerializeField, Min(0f)]
        private float maxImpactShakeAmplitude = 0.45f;

        [SerializeField, Min(1f)]
        private float impactShakeFrequency = 42f;

        private float impactShakeAmplitude;
        private float impactShakeDuration;
        private float impactShakeElapsed;
        private float impactShakePhase;
        private Vector3 appliedShakeOffset;

        /// <summary>
        /// Adds a short camera shake for a confirmed combat event.
        /// The offset is removed before camera tracking is calculated, so
        /// shake never accumulates into the camera's follow position.
        /// </summary>
        public void AddImpactShake(float amplitude, float duration)
        {
            if (!enableImpactShake || amplitude <= 0f || duration <= 0f)
                return;

            impactShakeAmplitude = Mathf.Clamp(
                impactShakeAmplitude + amplitude,
                0f,
                Mathf.Max(0f, maxImpactShakeAmplitude));
            impactShakeDuration = Mathf.Max(impactShakeDuration, duration);
            impactShakeElapsed = 0f;
            impactShakePhase = Time.unscaledTime * 13.7f;
        }

        // ================================================================
        // UNITY
        // ================================================================

        private void LateUpdate()
        {
            if (playerA == null ||
                playerB == null ||
                virtualCamera == null ||
                cameraTarget == null)
            {
                return;
            }

            UpdateCamera();
        }

        // ================================================================
        // MAIN CAMERA UPDATE
        // ================================================================

        private void UpdateCamera()
        {
            CalculatePlayerCenter(
                out float centerX,
                out float centerY);

            CalculateDesiredPosition(
                centerX,
                centerY);

            float targetZoom =
                CalculateDesiredZoom();

            targetZoom =
                ApplyZoomHysteresis(
                    targetZoom);

            UpdateZoom(
                targetZoom);

            UpdatePosition();
        }

        // ================================================================
        // PLAYER CENTER
        // ================================================================

        private void CalculatePlayerCenter(
            out float centerX,
            out float centerY)
        {
            centerX =
                (playerA.position.x +
                 playerB.position.x) * .5f;

            centerY =
                (playerA.position.y +
                 playerB.position.y) * .5f;
        }

        // ================================================================
        // POSITION
        // ================================================================

        private void CalculateDesiredPosition(
            float centerX,
            float centerY)
        {
            Vector3 baseCameraPosition =
                cameraTarget.position - appliedShakeOffset;

            float currentX =
                baseCameraPosition.x;

            float currentY =
                baseCameraPosition.y;

            float deltaX =
                centerX - currentX;

            float deltaY =
                centerY - currentY;

            // ------------------------------------------------------------
            // HORIZONTAL DEAD ZONE
            // ------------------------------------------------------------

            if (Mathf.Abs(deltaX) >
                horizontalDeadZone)
            {
                desiredCenterX =
                    centerX -
                    Mathf.Sign(deltaX) *
                    horizontalDeadZone;
            }
            else
            {
                desiredCenterX =
                    currentX;
            }

            // ------------------------------------------------------------
            // VERTICAL DEAD ZONE
            // ------------------------------------------------------------

            if (Mathf.Abs(deltaY) >
                verticalDeadZone)
            {
                desiredCenterY =
                    centerY -
                    Mathf.Sign(deltaY) *
                    verticalDeadZone;
            }
            else
            {
                desiredCenterY =
                    currentY;
            }

            // ------------------------------------------------------------
            // STAGE LIMITS
            // ------------------------------------------------------------

            desiredCenterX =
                ConstrainHorizontalPosition(
                    desiredCenterX,
                    virtualCamera.m_Lens.OrthographicSize);
        }

        private void UpdatePosition()
        {
            Vector3 current =
                cameraTarget.position - appliedShakeOffset;

            float newX =
                Mathf.SmoothDamp(
                    current.x,
                    desiredCenterX,
                    ref horizontalVelocity,
                    horizontalSmoothTime);

            float newY =
                Mathf.SmoothDamp(
                    current.y,
                    desiredCenterY,
                    ref verticalVelocity,
                    verticalSmoothTime);

            Vector3 basePosition = new Vector3(
                newX,
                newY,
                current.z);

            appliedShakeOffset = CalculateImpactShakeOffset();
            cameraTarget.position = basePosition + appliedShakeOffset;
        }

        private Vector3 CalculateImpactShakeOffset()
        {
            if (impactShakeAmplitude <= 0f || impactShakeDuration <= 0f ||
                impactShakeElapsed >= impactShakeDuration)
            {
                impactShakeAmplitude = 0f;
                impactShakeDuration = 0f;
                return Vector3.zero;
            }

            impactShakeElapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(
                impactShakeElapsed / Mathf.Max(0.0001f, impactShakeDuration));
            float envelope = (1f - progress) * (1f - progress);
            float phase = impactShakePhase +
                          impactShakeElapsed * impactShakeFrequency * Mathf.PI * 2f;

            float x = Mathf.Sin(phase) * impactShakeAmplitude * envelope;
            float y = Mathf.Sin(phase * 1.73f + 0.85f) *
                      impactShakeAmplitude * 0.58f * envelope;

            return new Vector3(x, y, 0f);
        }

        // ================================================================
        // ZOOM CALCULATION
        // ================================================================

        private float CalculateDesiredZoom()
        {
            Camera mainCamera =
                Camera.main;

            if (mainCamera == null)
                return minOrthographicSize;

            float aspect =
                mainCamera.aspect;

            float horizontalDistance =
                Mathf.Abs(
                    playerA.position.x -
                    playerB.position.x);

            float verticalDistance =
                Mathf.Abs(
                    playerA.position.y -
                    playerB.position.y);

            // ------------------------------------------------------------
            // HORIZONTAL REQUIREMENT
            // ------------------------------------------------------------

            float requiredWidth =
                horizontalDistance +
                horizontalPadding * 2f;

            float requiredSizeFromWidth =
                requiredWidth /
                (2f * aspect);

            // ------------------------------------------------------------
            // VERTICAL REQUIREMENT
            // ------------------------------------------------------------

            float requiredHeight =
                verticalDistance +
                verticalPadding * 2f;

            float requiredSizeFromHeight =
                requiredHeight /
                2f;

            float requiredSize =
                Mathf.Max(
                    requiredSizeFromWidth,
                    requiredSizeFromHeight);

            return Mathf.Clamp(
                requiredSize,
                minOrthographicSize,
                maxOrthographicSize);
        }

        // ================================================================
        // ZOOM HYSTERESIS
        // ================================================================

        private float ApplyZoomHysteresis(
            float targetZoom)
        {
            float currentZoom =
                virtualCamera.m_Lens.OrthographicSize;

            float difference =
                targetZoom - currentZoom;

            // La cámara está dentro de la zona estable.
            // No hacemos absolutamente nada.
            if (Mathf.Abs(difference) <
                zoomHysteresis)
            {
                return currentZoom;
            }

            return targetZoom;
        }

        // ================================================================
        // ZOOM UPDATE
        // ================================================================

        private void UpdateZoom(
            float targetZoom)
        {
            float currentZoom =
                virtualCamera.m_Lens.OrthographicSize;

            float smoothTime =
                targetZoom > currentZoom
                    ? zoomOutSmoothTime
                    : zoomInSmoothTime;

            float newZoom =
                Mathf.SmoothDamp(
                    currentZoom,
                    targetZoom,
                    ref zoomVelocity,
                    smoothTime);

            virtualCamera.m_Lens.OrthographicSize =
                newZoom;
        }

        // ================================================================
        // STAGE CLAMP
        // ================================================================

        private float ConstrainHorizontalPosition(
            float desiredX,
            float orthographicSize)
        {
            Camera mainCamera =
                Camera.main;

            if (mainCamera == null)
                return desiredX;

            float aspect =
                mainCamera.aspect;

            float halfWidth =
                orthographicSize *
                aspect;

            float leftLimit =
                leftCameraLimit != null
                    ? leftCameraLimit.position.x
                    : float.NegativeInfinity;

            float rightLimit =
                rightCameraLimit != null
                    ? rightCameraLimit.position.x
                    : float.PositiveInfinity;

            float minimumCenter =
                leftLimit +
                halfWidth;

            float maximumCenter =
                rightLimit -
                halfWidth;

            // El escenario es más pequeño que la cámara.
            if (minimumCenter >
                maximumCenter)
            {
                return
                    (leftLimit +
                     rightLimit) * .5f;
            }

            return Mathf.Clamp(
                desiredX,
                minimumCenter,
                maximumCenter);
        }
    }
}