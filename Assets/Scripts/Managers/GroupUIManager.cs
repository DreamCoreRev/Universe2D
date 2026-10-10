using Mirror;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Menu contextuel (clic droit sur un autre joueur -> "Inviter au groupe")
/// et popup d'invitation (Accepter/Refuser) recue par le joueur invite.
///
/// Purement visuel : ce composant ne touche jamais a Mirror directement,
/// il relaie vers Player.MyInstance (voir Player.InviteToGroup /
/// Player.RespondToGroupInvite), qui route lui-meme vers PlayerGroupSync --
/// meme separation que ChatManager/PlayerChatSync.
///
/// Toute l'UI est construite au demarrage par code, exactement comme
/// TouchJoystick.cs (voir son commentaire) : ca evite de devoir recopier a
/// la main des guids de composants Unity non deja verifies dans ce projet
/// (ScrollRect, etc. -- ici pas de composant exotique, mais le principe
/// "tout en code" reste le plus simple et le plus sur a maintenir). Un
/// Canvas dedie, avec la meme reference d'echelle que UICanvas (voir
/// TouchJoystick), pour rester aligne pixel pour pixel avec le reste de
/// l'interface.
/// </summary>
public class GroupUIManager : MonoBehaviour
{
    private static GroupUIManager instance;

