// Land of Fire · FUN-COM-001
// Detecta hurtboxes en los ticks activos de cualquier ataque L/M/H.
// Cada objetivo recibe un solo impacto por ejecución.
// No requiere un rival asignado ni usa Animation Events.
//
// También controla la visualización opcional de la hitbox para debug.

using UnityEngine;

namespace LandOfFire.BunnyStep
{
    [DisallowMultipleComponent]
    public sealed class FighterAttackDebugView : MonoBehaviour
    {
        private GameObject debugObject;
        private SpriteRenderer spriteRenderer;

        private Texture2D debugTexture;
        private Sprite debugSprite;

        private void Awake()
        {
            CreateDebugObject();
            Hide();
        }

        public void Show(
            AttackMoveData attack,
            int facing,
            Color color)
        {
            if (attack == null ||
                !attack.showHitboxDebug)
            {
                Hide();
                return;
            }

            EnsureCreated();

            Transform debugTransform =
                debugObject.transform;

            Vector2 offset = attack.hitboxOffset;

            debugTransform.localPosition =
                new Vector3(
                    offset.x * facing,
                    offset.y,
                    0f
                );

            debugTransform.localRotation =
                Quaternion.identity;

            debugTransform.localScale =
                new Vector3(
                    Mathf.Max(.01f, attack.hitboxSize.x),
                    Mathf.Max(.01f, attack.hitboxSize.y),
                    1f
                );

            spriteRenderer.color = color;
            spriteRenderer.enabled = true;
        }

        public void Hide()
        {
            if (spriteRenderer != null)
                spriteRenderer.enabled = false;
        }

        private void CreateDebugObject()
        {
            if (debugObject != null)
                return;

            debugObject =
                new GameObject("AttackHitboxDebug");

            debugObject.transform.SetParent(
                transform,
                false
            );

            spriteRenderer =
                debugObject.AddComponent<SpriteRenderer>();

            CreateSprite();

            spriteRenderer.sprite = debugSprite;

            SpriteRenderer ownerRenderer =
                GetComponentInChildren<SpriteRenderer>();

            if (ownerRenderer != null &&
                ownerRenderer != spriteRenderer)
            {
                spriteRenderer.sortingLayerID =
                    ownerRenderer.sortingLayerID;

                spriteRenderer.sortingOrder =
                    ownerRenderer.sortingOrder + 100;
            }
            else
            {
                spriteRenderer.sortingOrder = 10000;
            }
        }

        private void EnsureCreated()
        {
            if (debugObject == null ||
                spriteRenderer == null)
            {
                CreateDebugObject();
            }
        }

        private void CreateSprite()
        {
            debugTexture =
                new Texture2D(1, 1, TextureFormat.RGBA32, false);

            debugTexture.name =
                "AttackHitboxDebugTexture";

            debugTexture.filterMode =
                FilterMode.Point;

            debugTexture.wrapMode =
                TextureWrapMode.Clamp;

            debugTexture.SetPixel(
                0,
                0,
                Color.white
            );

            debugTexture.Apply();

            debugSprite =
                Sprite.Create(
                    debugTexture,
                    new Rect(0, 0, 1, 1),
                    new Vector2(.5f, .5f),
                    1f
                );

            debugSprite.name =
                "AttackHitboxDebugSprite";
        }

        private void OnDestroy()
        {
            if (debugSprite != null)
                Destroy(debugSprite);

            if (debugTexture != null)
                Destroy(debugTexture);

            if (debugObject != null)
                Destroy(debugObject);
        }
    }
}