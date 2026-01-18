using UnityEngine;

public class MainMenuManager : MonoBehaviour
{
 
    [SerializeField] GameObject[] objectsToHide;



    public void OnStartGameButton()
    {
        HideMenu();
        LoadingManager.Instance.LoadGame();
    }

    private void HideMenu()
    {
        for (int i = 0; i < objectsToHide.Length; i++)
        {
            objectsToHide[i].SetActive(false);
        }
    }
}

public enum SceneIndexes
{
    MAINMENU_INDEX = 0,
    SETTINGS_INDEX = 1,
    PERSISTENTGAMEPLAY_INDEX = 2,
    LEVEL0_INDEX = 3,
}