    public static GroupUIManager MyInstance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<GroupUIManager>();
            }

            return instance;
        }
    }

    private RectTransform canvasRect;
    private RectTransform contextMenuRect;
    private GameObject contextMenuGO;
    private Player contextMenuTarget;

    private GameObject invitePopupGO;
    private Text invitePopupText;
    private NetworkIdentity pendingInviterIdentity;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (instance != null)
        {
            return;
        }

        GameObject managerGO = new GameObject("GroupUIManager");
        instance = managerGO.AddComponent<GroupUIManager>();
        Object.DontDestroyOnLoad(managerGO);

        instance.BuildUI();
    }

    private void BuildUI()
    {
        GameObject canvasGO = new GameObject("GroupUICanvas");
        canvasGO.transform.SetParent(transform, false);
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 2000; // au-dessus du chat (voir ChatManager) et du joystick tactile (voir TouchJoystick)

        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        // Meme reference que UICanvas (voir Demo.unity) et TouchJoystickCanvas
        // (voir TouchJoystick.cs) -- un pixel ici correspond exactement a un
        // pixel du reste de l'interface.
        scaler.referenceResolution = new Vector2(800, 600);
        scaler.matchWidthOrHeight = 1f;

        canvasGO.AddComponent<GraphicRaycaster>();
        canvasRect = canvasGO.GetComponent<RectTransform>();

        BuildContextMenu(canvasRect);
        BuildInvitePopup(canvasRect);
    }

    private void BuildContextMenu(RectTransform parent)
    {
        contextMenuGO = new GameObject("GroupContextMenu", typeof(RectTransform));
        contextMenuGO.transform.SetParent(parent, false);

        contextMenuRect = contextMenuGO.GetComponent<RectTransform>();
        contextMenuRect.anchorMin = new Vector2(0f, 1f);
        contextMenuRect.anchorMax = new Vector2(0f, 1f);
        contextMenuRect.pivot = new Vector2(0f, 1f);
        contextMenuRect.sizeDelta = new Vector2(150f, 34f);

        Image bg = contextMenuGO.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.85f);

        GameObject buttonGO = BuildButton(contextMenuRect, "Inviter au groupe", Vector2.zero, new Vector2(150f, 34f), new Color(1f, 1f, 1f, 0.12f));
        Button button = buttonGO.GetComponent<Button>();
        button.onClick.AddListener(OnInviteButtonClicked);

        contextMenuGO.SetActive(false);
    }

    private void BuildInvitePopup(RectTransform parent)
    {
        invitePopupGO = new GameObject("GroupInvitePopup", typeof(RectTransform));
        invitePopupGO.transform.SetParent(parent, false);

        RectTransform rect = invitePopupGO.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(260f, 110f);
        rect.anchoredPosition = new Vector2(0f, 80f);

        Image bg = invitePopupGO.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.85f);

        GameObject textGO = new GameObject("Text", typeof(RectTransform));
        textGO.transform.SetParent(invitePopupGO.transform, false);
        RectTransform textRect = textGO.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0f, 1f);
        textRect.anchorMax = new Vector2(1f, 1f);
        textRect.pivot = new Vector2(0.5f, 1f);
        textRect.anchoredPosition = new Vector2(0f, -8f);
        textRect.sizeDelta = new Vector2(-16f, 56f);

        invitePopupText = textGO.AddComponent<Text>();
        invitePopupText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        invitePopupText.fontSize = 14;
        invitePopupText.color = Color.white;
        invitePopupText.alignment = TextAnchor.UpperCenter;
        invitePopupText.horizontalOverflow = HorizontalWrapMode.Wrap;
        invitePopupText.verticalOverflow = VerticalWrapMode.Overflow;
        invitePopupText.text = "";

        GameObject acceptGO = BuildButton(invitePopupGO.GetComponent<RectTransform>(), "Accepter", new Vector2(-62f, 12f), new Vector2(110f, 30f), new Color(0.25f, 0.65f, 0.25f, 0.9f));
        acceptGO.GetComponent<RectTransform>().anchorMin = new Vector2(0.5f, 0f);
        acceptGO.GetComponent<RectTransform>().anchorMax = new Vector2(0.5f, 0f);
        acceptGO.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 0f);
        acceptGO.GetComponent<Button>().onClick.AddListener(OnAcceptButtonClicked);

        GameObject declineGO = BuildButton(invitePopupGO.GetComponent<RectTransform>(), "Refuser", new Vector2(62f, 12f), new Vector2(110f, 30f), new Color(0.65f, 0.25f, 0.25f, 0.9f));
        declineGO.GetComponent<RectTransform>().anchorMin = new Vector2(0.5f, 0f);
        declineGO.GetComponent<RectTransform>().anchorMax = new Vector2(0.5f, 0f);
        declineGO.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 0f);
        declineGO.GetComponent<Button>().onClick.AddListener(OnDeclineButtonClicked);

        invitePopupGO.SetActive(false);
    }

    private GameObject BuildButton(RectTransform parent, string label, Vector2 anchoredPosition, Vector2 size, Color color)
    {
        GameObject go = new GameObject(label + "Button", typeof(RectTransform));
        go.transform.SetParent(parent, false);

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.sizeDelta = size;
        rect.anchoredPosition = anchoredPosition;

        Image image = go.AddComponent<Image>();
        image.color = color;

        Button button = go.AddComponent<Button>();
        button.targetGraphic = image;

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
        text.fontSize = 13;
        text.color = Color.white;
        text.raycastTarget = false;

        return go;
    }

    /// <summary>
    /// Appele par GameManager des qu'un clic droit touche le collider
    /// "PlayerClickable" d'un autre joueur (voir Player.prefab -- un enfant
    /// dedie sur le layer Clickable, separe du collider "Player" deja
    /// utilise par l'aggro des monstres (voir Range.cs) et le ciblage de
    /// combat, pour ne jamais interferer avec eux).
    /// </summary>
    public void ShowContextMenu(Player target, Vector3 screenPosition)
    {
        if (target == null || canvasRect == null)
        {
            return;
        }

        contextMenuTarget = target;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPosition, null, out Vector2 localPoint);

        // On garde le menu entierement visible meme si on clique pres d'un bord d'ecran.
        Vector2 canvasSize = canvasRect.rect.size;
        Vector2 menuSize = contextMenuRect.sizeDelta;
        float minX = -canvasSize.x / 2f;
        float maxX = canvasSize.x / 2f - menuSize.x;
        float minY = -canvasSize.y / 2f + menuSize.y;
        float maxY = canvasSize.y / 2f;

        localPoint.x = Mathf.Clamp(localPoint.x, minX, maxX);
        localPoint.y = Mathf.Clamp(localPoint.y, minY, maxY);

        contextMenuRect.anchoredPosition = localPoint;
        contextMenuGO.SetActive(true);
    }

    public void HideContextMenu()
    {
        if (contextMenuGO != null)
        {
            contextMenuGO.SetActive(false);
        }

        contextMenuTarget = null;
    }

    private void OnInviteButtonClicked()
    {
        if (contextMenuTarget != null && Player.MyInstance != null)
        {
            Player.MyInstance.InviteToGroup(contextMenuTarget);
        }

        HideContextMenu();
    }

    /// <summary>
    /// Appele par PlayerGroupSync.TargetReceiveInvite (cote joueur invite)
    /// quand un autre joueur vient de l'inviter dans son groupe.
    /// </summary>
    public void ShowInvitePopup(string inviterName, NetworkIdentity inviterIdentity)
    {
        if (invitePopupGO == null)
        {
            return;
        }

        pendingInviterIdentity = inviterIdentity;
        invitePopupText.text = inviterName + " vous invite dans son groupe.";
        invitePopupGO.SetActive(true);
    }

    private void OnAcceptButtonClicked()
    {
        RespondToPendingInvite(true);
    }

    private void OnDeclineButtonClicked()
    {
        RespondToPendingInvite(false);
    }

    private void RespondToPendingInvite(bool accepted)
    {
        if (pendingInviterIdentity != null && Player.MyInstance != null)
        {
            Player.MyInstance.RespondToGroupInvite(pendingInviterIdentity, accepted);
        }

        pendingInviterIdentity = null;
        invitePopupGO.SetActive(false);
    }
}
