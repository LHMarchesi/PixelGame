using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LandOfFire.BunnyStep.Editor
{
    public static class BunnyLabBuilder
    {
        [MenuItem("Land of Fire/Crear laboratorio Bunny Step")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            string folder = AssetDatabase.GenerateUniqueAssetPath("Assets/LoF_BunnyDemo");
            AssetDatabase.CreateFolder("Assets", folder.Substring("Assets/".Length));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var texture = new Texture2D(20, 40);
            texture.name = "Placeholder";
            texture.filterMode = FilterMode.Point;
            Color[] pixels = new Color[800];
            for (int y = 0; y < 40; y++)
                for (int x = 0; x < 20; x++)
                    pixels[y * 20 + x] = x >= 15 && y >= 30 && y <= 33 ? Color.black : Color.white;
            texture.SetPixels(pixels); texture.Apply();
            AssetDatabase.CreateAsset(texture, folder + "/Placeholder.asset");
            var sprite = Sprite.Create(texture, new Rect(0, 0, 20, 40), new Vector2(0.5f, 0), 25);
            sprite.name = "Fighter";
            AssetDatabase.AddObjectToAsset(sprite, texture);

            var controller = AnimatorController.CreateAnimatorControllerAtPath(folder + "/Fighter.controller");
            foreach (FighterState state in System.Enum.GetValues(typeof(FighterState)))
            {
                var clip = new AnimationClip { name = state.ToString(), frameRate = 60 };
                bool hop = state == FighterState.BunnyForward || state == FighterState.BunnyBackward;
                // Tres poses provisionales mediante squash/stretch en el hijo Sprite.
                float guardScale = state == FighterState.Guard ? 0.9f : 1f;
                var x = hop ? new AnimationCurve(new Keyframe(0, 1.12f), new Keyframe(.05f, .9f),
                    new Keyframe(.15f, 1.12f), new Keyframe(.2f, 1f)) : AnimationCurve.Constant(0, 1, 1);
                var y = hop ? new AnimationCurve(new Keyframe(0, .82f), new Keyframe(.05f, 1.08f),
                    new Keyframe(.15f, .85f), new Keyframe(.2f, 1f)) : AnimationCurve.Constant(0, 1, guardScale);
                clip.SetCurve("", typeof(Transform), "localScale.x", x);
                clip.SetCurve("", typeof(Transform), "localScale.y", y);
                AssetDatabase.CreateAsset(clip, folder + "/" + state + ".anim");
                AnimatorState animatorState = controller.layers[0].stateMachine.AddState(state.ToString());
                animatorState.motion = clip;
                if (state == FighterState.Idle) controller.layers[0].stateMachine.defaultState = animatorState;
            }

            var camera = new GameObject("Main Camera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.orthographic = true; camera.orthographicSize = 4.5f;
            camera.transform.position = new Vector3(0, 1.7f, -10);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.08f, .10f, .14f);
            camera.gameObject.AddComponent<AudioListener>();

            BunnyFighter left = CreateFighter("Player", -2, Color.cyan, sprite, controller, false);
            BunnyFighter right = CreateFighter("Opponent", 2, new Color(1, .5f, .25f), sprite, controller, true);
            var lab = new GameObject("MovementLab").AddComponent<MovementLab>();
            lab.left = left; lab.right = right;

            var ground = new GameObject("GroundVisual").AddComponent<SpriteRenderer>();
            ground.sprite = sprite; ground.color = Color.gray;
            ground.sortingOrder = -1;
            ground.transform.position = new Vector3(0, -.12f, 0);
            ground.transform.localScale = new Vector3(17.5f, .075f, 1);

            PrefabUtility.SaveAsPrefabAsset(left.gameObject, folder + "/Player.prefab");
            PrefabUtility.SaveAsPrefabAsset(right.gameObject, folder + "/Opponent.prefab");
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene, folder + "/CombatLab.unity");
            Selection.activeGameObject = left.gameObject;
            Debug.Log("CombatLab creado. Play: P1 A/D, P2 flechas. Doble tap atrás para retroceder. Assets en " + folder);
        }

        private static BunnyFighter CreateFighter(string name, float x, Color color, Sprite sprite,
            RuntimeAnimatorController controller, bool arrows)
        {
            var root = new GameObject(name);
            root.transform.position = new Vector3(x, 0, 0);
            var input = root.AddComponent<FighterInput>();
            input.useArrowKeys = arrows;
            var offset = new GameObject("VisualOffset");
            offset.transform.SetParent(root.transform, false);
            var graphic = new GameObject("Sprite");
            graphic.transform.SetParent(offset.transform, false);
            var renderer = graphic.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite; renderer.color = color;
            var animator = graphic.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            var presentation = root.AddComponent<FighterPresentation>();
            presentation.visualOffset = offset.transform;
            presentation.sprite = renderer;
            presentation.animator = animator;
            var fighter = root.AddComponent<BunnyFighter>();
            fighter.input = input; fighter.presentation = presentation;
            return fighter;
        }
    }
}
