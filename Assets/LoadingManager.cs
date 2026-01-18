using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using UnityEngine;

public class LoadingManager : Singleton<LoadingManager>
{
    [SerializeField] GameObject loadingCanvas;
    GameObject loadingCanvasInstance;
    Slider loadingBar;

    List<AsyncOperation> operations = new();
    public override void Awake()
    {
        base.Awake();
        DontDestroyOnLoad(gameObject);

    }

    public void LoadGame()
    {
        if (loadingCanvasInstance == null)
        {
            loadingCanvasInstance = Instantiate(loadingCanvas);
            DontDestroyOnLoad(loadingCanvasInstance);
            loadingBar = loadingCanvasInstance.GetComponentInChildren<Slider>();
        }
        loadingCanvasInstance.SetActive(true);

        StartCoroutine(LoadGameRoutine());
    }

    bool IsSceneLoaded(int buildIndex)
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            if (SceneManager.GetSceneAt(i).buildIndex == buildIndex)
                return true;
        }
        return false;
    }

    IEnumerator LoadGameRoutine()
    {
        operations.Clear();


        if (!IsSceneLoaded((int)SceneIndexes.PERSISTENTGAMEPLAY_INDEX))
        {
            var persistent = SceneManager.LoadSceneAsync((int)SceneIndexes.PERSISTENTGAMEPLAY_INDEX, LoadSceneMode.Additive);
            persistent.allowSceneActivation = false;
            operations.Add(persistent);
        }

        if (!IsSceneLoaded((int)SceneIndexes.LEVEL0_INDEX))
        {
            var level = SceneManager.LoadSceneAsync((int)SceneIndexes.LEVEL0_INDEX, LoadSceneMode.Additive);
            level.allowSceneActivation = false;
            operations.Add(level);
        }


        float timer = 0f;
        float minTime = 3f;
        float visual = 0f;

        while (true)
        {
            timer += Time.deltaTime;

            float total = 0f;
            bool ready = true;

            foreach (var op in operations)
            {
                total += op.progress;
                if (op.progress < 0.9f)
                    ready = false;
            }

            float target = Mathf.Clamp01((total / operations.Count) / 0.9f);
            visual = Mathf.Lerp(visual, target, Time.deltaTime * 3f);
            loadingBar.value = visual;

            if (ready && timer >= minTime)
                break;

            yield return null;
        }

        loadingBar.value = 1f;

        foreach (var op in operations)
            op.allowSceneActivation = true;


        foreach (var op in operations)
        {
            while (!op.isDone)
                yield return null;
        }

        SceneManager.SetActiveScene(SceneManager.GetSceneByBuildIndex((int)SceneIndexes.LEVEL0_INDEX));
        SceneManager.UnloadSceneAsync((int)SceneIndexes.MAINMENU_INDEX);

        GameManager.Instance.ChangeGameState(new GameState());

        loadingCanvasInstance.SetActive(false);
    }
}
