using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Ecran de selection de personnage, affiche dans sa propre scene
/// (CharacterSelect.unity) entre Login.unity et Demo.unity. Parle au meme
/// serveur HTTP/JSON que LoginManager (Server/AuthServer) pour
/// lister/creer/supprimer les personnages du compte connecte (voir
/// Session.Username), puis fixe Session.SelectedCharacter* et lance
/// Mirror.NetworkManager.singleton.StartClient() -- le NetworkManager
/// (persiste depuis Login.unity via DontDestroyOnLoad) charge alors lui
/// meme Demo.unity (onlineScene), exactement comme avant pour LoginManager.
///
/// Toute l'UI est construite au runtime (meme technique que GroupUIManager)
/// : cette scene ne contient qu'une Camera, un EventSystem et ce script.
/// </summary>
public class CharacterSelectManager : MonoBehaviour
{
    private const string ServerUrl = "http://192.168.42.90:8080";
    private const int MaxCharacters = 5;

    // Classes jouables aujourd'hui cote client. Le Guerrier n'a pas encore
    // ses assets dans le projet -- bouton visible mais grise, pas d'appel
    // serveur possible pour cette classe (le serveur la refuserait de toute
    // facon, voir AuthServer/Program.cs availableClasses).
    private const string MageClass = "Mage";
    private const string WarriorClass = "Guerrier";

    // Meme palette bronze/or que GroupUIManager, pour un theme coherent.
    private static readonly Color BorderColor = new Color(0.22f, 0.14f, 0.03f, 0.95f);
    private static readonly Color PanelColor = new Color(0.07f, 0.06f, 0.08f, 0.96f);
    private static readonly Color SlotEmptyColor = new Color(0.14f, 0.12f, 0.1f, 0.9f);
    private static readonly Color SlotFilledColor = new Color(0.18f, 0.15f, 0.08f, 0.95f);
    private static readonly Color SlotSelectedColor = new Color(0.30f, 0.22f, 0.07f, 1f);
    private static readonly Color GoldFill = new Color(0.82f, 0.62f, 0.16f, 1f);
    private static readonly Color GoldFillHover = new Color(0.90f, 0.70f, 0.22f, 1f);
    private static readonly Color GoldFillPressed = new Color(0.68f, 0.50f, 0.10f, 1f);
    private static readonly Color DisabledGray = new Color(0.32f, 0.32f, 0.32f, 1f);
    private static readonly Color GoldTextColor = new Color(0.95f, 0.82f, 0.45f, 1f);
    private static readonly Color DarkTextColor = new Color(0.20f, 0.11f, 0.02f, 1f);
    private static readonly Color ErrorTextColor = new Color(0.85f, 0.3f, 0.25f, 1f);

    private sealed class CharacterSlotUI
    {
        public GameObject Root;
        public Image Background;
        public Text NameText;
        public Text ClassText;
        public Text EmptyHintText;
        public Button Button;
    }

    [Serializable]
    private sealed class CharacterDto
    {
        public int id;
        public string name;
        public string characterClass;
        public int saveSlotIndex;
    }

    [Serializable]
    private sealed class CharacterListResponse
    {
        public bool success;
        public string message;
        public CharacterDto[] characters;
    }

    [Serializable]
    private sealed class CharacterCreateResponse
    {
        public bool success;
        public string message;
        public CharacterDto character;
    }

    [Serializable]
    private sealed class SimpleResponse
    {
        public bool success;
        public string message;
    }

    [Serializable]
    private sealed class ListRequestPayload
    {
        public string username;
    }

    [Serializable]
    private sealed class CreateRequestPayload
    {
        public string username;
        public string name;
        public string characterClass;
    }

    [Serializable]
    private sealed class DeleteRequestPayload
    {
        public string username;
        public int characterId;
    }

    private RectTransform canvasRect;
    private readonly CharacterSlotUI[] slots = new CharacterSlotUI[MaxCharacters];
    private readonly CharacterDto[] characters = new CharacterDto[MaxCharacters];

    private int selectedIndex = -1;

    private Text statusText;
    private Button playButton;
    private Button deleteButton;

