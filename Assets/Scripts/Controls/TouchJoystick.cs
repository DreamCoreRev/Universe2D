using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// Joystick virtuel tactile, pour pouvoir tester le jeu sur téléphone/tablette
/// (ou à la souris dans l'éditeur) en plus du clavier.
///
/// Il s'installe tout seul au lancement du jeu (voir Install(), appelée
/// automatiquement par Unity) : il n'y a rien à glisser dans la scène, il
/// suffit que ce fichier existe dans le projet. Un cercle semi-transparent
/// apparaît en bas à gauche de l'écran ; le faire glisser déplace le
/// personnage dans cette direction, exactement comme les touches
/// ZQSD/flèches (voir Player.GetInput() dans Player.cs, qui additionne
/// TouchJoystick.Direction à la direction du clavier).
/// </summary>
public class TouchJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
	private static TouchJoystick instance;

	/// <summary>
	/// Direction actuelle du joystick, de (0,0) (relâché) à une longueur de 1
	/// (bâton poussé jusqu'au bord). Vector2.zero si le joystick n'existe pas
	/// encore (par exemple avant que la première scène ait fini de charger).
	/// </summary>
	public static Vector2 Direction
	{
		get { return instance != null ? instance.currentDirection : Vector2.zero; }
	}

	// Rayon, en pixels à la résolution de référence (1920x1080), que le
	// bâton peut parcourir autour du centre avant d'être bloqué.
	private const float RADIUS = 100f;

	private RectTransform background;
	private RectTransform handle;
	private Vector2 currentDirection = Vector2.zero;
	private static Sprite cachedKnobSprite;

	// RuntimeInitializeOnLoadMethod : Unity appelle cette méthode toute
	// seule juste après le chargement de la première scène, que ce script
	// soit posé sur un GameObject ou non -- c'est ce qui permet au joystick
	// de s'installer sans aucune manipulation dans l'éditeur.
	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
	private static void Install()
	{
		if (instance != null)
		{
			return;
		}

		GameObject canvasGO = new GameObject("TouchJoystickCanvas");
		Canvas canvas = canvasGO.AddComponent<Canvas>();
		canvas.renderMode = RenderMode.ScreenSpaceOverlay;
		canvas.sortingOrder = 1000; // par-dessus le reste de l'interface

		CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
		scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
		scaler.referenceResolution = new Vector2(1920, 1080);
		scaler.matchWidthOrHeight = 0.5f;

		canvasGO.AddComponent<GraphicRaycaster>();
		Object.DontDestroyOnLoad(canvasGO);

		GameObject bgGO = new GameObject("JoystickBackground", typeof(RectTransform));
		bgGO.transform.SetParent(canvasGO.transform, false);

		Image bgImage = bgGO.AddComponent<Image>();
		bgImage.sprite = GetKnobSprite();
		bgImage.color = new Color(1f, 1f, 1f, 0.25f);

		RectTransform bgRect = bgGO.GetComponent<RectTransform>();
		bgRect.anchorMin = new Vector2(0f, 0f);
		bgRect.anchorMax = new Vector2(0f, 0f);
		bgRect.pivot = new Vector2(0.5f, 0.5f);
		bgRect.sizeDelta = new Vector2(220f, 220f);
		bgRect.anchoredPosition = new Vector2(180f, 180f);

		GameObject handleGO = new GameObject("JoystickHandle", typeof(RectTransform));
		handleGO.transform.SetParent(bgGO.transform, false);

		Image handleImage = handleGO.AddComponent<Image>();
		handleImage.sprite = bgImage.sprite;
		handleImage.color = new Color(1f, 1f, 1f, 0.6f);

		RectTransform handleRect = handleGO.GetComponent<RectTransform>();
		handleRect.sizeDelta = new Vector2(100f, 100f);
		handleRect.anchoredPosition = Vector2.zero;

		TouchJoystick joystick = bgGO.AddComponent<TouchJoystick>();
		joystick.background = bgRect;
		joystick.handle = handleRect;
		instance = joystick;
	}

	// Unity a un sprite rond tout prêt ("Knob") fourni avec l'éditeur -- pas
	// besoin d'importer une image, ce qui évite de devoir créer/brancher un
	// nouvel asset juste pour ce bouton.
	private static Sprite GetKnobSprite()
	{
		if (cachedKnobSprite == null)
		{
			cachedKnobSprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/Knob.psd");
		}
		if (cachedKnobSprite == null)
		{
			cachedKnobSprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/UISprite.psd");
		}
		return cachedKnobSprite;
	}

	public void OnPointerDown(PointerEventData eventData)
	{
		OnDrag(eventData);
	}

	public void OnDrag(PointerEventData eventData)
	{
		Vector2 localPoint;
		RectTransformUtility.ScreenPointToLocalPointInRectangle(background, eventData.position, eventData.pressEventCamera, out localPoint);

		Vector2 clamped = Vector2.ClampMagnitude(localPoint, RADIUS);
		handle.anchoredPosition = clamped;
		currentDirection = clamped / RADIUS;
	}

	public void OnPointerUp(PointerEventData eventData)
	{
		currentDirection = Vector2.zero;
		handle.anchoredPosition = Vector2.zero;
	}
}
