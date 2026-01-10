using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : Singleton<GameManager>
{
    GameStateMachine gameStateMachine;

    public override void Awake()
    {
        base.Awake();
        DontDestroyOnLoad(gameObject);

        int currentBuildIndex = SceneManager.GetActiveScene().buildIndex;

        gameStateMachine = new GameStateMachine();
        switch (currentBuildIndex)
        {
            case 0:
                gameStateMachine.ChangeState(new MainMenuState());
                break;
            case 1:
                gameStateMachine.ChangeState(new GameState());
                break;
            default:
                break;
        }
    }

    private void Update()
    {
        gameStateMachine.Update();
    }

    public void ChangeGameState(IGameState state)
    {
        gameStateMachine.ChangeState(state);
    }
}


/// <summary>
/// ////////////////// GAME STATES
/// </summary>

public enum GameStates
{
    MainMenu, Pause, Game, GameOver
}
public interface IGameState
{
    public void Enter();
    public void Update();
    public void Exit();
}


public class MainMenuState : IGameState
{
    public void Enter()
    {
        Cursor.lockState = CursorLockMode.Confined;
        Cursor.visible = true;
    }

    public void Exit()
    {
    }

    public void Update()
    {
    }
}

public class GameState : IGameState
{
    public void Enter()
    {
        Cursor.lockState = CursorLockMode.Confined;
        Cursor.visible = false;
    }

    public void Exit()
    {
    }

    public void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            GameManager.Instance.ChangeGameState(new PauseState());
        }
    }
}

public class PauseState : IGameState
{
    public void Enter()
    {
        Debug.Log("Entered PauseState");
        var playerContext = GameObject.FindGameObjectWithTag("Player").GetComponent<PlayerContext>();
        playerContext.InputHandler.SetPaused(true);
      //  playerContext.PlayerUI.TogglePauseScreen(true);

        Cursor.lockState = CursorLockMode.Confined;
        Cursor.visible = true;
    }

    public void Exit()
    {
        var playerContext = GameObject.FindGameObjectWithTag("Player").GetComponent<PlayerContext>();
        playerContext.InputHandler.SetPaused(false);
        //playerContext.PlayerUI.TogglePauseScreen(false);
        Cursor.visible = false;
    }

    public void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            GameManager.Instance.ChangeGameState(new GameState());
        }
    }
}

public class GameOver : IGameState
{
    public void Enter()
    {
    }

    public void Exit()
    {
    }

    public void Update()
    {
    }
}

/// <summary>
/// ////////// GAME STATE MACHINE
/// </summary>

public class GameStateMachine
{
    public IGameState CurrentState { get => currentState; private set { } }

    private IGameState currentState;

    public void ChangeState(IGameState state)
    {
        if (currentState?.GetType() == state.GetType())
            return;

        currentState?.Exit();
        currentState = state;
        currentState?.Enter();
    }

    public void Update()
    {
        currentState?.Update();
    }
}