    private GameObject createPanelGO;
    private InputField nameInput;
    private Button mageClassButton;
    private Image mageClassBackground;
    private Button confirmCreateButton;
    private Text createStatusText;

    private GameObject deleteConfirmGO;
    private Text deleteConfirmText;

    private bool busy;

    private void Awake()
    {
        BuildUI();
    }

    private void Start()
    {
        RefreshCharacterList();
    }

    // ------------------------------------------------------------------
    // Construction de l'UI
    // ------------------------------------------------------------------

    private void BuildUI()
    {
        GameObject canvasGO = new GameObject("CharacterSelectCanvas");
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(800, 600);
        scaler.matchWidthOrHeight = 1f;

        canvasGO.AddComponent<GraphicRaycaster>();
        canvasRect = canvasGO.GetComponent<RectTransform>();

        Image background = CreateImage("Background", canvasRect, new Color(0.05f, 0.05f, 0.06f, 1f));
        StretchFull(background.rectTransform);

        Text title = CreateText("Title", canvasRect, "SELECTION DU PERSONNAGE", 28, GoldTextColor, FontStyle.Bold, TextAnchor.MiddleCenter);
        SetAnchoredRect(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -40), new Vector2(560, 50));

        BuildSlotRow(canvasRect);
        BuildActionButtons(canvasRect);
        BuildCreatePanel(canvasRect);
        BuildDeleteConfirmPanel(canvasRect);
        BuildLogoutButton(canvasRect);

