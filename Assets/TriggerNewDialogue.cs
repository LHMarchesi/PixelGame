using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TriggerNewDialogue : MonoBehaviour
{
   [SerializeField]private TextAsset _InkJsonFile;

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            ScriptReader.Instance.LoadStory(_InkJsonFile);
        }
        Destroy(gameObject);
    }
}
