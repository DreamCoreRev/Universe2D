using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Chat global, purement client/affichage : le texte tape ici est envoye
/// via Player.SendChatMessage, qui route vers PlayerChatSync (reseau) ou
/// l'affiche directement (solo). Ce composant ne touche jamais Mirror.
///
/// La zone de defilement (Viewport/Content/ScrollRect), les deux boutons
/// fleche et le texte qu'ils affichent sont construits ici en code plutot
/// que poses a la main dans Demo.unity -- comme TouchJoystick.cs, voir ce
/// fichier -- pour ne jamais dependre d'un guid de composant Unity
/// (ScrollRect/RectMask2D/ContentSizeFitter/VerticalLayoutGroup/Button)
/// qu'on ne peut pas verifier depuis ici sans ouvrir l'editeur : un guid
/// recopie de travers casserait silencieusement la scene. AddComponent
/// resout le type directement via le compilateur, donc aucun risque.
/// </summary>
public class ChatManager : MonoBehaviour
{
    /// <summary>
    /// Nombre de messages gardes en memoire (visibles en remontant le fil
    /// avec la fleche du haut) -- au-dela, les plus vieux sont detruits.
    /// </summary>
    private const int MaxHistory = 60;

    /// <summary>
    /// Fraction de la hauteur defilable parcourue a chaque clic sur une
    /// fleche (0 a 1, voir ScrollRect.verticalNormalizedPosition).
    /// </summary>
    private const float ArrowScrollStep = 0.22f;

    private const float ArrowButtonSize = 20f;
    private const float ArrowColumnWidth = ArrowButtonSize + 4f;

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
    private InputField inputField;

    private RectTransform content;
    private RectTransform viewportRectRef;
    private ScrollRect scrollRect;
    private readonly List<GameObject> activeMessages = new List<GameObject>();

    /// <summary>
    /// Vrai tant qu'on n'a pas remonte manuellement le fil : un nouveau
    /// message recale alors automatiquement la vue tout en bas. Des qu'on
    /// remonte (fleche du haut ou molette/glisser), on arrete de forcer la
    /// vue en bas pour ne pas arracher la lecture en plein milieu d'un
    /// vieux message -- exactement comme un client de chat classique.
    /// </summary>
    private bool stickToBottom = true;

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

        BuildScrollArea();
        WarmUpFontAtlas();

