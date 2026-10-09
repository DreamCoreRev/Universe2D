using UnityEngine;
using UnityEngine.UI;

// DIAGNOSTIC TEMPORAIRE : affiche en haut de l'écran les infos de
// GameManager.DebugLine1/2 (voir GameManager.ClickTarget()) pour comprendre
// pourquoi le ciblage tactile des ennemis ne marche pas sur téléphone. Ce
// fichier est à supprimer une fois le bug trouvé.
public class DebugOverlay : MonoBehaviour
{
    private Text text;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        GameObject canvasGO = new GameObject("DebugOverlayCanvas");
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 2000; // par-dessus absolument tout

        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        UnityEngine.Object.DontDestroyOnLoad(canvasGO);

        GameObject textGO = new GameObject("DebugText", typeof(RectTransform));
        textGO.transform.SetParent(canvasGO.transform, false);

        Text text = textGO.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (text.font == null)
        {
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        }
        if (text.font == null)
        {
            text.font = Font.CreateDynamicFontFromOSFont("Arial", 22);
        }
        text.fontSize = 26;
        text.color = Color.yellow;
        text.alignment = TextAnchor.UpperLeft;
        text.raycastTarget = false;
        text.text = "(debug)";

        RectTransform rect = textGO.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(16f, -140f);
        rect.sizeDelta = new Vector2(-32f, 120f);

        DebugOverlay overlay = canvasGO.AddComponent<DebugOverlay>();
        overlay.text = text;
    }

    private void Update()
    {
        if (text != null)
        {
            text.text = GameManager.DebugLine1 + "\n" + GameManager.DebugLine2;
        }
    }
}
