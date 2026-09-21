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
            float currentX =
                cameraTarget.position.x;

            float currentY =
                cameraTarget.position.y;

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
                cameraTarget.position;

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

            cameraTarget.position =
                new Vector3(
                    newX,
                    newY,
                    current.z);
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