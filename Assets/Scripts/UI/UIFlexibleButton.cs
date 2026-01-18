using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using UnityEngine.UI;
/// <summary>
/// A flexible UI button that can be configured to perform various actions
/// like changing scenes, quitting the game, or resuming gameplay.
/// </summary>

/// <summary>
/// Enum representing the different actions a UI button can perform.
/// </summary>
public enum ButtonAction
{
    ChangeState, // Load a scene and change game state
    RestartGame,
    QuitGame,    // Exit the application
    Resume,  // Resume gameplay from pause
    Settings
        
}

public class UIFlexibleButton : MonoBehaviour
{
    [SerializeField] private ButtonAction actionType;
    [SerializeField] private GameStates parameter;
    [SerializeField] private AudioClip OnClickSfx;

    private Button button;
    private void Start()
    {
        button = GetComponent<Button>();

        // Add PerformAction as a listener to the button click event
        button.onClick.AddListener(PerformAction);
        button.interactable = true;     // Ensure the button is clickable at start
    }

    /// <summary>
    /// Performs an action based on the selected ButtonAction and parameter.
    /// </summary>
    private void PerformAction()
    {
        //if (OnClickSfx != null)
        //  SoundManager.Instance.PlaySFX(OnClickSfx);

        StartCoroutine(DelayedAction());
    }

    private IEnumerator DelayedAction() 
    {
        yield return new WaitForSecondsRealtime(0.1f);   // Delay

        switch (actionType)
        {
            case ButtonAction.ChangeState: 
                switch (parameter)
                {
                    case GameStates.MainMenu:
                        TransitionManager.Instance.PlayTransitionAndLoadScene(TransitionType.FadeOut, (int)SceneIndexes.MAINMENU_INDEX);  // Change to Menu Scene with Transition
                        GameManager.Instance.ChangeGameState(new MainMenuState());
                        button.interactable = false;
                        break;

                    case GameStates.Game:
                        TransitionManager.Instance.PlayTransitionAndLoadScene(TransitionType.FadeOut, (int)SceneIndexes.PERSISTENTGAMEPLAY_INDEX);  // Esto no esta ne uso 
                        GameManager.Instance.ChangeGameState(new GameState());
                        button.interactable = false;
                        break;

                }
                break;

            case ButtonAction.QuitGame:
                Application.Quit();
                break;

            case ButtonAction.Resume:
                GameManager.Instance.ChangeGameState(new GameState());
                break;

            case ButtonAction.RestartGame:
                TransitionManager.Instance.PlayTransitionAndLoadScene(TransitionType.FadeOut, SceneManager.GetActiveScene().buildIndex);
                GameManager.Instance.ChangeGameState(new GameState());
                button.interactable = false;
                break;

            case ButtonAction.Settings:
                TransitionManager.Instance.PlayTransitionAndLoadScene(TransitionType.FadeOut, (int)SceneIndexes.SETTINGS_INDEX);  // Change to Settings Scene with Transition
                GameManager.Instance.ChangeGameState(new SettingsState());
                break;
        }
    }
}
