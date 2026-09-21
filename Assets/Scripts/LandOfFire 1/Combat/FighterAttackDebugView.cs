// Land of Fire · Debug visual de hitbox.
//
// No participa en física ni combate.
// Usa exactamente hitboxOffset y hitboxSize del AttackMoveData.

using UnityEngine;

namespace LandOfFire.BunnyStep
{
    [DisallowMultipleComponent]
    public sealed class FighterAttackDebugView :
        MonoBehaviour
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

            Vector2 offset =
                attack.hitboxOffset;

            debugObject.transform.localPosition =
                new Vector3(
                    offset.x * facing,
                    offset.y,
                    0f);

            debugObject.transform.localRotation =
                Quaternion.identity;

            debugObject.transform.localScale =
                new Vector3(
                    Mathf.Max(
                        .01f,
                        attack.hitboxSize.x),
                    Mathf.Max(
                        .01f,
                        attack.hitboxSize.y),
                    1f);

            spriteRenderer.color =
                color;

            spriteRenderer.enabled =
                true;
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
                new GameObject(
                    "AttackHitboxDebug");

            debugObject.transform.SetParent(
                transform,
                false);

            spriteRenderer =
                debugObject.AddComponent<
                    SpriteRenderer>();

            CreateSprite();

            spriteRenderer.sprite =
                debugSprite;

            SpriteRenderer ownerRenderer =
                GetComponentInChildren<
                    SpriteRenderer>();

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
                spriteRenderer.sortingOrder =
                    10000;
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
                new Texture2D(
                    1,
                    1,
                    TextureFormat.RGBA32,
                    false);

            debugTexture.filterMode =
                FilterMode.Point;

            debugTexture.wrapMode =
                TextureWrapMode.Clamp;

            debugTexture.SetPixel(
                0,
                0,
                Color.white);

            debugTexture.Apply();

            debugSprite =
                Sprite.Create(
                    debugTexture,
                    new Rect(
                        0,
                        0,
                        1,
                        1),
                    new Vector2(
                        .5f,
                        .5f),
                    1f);
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