        statusText = CreateText("StatusText", canvasRect, "", 16, ErrorTextColor, FontStyle.Normal, TextAnchor.MiddleCenter);
        SetAnchoredRect(statusText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 40), new Vector2(700, 30));
    }

    private void BuildSlotRow(RectTransform parent)
    {
        const float slotWidth = 136f;
        const float slotHeight = 190f;
        const float gap = 14f;
        float totalWidth = MaxCharacters * slotWidth + (MaxCharacters - 1) * gap;
        float startX = -totalWidth / 2f + slotWidth / 2f;

        for (int i = 0; i < MaxCharacters; i++)
        {
            int capturedIndex = i;

            GameObject slotGO = new GameObject("Slot" + i);
            RectTransform slotRect = slotGO.AddComponent<RectTransform>();
            slotRect.SetParent(parent, false);
            slotRect.anchorMin = new Vector2(0.5f, 0.5f);
            slotRect.anchorMax = new Vector2(0.5f, 0.5f);
            slotRect.sizeDelta = new Vector2(slotWidth, slotHeight);
            slotRect.anchoredPosition = new Vector2(startX + i * (slotWidth + gap), 10f);

            Image bg = slotGO.AddComponent<Image>();
            bg.color = SlotEmptyColor;

            Outline outline = slotGO.AddComponent<Outline>();
            outline.effectColor = BorderColor;
            outline.effectDistance = new Vector2(2, -2);

            Button button = slotGO.AddComponent<Button>();
            button.targetGraphic = bg;
            ApplyButtonColors(button);
            button.onClick.AddListener(() => OnSlotClicked(capturedIndex));

            Text nameText = CreateText("NameText", slotRect, "", 15, GoldTextColor, FontStyle.Bold, TextAnchor.MiddleCenter);
            SetAnchoredRect(nameText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0, -32), new Vector2(0, 28));

            Text classText = CreateText("ClassText", slotRect, "", 12, new Color(0.8f, 0.8f, 0.8f, 1f), FontStyle.Normal, TextAnchor.MiddleCenter);
            SetAnchoredRect(classText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0, -58), new Vector2(0, 22));

            Text emptyHint = CreateText("EmptyHint", slotRect, "+\n\nNouveau\npersonnage", 14, new Color(0.6f, 0.6f, 0.6f, 1f), FontStyle.Bold, TextAnchor.MiddleCenter);
            StretchFull(emptyHint.rectTransform);

            slots[i] = new CharacterSlotUI
            {
                Root = slotGO,
                Background = bg,
                NameText = nameText,
                ClassText = classText,
                EmptyHintText = emptyHint,
                Button = button
            };
        }
    }

    private void BuildActionButtons(RectTransform parent)
    {
        playButton = CreateThemedButton(parent, "JOUER", new Vector2(-90, -130), new Vector2(160, 46));
        playButton.onClick.AddListener(OnPlayClicked);
        playButton.interactable = false;

        deleteButton = CreateThemedButton(parent, "SUPPRIMER", new Vector2(90, -130), new Vector2(160, 46));
        deleteButton.onClick.AddListener(OnDeleteClicked);
        deleteButton.interactable = false;
    }

    private void BuildCreatePanel(RectTransform parent)
    {
        createPanelGO = CreatePanel("CreatePanel", parent, new Vector2(360, 260), out RectTransform panelRect);
        createPanelGO.SetActive(false);

        Text title = CreateText("Title", panelRect, "NOUVEAU PERSONNAGE", 18, GoldTextColor, FontStyle.Bold, TextAnchor.MiddleCenter);
        SetAnchoredRect(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -16), new Vector2(320, 30));

        nameInput = CreateInputField(panelRect, new Vector2(0, 55), new Vector2(300, 36), "Nom du personnage");

        // Choix de la classe : Mage cliquable, Guerrier visible mais grise
        // (pas encore d'assets dans le projet -- voir CharacterRelated).
        mageClassButton = CreateThemedButton(panelRect, "MAGE", new Vector2(-75, 0), new Vector2(130, 40));
        mageClassBackground = mageClassButton.GetComponent<Image>();
        mageClassButton.onClick.AddListener(() => SelectCreateClass(MageClass));

        Button warriorButton = CreateThemedButton(panelRect, "GUERRIER\n(bientot)", new Vector2(75, 0), new Vector2(130, 40));
        Image warriorBg = warriorButton.GetComponent<Image>();
        warriorBg.color = DisabledGray;
        warriorButton.interactable = false;
        Text warriorLabel = warriorButton.GetComponentInChildren<Text>();
        warriorLabel.fontSize = 11;
        warriorLabel.color = new Color(0.7f, 0.7f, 0.7f, 1f);

        createStatusText = CreateText("CreateStatus", panelRect, "", 13, ErrorTextColor, FontStyle.Normal, TextAnchor.MiddleCenter);
        SetAnchoredRect(createStatusText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 56), new Vector2(320, 24));

        confirmCreateButton = CreateThemedButton(panelRect, "CREER", new Vector2(-75, -92), new Vector2(130, 40));
        confirmCreateButton.onClick.AddListener(OnConfirmCreateClicked);

        Button cancelButton = CreateThemedButton(panelRect, "ANNULER", new Vector2(75, -92), new Vector2(130, 40));
        cancelButton.onClick.AddListener(CloseCreatePanel);

        SelectCreateClass(MageClass);
    }

    private void BuildLogoutButton(RectTransform parent)
    {
        // Bouton "Deconnexion" facon WoW, coin superieur droit : termine la
        // session du COMPTE (retour a Login) -- a ne pas confondre avec le
        // bouton "Deconnexion" du menu Echap en jeu (voir UIManager.Logout),
        // qui lui ne fait que revenir a CET ecran, compte toujours connecte.
        Button logoutButton = CreateThemedButton(parent, "DECONNEXION", Vector2.zero, new Vector2(140, 34));
        SetAnchoredRect(logoutButton.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-82, -24), new Vector2(140, 34));

        Text label = logoutButton.GetComponentInChildren<Text>();
        label.fontSize = 12;

        logoutButton.onClick.AddListener(OnLogoutClicked);
    }

    private void OnLogoutClicked()
    {
        if (busy)
        {
            return;
        }

        if (Mirror.NetworkManager.singleton != null && Mirror.NetworkClient.active)
        {
            Mirror.NetworkManager.singleton.StopClient();
        }

        Session.ClearAll();
        SceneManager.LoadScene("Login");
    }

    private void BuildDeleteConfirmPanel(RectTransform parent)
    {
        deleteConfirmGO = CreatePanel("DeleteConfirmPanel", parent, new Vector2(340, 170), out RectTransform panelRect);
        deleteConfirmGO.SetActive(false);

        deleteConfirmText = CreateText("Message", panelRect, "", 15, GoldTextColor, FontStyle.Bold, TextAnchor.MiddleCenter);
        SetAnchoredRect(deleteConfirmText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -50), new Vector2(300, 70));

        Button confirmButton = CreateThemedButton(panelRect, "SUPPRIMER", new Vector2(-75, -55), new Vector2(130, 40));
        confirmButton.onClick.AddListener(OnConfirmDeleteClicked);

        Button cancelButton = CreateThemedButton(panelRect, "ANNULER", new Vector2(75, -55), new Vector2(130, 40));
        cancelButton.onClick.AddListener(() => deleteConfirmGO.SetActive(false));
    }

    // ------------------------------------------------------------------
    // Logique de selection / creation / suppression
    // ------------------------------------------------------------------

    private void OnSlotClicked(int index)
    {
        if (busy)
        {
            return;
        }

        if (characters[index] == null)
        {
            OpenCreatePanel();
            return;
        }

        selectedIndex = index;
        UpdateSlotVisuals();
        playButton.interactable = true;
        deleteButton.interactable = true;
    }

    private void OpenCreatePanel()
    {
        nameInput.text = "";
        createStatusText.text = "";
        SelectCreateClass(MageClass);
        createPanelGO.SetActive(true);
    }

    private void CloseCreatePanel()
    {
        createPanelGO.SetActive(false);
    }

    private string pendingCreateClass = MageClass;

    private void SelectCreateClass(string characterClass)
    {
        pendingCreateClass = characterClass;
        mageClassBackground.color = characterClass == MageClass ? GoldFillPressed : GoldFill;
    }

    private void OnPlayClicked()
    {
        if (selectedIndex < 0 || characters[selectedIndex] == null || busy)
        {
            return;
        }

        EnterGameWithCharacter(characters[selectedIndex], isNew: false);
    }

    private void OnDeleteClicked()
    {
        if (selectedIndex < 0 || characters[selectedIndex] == null)
        {
            return;
        }

        deleteConfirmText.text = "Supprimer \"" + characters[selectedIndex].name + "\" ?\nCette action est definitive.";
        deleteConfirmGO.SetActive(true);
    }

    private void OnConfirmDeleteClicked()
    {
        deleteConfirmGO.SetActive(false);

        if (selectedIndex < 0 || characters[selectedIndex] == null || busy)
        {
            return;
        }

        StartCoroutine(DeleteCharacterRequest(characters[selectedIndex].id));
    }

    private void OnConfirmCreateClicked()
    {
        if (busy)
        {
            return;
        }

        string name = nameInput.text != null ? nameInput.text.Trim() : "";

        if (name.Length < 2 || name.Length > 24)
        {
            createStatusText.text = "Le nom doit contenir entre 2 et 24 caracteres.";
            return;
        }

        StartCoroutine(CreateCharacterRequest(name, pendingCreateClass));
    }

    private void UpdateSlotVisuals()
    {
        for (int i = 0; i < MaxCharacters; i++)
        {
            CharacterDto character = characters[i];
            CharacterSlotUI slot = slots[i];

            bool hasCharacter = character != null;
            slot.NameText.gameObject.SetActive(hasCharacter);
            slot.ClassText.gameObject.SetActive(hasCharacter);
            slot.EmptyHintText.gameObject.SetActive(!hasCharacter);

            if (hasCharacter)
            {
                slot.NameText.text = character.name;
                slot.ClassText.text = character.characterClass;
            }

            if (i == selectedIndex)
            {
                slot.Background.color = SlotSelectedColor;
            }
            else
            {
                slot.Background.color = hasCharacter ? SlotFilledColor : SlotEmptyColor;
            }
        }
    }

    private void EnterGameWithCharacter(CharacterDto character, bool isNew)
    {
        Session.SelectedCharacterId = character.id;
        Session.SelectedCharacterName = character.name;
        Session.SelectedCharacterClass = character.characterClass;
        Session.SelectedSaveSlotIndex = character.saveSlotIndex;
        Session.IsNewCharacter = isNew;

        if (Mirror.NetworkManager.singleton != null)
        {
            Mirror.NetworkManager.singleton.StartClient();
        }
        else
        {
            // Filet de securite si jamais cette scene est testee seule,
            // sans NetworkManager (ne devrait pas arriver en jeu normal).
            SceneManager.LoadScene("Demo");
        }
    }

    // ------------------------------------------------------------------
    // Appels reseau (AuthServer)
    // ------------------------------------------------------------------

    private void RefreshCharacterList()
    {
        StartCoroutine(ListCharactersRequest());
    }

    private IEnumerator ListCharactersRequest()
    {
        SetBusy(true, "Chargement des personnages...");

        ListRequestPayload payload = new ListRequestPayload { username = Session.Username };
        string json = JsonUtility.ToJson(payload);

        using (UnityWebRequest req = BuildPostRequest("/characters/list", json))
        {
            yield return req.SendWebRequest();

            CharacterListResponse response = ParseResponse<CharacterListResponse>(req);

            if (response != null && response.success)
            {
                Array.Clear(characters, 0, characters.Length);

                if (response.characters != null)
                {
                    foreach (CharacterDto dto in response.characters)
                    {
                        if (dto.saveSlotIndex >= 0 && dto.saveSlotIndex < MaxCharacters)
                        {
                            characters[dto.saveSlotIndex] = dto;
                        }
                    }
                }

                selectedIndex = -1;
                playButton.interactable = false;
                deleteButton.interactable = false;
                UpdateSlotVisuals();
                SetBusy(false, "");
            }
            else
            {
                SetBusy(false, DescribeNetworkFailure(req, response));
            }
        }
    }

    private IEnumerator CreateCharacterRequest(string name, string characterClass)
    {
        SetBusy(true, "");
        createStatusText.text = "Creation en cours...";

        CreateRequestPayload payload = new CreateRequestPayload
        {
            username = Session.Username,
            name = name,
            characterClass = characterClass
        };
        string json = JsonUtility.ToJson(payload);

        using (UnityWebRequest req = BuildPostRequest("/characters/create", json))
        {
            yield return req.SendWebRequest();

            CharacterCreateResponse response = ParseResponse<CharacterCreateResponse>(req);

            if (response != null && response.success && response.character != null)
            {
                SetBusy(false, "");
                CloseCreatePanel();
                EnterGameWithCharacter(response.character, isNew: true);
            }
            else
            {
                SetBusy(false, "");
                createStatusText.text = response != null ? response.message : DescribeNetworkFailure(req, null);
            }
        }
    }

    private IEnumerator DeleteCharacterRequest(int characterId)
    {
        SetBusy(true, "Suppression en cours...");

        DeleteRequestPayload payload = new DeleteRequestPayload { username = Session.Username, characterId = characterId };
        string json = JsonUtility.ToJson(payload);

        using (UnityWebRequest req = BuildPostRequest("/characters/delete", json))
        {
            yield return req.SendWebRequest();

            SimpleResponse response = ParseResponse<SimpleResponse>(req);

            if (response != null && response.success)
            {
                selectedIndex = -1;
                playButton.interactable = false;
                deleteButton.interactable = false;
                RefreshCharacterList();
            }
            else
            {
                SetBusy(false, response != null ? response.message : DescribeNetworkFailure(req, null));
            }
        }
    }

    private UnityWebRequest BuildPostRequest(string path, string json)
    {
        byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
        UnityWebRequest req = new UnityWebRequest(ServerUrl + path, "POST");
        req.uploadHandler = new UploadHandlerRaw(bodyRaw);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        return req;
    }

    private static T ParseResponse<T>(UnityWebRequest req) where T : class
    {
        string text = req.downloadHandler != null ? req.downloadHandler.text : null;

        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        try
        {
            return JsonUtility.FromJson<T>(text);
        }
        catch
        {
            return null;
        }
    }

    private static string DescribeNetworkFailure(UnityWebRequest req, object response)
    {
        if (response != null)
        {
            return "Reponse invalide du serveur.";
        }

        if (req.result == UnityWebRequest.Result.ConnectionError || req.result == UnityWebRequest.Result.ProtocolError)
        {
            return "Impossible de contacter le serveur. Verifie qu'il est lance (dotnet run dans Server/AuthServer).";
        }

        return "Reponse invalide du serveur.";
    }

    private void SetBusy(bool isBusy, string status)
    {
        busy = isBusy;
        statusText.text = status;
    }

    // ------------------------------------------------------------------
    // Petits constructeurs d'UI generiques (meme esprit que GroupUIManager)
    // ------------------------------------------------------------------

    private GameObject CreatePanel(string name, RectTransform parent, Vector2 size, out RectTransform panelRect)
    {
        GameObject panelGO = new GameObject(name);
        panelRect = panelGO.AddComponent<RectTransform>();
        panelRect.SetParent(parent, false);
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = size;
        panelRect.anchoredPosition = Vector2.zero;

        Image bg = panelGO.AddComponent<Image>();
        bg.color = PanelColor;

        Outline outline = panelGO.AddComponent<Outline>();
        outline.effectColor = BorderColor;
        outline.effectDistance = new Vector2(3, -3);

        panelGO.transform.SetAsLastSibling();

        return panelGO;
    }

    private Image CreateImage(string name, RectTransform parent, Color color)
    {
        GameObject go = new GameObject(name);
        RectTransform rect = go.AddComponent<RectTransform>();
        rect.SetParent(parent, false);
        Image image = go.AddComponent<Image>();
        image.color = color;
        return image;
    }

    private Text CreateText(string name, RectTransform parent, string text, int fontSize, Color color, FontStyle style, TextAnchor alignment)
    {
        GameObject go = new GameObject(name);
        RectTransform rect = go.AddComponent<RectTransform>();
        rect.SetParent(parent, false);

        Text textComponent = go.AddComponent<Text>();
        textComponent.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        textComponent.text = text;
        textComponent.fontSize = fontSize;
        textComponent.color = color;
        textComponent.fontStyle = style;
        textComponent.alignment = alignment;
        textComponent.raycastTarget = false;

        return textComponent;
    }

    private Button CreateThemedButton(RectTransform parent, string label, Vector2 anchoredPosition, Vector2 size)
    {
        GameObject go = new GameObject("Button_" + label);
        RectTransform rect = go.AddComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = anchoredPosition;

        Image image = go.AddComponent<Image>();
        image.color = GoldFill;

        Outline outline = go.AddComponent<Outline>();
        outline.effectColor = BorderColor;
        outline.effectDistance = new Vector2(1.5f, -1.5f);

        Button button = go.AddComponent<Button>();
        button.targetGraphic = image;
        ApplyButtonColors(button);

        Text text = CreateText("Text", rect, label, 14, DarkTextColor, FontStyle.Bold, TextAnchor.MiddleCenter);
        StretchFull(text.rectTransform);

        return button;
    }

    private InputField CreateInputField(RectTransform parent, Vector2 anchoredPosition, Vector2 size, string placeholder)
    {
        GameObject go = new GameObject("InputField");
        RectTransform rect = go.AddComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = anchoredPosition;

        Image bg = go.AddComponent<Image>();
        bg.color = new Color(0.12f, 0.1f, 0.08f, 1f);

        Outline outline = go.AddComponent<Outline>();
        outline.effectColor = BorderColor;
        outline.effectDistance = new Vector2(1, -1);

        InputField input = go.AddComponent<InputField>();

        Text placeholderText = CreateText("Placeholder", rect, placeholder, 14, new Color(0.6f, 0.6f, 0.6f, 0.8f), FontStyle.Italic, TextAnchor.MiddleLeft);
        SetAnchoredRect(placeholderText.rectTransform, Vector2.zero, Vector2.one, new Vector2(10, 0), new Vector2(-20, 0));

        Text valueText = CreateText("Text", rect, "", 14, new Color(0.95f, 0.92f, 0.85f, 1f), FontStyle.Normal, TextAnchor.MiddleLeft);
        SetAnchoredRect(valueText.rectTransform, Vector2.zero, Vector2.one, new Vector2(10, 0), new Vector2(-20, 0));
        valueText.raycastTarget = true;

        input.textComponent = valueText;
        input.placeholder = placeholderText;
        input.characterLimit = 24;

        return input;
    }

    private static void ApplyButtonColors(Button button)
    {
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.1f, 1.1f, 1.1f, 1f);
        colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
        colors.disabledColor = new Color(0.6f, 0.6f, 0.6f, 0.6f);
        button.colors = colors;
    }

    private static void StretchFull(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void SetAnchoredRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 sizeDelta)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;
    }
}
