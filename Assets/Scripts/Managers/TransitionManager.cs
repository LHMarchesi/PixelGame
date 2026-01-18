using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public enum TransitionType
{
    FadeIn,
    FadeOut
}

public class TransitionManager : Singleton<TransitionManager>
{
    [Header("Animator Settings")]
    [SerializeField] private bool playOnStart = true;
    [SerializeField] private TransitionType startTransition = TransitionType.FadeIn;
    private Animator animator;
    private string currentAnimation;

    private bool isTransitioning;


    public override void Awake()
    {
        base.Awake();
        DontDestroyOnLoad(gameObject);
        animator = GetComponentInChildren<Animator>();
    }
    public void ChangeAnimationState(string newState)
    {
        // STOP THE SAME ANIMATION FROM INTERRUPTING WITH ITSELF //
        if (currentAnimation == newState) return;

        // PLAY THE ANIMATION //
        animator.Play(newState);
        currentAnimation = newState;
    }

    private void Start()
    {
        if (playOnStart)
            PlayTransition(startTransition);
    }

    /// <summary>
    /// Llama una animaci�n de transici�n por su tipo.
    /// </summary>
    public void PlayTransition(TransitionType type)
    {
        if (isTransitioning)
            return;

        isTransitioning = true;
        ChangeAnimationState(type.ToString());
        StartCoroutine(ResetTransitionFlag());
    }

    IEnumerator ResetTransitionFlag()
    {
        yield return new WaitForSeconds(animator.GetCurrentAnimatorStateInfo(0).length);
        isTransitioning = false;
    }
    public void PlayTransitionAndLoadScene(TransitionType type, int sceneIndex)
    {
        StartCoroutine(PlayTransitionAndLoadSceneCoroutine(type, sceneIndex));
    }

    public IEnumerator PlayTransitionAndLoadSceneCoroutine(TransitionType type, int sceneIndex)
    {
        if (isTransitioning)
            yield break;

        isTransitioning = true;

        ChangeAnimationState(type.ToString());
        yield return null;

        float fadeOutDuration = (animator.GetCurrentAnimatorStateInfo(0).length);
        yield return new WaitForSeconds(fadeOutDuration);

        //  Pausar recepci�n de eventos antes de cargar

        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneIndex);
        asyncLoad.allowSceneActivation = false;

        while (asyncLoad.progress < 0.9f)
            yield return null;

        asyncLoad.allowSceneActivation = true;

        //  Reanudar eventos despu�s de cargar

        ChangeAnimationState(TransitionType.FadeIn.ToString());
        yield return new WaitForSeconds(animator.GetCurrentAnimatorStateInfo(0).length);

        yield return new WaitForSeconds(0.1f);
        isTransitioning = false;
    }
}
