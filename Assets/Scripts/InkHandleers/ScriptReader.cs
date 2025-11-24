using System.Collections;
using Ink.Runtime;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

public class ScriptReader : MonoBehaviour
{
    public static ScriptReader Instance;
    
    [SerializeField]
    private Story _StoryScript;

    public GameObject dialoguePanel;
    public Image characterIcon;
    public TMP_Text dialogueText;
    public TMP_Text nameText;

    private PlayerContext playerContext;
    private bool isTextDisplaying = true;
    public int charsToPlaySound = 4;
    public float chTime;

    private void OnEnable()
    {
        playerContext = GameObject.FindGameObjectWithTag("Player").GetComponent<PlayerContext>();
    }

    private void Awake()
    {
        // Singleton pattern
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (isTextDisplaying)
            {
                // Si el texto se está mostrando, setea isTextDisplaying a false para saltear al final
                isTextDisplaying = false;
            }
            else
            {
                // Si no se está mostrando, comienza a mostrar el siguiente texto
                StartCoroutine(DisplayNextLine());
            }
        }
    }

    public void LoadStory(TextAsset textAsset)
    {
        playerContext.InputHandler.SetPaused(true);
        dialoguePanel.SetActive(true);

        _StoryScript = new Story(textAsset.text);

        _StoryScript.BindExternalFunction("Name", (string charName) => ChangeName(charName));
        _StoryScript.BindExternalFunction("CharacterIcon", (string charName) => ChangeCharacterIcon(charName));

        StartCoroutine(DisplayNextLine());

    }

    private IEnumerator DisplayNextLine()
    {
        if (_StoryScript.canContinue) // Checking if there is content to go through
        {
            dialogueText.text = string.Empty; //Displays new text
            string text = _StoryScript.Continue(); //Gets Next Line
            text = text?.Trim(); //Removes White space from the text


            isTextDisplaying = true;
            
            int charIndex = 0;
            foreach (char ch in text)
            {
                if (!isTextDisplaying) // Si se presiona espacio otra vez, muestra todo el texto
                {
                    dialogueText.text = text;
                    break;
                }

                if (charIndex % charsToPlaySound == 0)
                {
                    SoundManager.Instance.PlaySound("sfxWriting");
                }
                charIndex++;
                dialogueText.text += ch;
                yield return new WaitForSecondsRealtime(chTime);
            }
            isTextDisplaying = false; // Termina de mostrar el texto
        }
        else
        {
            playerContext.InputHandler.SetPaused(false);
            dialoguePanel.SetActive(false);
        }
    }

    public void ChangeName(string name)
    {
        string SpeakerName = name;

        nameText.text = SpeakerName;
    }

    public void ChangeCharacterIcon(string charName)
    {
        characterIcon.sprite = Resources.Load<Sprite>("CharacterIcons/" + charName);
    }
}