        if (inputField != null)
        {
            inputField.onEndEdit.AddListener(OnInputEndEdit);
        }
    }

    /// <summary>
    /// Bug trouve via les logs [DEBUG-CHAT] : le nom du joueur (ex.
    /// "Aurora: ") disparaissait du texte affiche alors que Text.text
    /// contenait bien la chaine complete (confirme par characterCountVisible
    /// == text.Length, donc pas une troncature du TextGenerator). La police
    /// "Arial.ttf" integree a Unity est une police dynamique : chaque
    /// caractere est rasterise a la demande dans une texture partagee
    /// (Font.RequestCharactersInTexture). Quand un message utilise pour la
    /// premiere fois des caracteres jamais affiches ailleurs dans l'UI
    /// (majuscules, ":", etc.), le maillage du texte peut se generer avant
    /// que la texture ne soit reconstruite avec ces caracteres -- ils
    /// restent alors invisibles (glyphes vides) meme si le texte est
    /// correct. On force ici, une seule fois au demarrage et bien avant le
    /// premier message, le chargement de tout le jeu de caracteres probable
    /// (alphabet, chiffres, ponctuation, accents francais) pour eliminer ce
    /// decalage.
    /// </summary>
    private void WarmUpFontAtlas()
    {
        Font font = Resources.GetBuiltinResource<Font>("Arial.ttf");

        if (font == null)
        {
            return;
        }

        const string charset = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789 " +
            ".,!?;:'\"-_()[]/\\+=*@#&%" +
            "\u00e0\u00e2\u00e4\u00e7\u00e8\u00e9\u00ea\u00eb\u00ee\u00ef\u00f4\u00f6\u00f9\u00fb\u00fc\u00c0\u00c7\u00c9\u00c8\u00ca";

        font.RequestCharactersInTexture(charset, 14, FontStyle.Normal);
    }

    /// <summary>
    /// Construit, sous CE GameObject (ChatWindow, voir Demo.unity -- il ne
    /// porte plus que son image de fond), la zone de defilement des
    /// messages et les deux boutons fleche. Appele une seule fois, au
    /// lancement.
    /// </summary>
    private void BuildScrollArea()
    {
        RectTransform root = (RectTransform)transform;

        GameObject viewportGO = new GameObject("Viewport", typeof(RectTransform));
        viewportGO.transform.SetParent(root, false);
        RectTransform viewportRect = viewportGO.GetComponent<RectTransform>();
        viewportRect.anchorMin = new Vector2(0f, 0f);
        viewportRect.anchorMax = new Vector2(1f, 1f);
        viewportRect.pivot = new Vector2(0f, 1f);
        viewportRect.offsetMin = new Vector2(2f, 2f);
        viewportRect.offsetMax = new Vector2(-ArrowColumnWidth, -2f);
        viewportGO.AddComponent<RectMask2D>();
        viewportRectRef = viewportRect;

        GameObject contentGO = new GameObject("Content", typeof(RectTransform));
        contentGO.transform.SetParent(viewportRect, false);
        content = contentGO.GetComponent<RectTransform>();
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;

        VerticalLayoutGroup layout = contentGO.AddComponent<VerticalLayoutGroup>();
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        layout.spacing = 2f;
        layout.padding = new RectOffset(2, 2, 2, 2);

        ContentSizeFitter fitter = contentGO.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        scrollRect = gameObject.AddComponent<ScrollRect>();
        scrollRect.content = content;
        scrollRect.viewport = viewportRect;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 18f;
        scrollRect.onValueChanged.AddListener(OnScrollValueChanged);

        BuildArrowButton("ScrollUpButton", "▲", new Vector2(-2f, -2f), ScrollUp);
        BuildArrowButton("ScrollDownButton", "▼", new Vector2(-2f, -2f - ArrowButtonSize - 2f), ScrollDown);
    }

    private void BuildArrowButton(string name, string label, Vector2 anchoredPosition, UnityEngine.Events.UnityAction onClick)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(transform, false);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(1f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 1f);
        rt.sizeDelta = new Vector2(ArrowButtonSize, ArrowButtonSize);
        rt.anchoredPosition = anchoredPosition;

        Image image = go.AddComponent<Image>();
        image.color = new Color(1f, 1f, 1f, 0.25f);

        Button button = go.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(onClick);

        GameObject textGO = new GameObject("Label", typeof(RectTransform));
        textGO.transform.SetParent(go.transform, false);
        RectTransform textRect = textGO.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        Text text = textGO.AddComponent<Text>();
        text.text = label;
        text.alignment = TextAnchor.MiddleCenter;
        text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        text.fontSize = 12;
        text.color = Color.white;
        text.raycastTarget = false;
    }

    private void OnScrollValueChanged(Vector2 position)
    {
        // 0 = tout en bas (voir ScrollRect.verticalNormalizedPosition). On
        // remet le "suivi automatique" des que l'utilisateur revient en
        // bas lui-meme (molette, glisser ou fleche du bas), pas seulement
        // via ScrollDown().
        stickToBottom = scrollRect.verticalNormalizedPosition <= 0.02f;
    }

    private void ScrollUp()
    {
        stickToBottom = false;
        scrollRect.verticalNormalizedPosition = Mathf.Clamp01(scrollRect.verticalNormalizedPosition + ArrowScrollStep);
    }

    private void ScrollDown()
    {
        float next = scrollRect.verticalNormalizedPosition - ArrowScrollStep;

        if (next <= 0.02f)
        {
            next = 0f;
            stickToBottom = true;
        }

        scrollRect.verticalNormalizedPosition = Mathf.Clamp01(next);
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
    /// clavier tactile. On envoie donc desormais chaque fois que le champ
    /// n'est pas vide, sur PC comme sur mobile ; la contrepartie est qu'un
    /// message en cours de redaction part aussi si on clique ailleurs
    /// avant de l'avoir termine.
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
        Debug.Log($"[DEBUG-CHAT] AddMessage recu: senderName='{senderName}' message='{message}'");

        if (messagePrefab == null || content == null)
        {
            return;
        }

        GameObject go = Instantiate(messagePrefab, content);
        Text t = go.GetComponent<Text>();

        if (t != null)
        {
            t.text = string.IsNullOrEmpty(senderName) ? message : string.Format("{0}: {1}", senderName, message);
            Debug.Log($"[DEBUG-CHAT] Text.text assigne = '{t.text}' (go.name={go.name}, t.GetInstanceID()={t.GetInstanceID()})");
            Debug.Log($"[DEBUG-CHAT] Avant layout: text.Length={t.text.Length} rect={t.rectTransform.rect} anchoredPos={t.rectTransform.anchoredPosition} font={(t.font == null ? "NULL" : t.font.name)}");
        }
        else
        {
            Debug.Log("[DEBUG-CHAT] AddMessage: go.GetComponent<Text>() == null !");
        }

        activeMessages.Add(go);

        while (activeMessages.Count > MaxHistory)
        {
            GameObject oldest = activeMessages[0];
            activeMessages.RemoveAt(0);
            Destroy(oldest);
        }

        if (stickToBottom)
        {
            // Le layout ne s'est pas encore recalcule cette frame (le
            // nouveau Text vient d'etre instancie) : on force la mise a
            // jour avant de recaler le defilement, sinon on recale sur la
            // hauteur d'AVANT ce message.
            Canvas.ForceUpdateCanvases();
            scrollRect.verticalNormalizedPosition = 0f;

            if (t != null)
            {
                int visible = t.cachedTextGenerator != null ? t.cachedTextGenerator.characterCountVisible : -1;
                Debug.Log($"[DEBUG-CHAT] Apres layout: text.Length={t.text.Length} characterCountVisible={visible} rect={t.rectTransform.rect} anchoredPos={t.rectTransform.anchoredPosition}");

                RectTransform root = (RectTransform)transform;
                Vector3[] textCorners = new Vector3[4];
                t.rectTransform.GetWorldCorners(textCorners);
                Vector3[] viewportCorners = new Vector3[4];

                if (viewportRectRef != null)
                {
                    viewportRectRef.GetWorldCorners(viewportCorners);
                }

                Debug.Log($"[DEBUG-CHAT] Largeurs: ChatWindow={root.rect.width} viewport={(viewportRectRef != null ? viewportRectRef.rect.width.ToString() : "NULL")} content={(content != null ? content.rect.width.ToString() : "NULL")}");
                Debug.Log($"[DEBUG-CHAT] Coins monde texte: bas-gauche={textCorners[0]} haut-droit={textCorners[2]}");

                if (viewportRectRef != null)
                {
                    Debug.Log($"[DEBUG-CHAT] Coins monde viewport: bas-gauche={viewportCorners[0]} haut-droit={viewportCorners[2]}");
                }
            }
        }
    }
}
