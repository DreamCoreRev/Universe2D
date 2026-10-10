using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Chat global, purement client/affichage : le texte tape ici est envoye
/// via Player.SendChatMessage, qui route vers PlayerChatSync (reseau) ou
/// l'affiche directement (solo). Ce composant ne touche jamais Mirror.
/// </summary>
public class ChatManager : MonoBehaviour
{
    /// <summary>
    /// Nombre de messages gardes a l'ecran -- au-dela, les plus vieux sont
    /// detruits. Pas de ScrollRect pour l'instant, donc pas de defilement
    /// pour relire plus loin en arriere.
    /// </summary>
    private const int MaxVisibleMessages = 8;

    private static ChatManager instance;

    public static ChatManager MyInstance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<ChatManager>();
            }

            return instance;
        }
    }

    [SerializeField]
    private GameObject messagePrefab;

    [SerializeField]
    private Transform messageList;

    [SerializeField]
    private InputField inputField;

    private readonly List<GameObject> activeMessages = new List<GameObject>();

    /// <summary>
    /// Vrai pendant qu'on tape dans le champ de chat -- Player.GetInput()
    /// s'en sert pour ignorer les touches de deplacement/debug/sorts
    /// pendant ce temps (sinon taper "z" ou "1" dans un message deplacerait
    /// le perso ou lancerait un sort de la barre d'action).
    /// </summary>
    public bool IsTyping
    {
        get
        {
            return inputField != null && EventSystem.current != null &&
                EventSystem.current.currentSelectedGameObject == inputField.gameObject;
        }
    }

    private void Awake()
    {
        instance = this;

        if (inputField != null)
        {
            inputField.onEndEdit.AddListener(OnInputEndEdit);
        }
    }

    private void Update()
    {
        if (inputField == null)
        {
            return;
        }

        if (!IsTyping)
        {
            // Entree ouvre le chat (le focus) quand on n'est pas deja en
            // train d'y taper -- cf OnInputEndEdit pour l'envoi proprement
            // dit, gere via l'evenement natif de l'InputField.
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                FocusInput();
            }

            return;
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            inputField.text = "";
            EventSystem.current.SetSelectedGameObject(null);
        }
    }

    private void FocusInput()
    {
        inputField.Select();
        inputField.ActivateInputField();
    }

    /// <summary>
    /// Appele par l'InputField des que son edition se termine -- par
    /// Entree sur PC, par le bouton "Retour"/"OK" du clavier virtuel sur
    /// telephone, ou par perte de focus autrement (clic ailleurs dans le
    /// jeu...).
    ///
    /// On a d'abord essaye de ne declencher l'envoi que si Entree venait
    /// d'etre pressee CETTE frame (Input.GetKeyDown(KeyCode.Return)), pour
    /// ne pas envoyer un message a moitie tape en cliquant ailleurs. Ca
    /// marche sur PC, mais le clavier virtuel Android/iOS ne genere jamais
    /// cette touche : InputField envoie directement l'evenement de
    /// soumission depuis TouchScreenKeyboard sans jamais passer par
    /// Input.GetKeyDown -- resultat, impossible d'envoyer un message au
    /// clavier tactile (voir le signalement du 2026-10-10). On envoie donc
    /// desormais chaque fois que le champ n'est pas vide, sur PC comme sur
    /// mobile ; la contrepartie est qu'un message en cours de redaction
    /// part aussi si on clique ailleurs avant de l'avoir termine.
    /// </summary>
    private void OnInputEndEdit(string text)
    {
        string message = text == null ? "" : text.Trim();
        inputField.text = "";

        if (string.IsNullOrEmpty(message))
        {
            EventSystem.current.SetSelectedGameObject(null);
            return;
        }

        if (Player.MyInstance != null)
        {
            Player.MyInstance.SendChatMessage(message);
        }

        // On garde le focus pour enchainer plusieurs messages sans avoir a
        // rouvrir le champ a chaque fois.
        FocusInput();
    }

    /// <summary>
    /// Affiche un message dans le feed. Appele depuis PlayerChatSync quand
    /// un message reseau arrive, ou directement par Player.SendChatMessage
    /// en solo.
    /// </summary>
    public void AddMessage(string senderName, string message)
    {
        if (messagePrefab == null || messageList == null)
        {
            return;
        }

        GameObject go = Instantiate(messagePrefab, messageList);
        Text t = go.GetComponent<Text>();

        if (t != null)
        {
            t.text = string.IsNullOrEmpty(senderName) ? message : string.Format("{0}: {1}", senderName, message);
        }

        activeMessages.Add(go);

        while (activeMessages.Count > MaxVisibleMessages)
        {
            GameObject oldest = activeMessages[0];
            activeMessages.RemoveAt(0);
            Destroy(oldest);
        }
    }
}
