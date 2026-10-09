using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// Barre d'icônes tactiles en haut de l'écran : panneaux (Personnage, Sorts,
/// Métiers, Quêtes, Talents, Menu) + raccourcis de debug (potion, potion+or,
/// équipement, XP) -- l'équivalent tactile des touches C / I / P / L / N /
/// Echap / M / U / H / X, pour quand il n'y a pas de clavier (téléphone,
/// tablette).
///
/// Comme TouchJoystick.cs, ce script s'installe tout seul au lancement du
/// jeu : rien à glisser dans la scène, il suffit que ce fichier soit dans le
/// projet. Chaque bouton est une petite icône carrée avec juste un sigle
/// dessus (pas de rectangle avec un nom complet dessus) ; appuyer dessus fait
/// l'action tout de suite ET affiche brièvement une bulle au-dessus avec le
/// nom complet, pour qu'on sache ce qu'on vient de presser.
/// </summary>
public class TouchMenuButtons : MonoBehaviour
{
	private struct ButtonDef
	{
		public string Sigle;   // texte affiché sur l'icône elle-même
		public string Tooltip; // texte affiché dans la bulle au-dessus
		public Action OnTap;

		public ButtonDef(string sigle, string tooltip, Action onTap)
		{
			Sigle = sigle;
			Tooltip = tooltip;
			OnTap = onTap;
		}
	}

	// Les index correspondent à UIManager.menus[] -- voir UIManager.Update()
	// pour la correspondance avec les raccourcis clavier (Echap=0,
	// SpellBook=1, CharacterPanel=2, Questlog=3, Profession=6, TalentTree=7).
	// Les 4 derniers boutons reprennent exactement ce que font les touches
	// M / U / H / X (voir InventoryScript.cs et Player.GainXP).
	private static List<ButtonDef> BuildButtons()
	{
		return new List<ButtonDef>
		{
			new ButtonDef("PER", "Personnage", () => UIManager.MyInstance.ToggleMenu(2)),
			new ButtonDef("SOR", "Sorts", () => UIManager.MyInstance.ToggleMenu(1)),
			new ButtonDef("MÉT", "Métiers", () => UIManager.MyInstance.ToggleMenu(6)),
			new ButtonDef("QUÊ", "Quêtes", () => UIManager.MyInstance.ToggleMenu(3)),
			new ButtonDef("TAL", "Talents", () => UIManager.MyInstance.ToggleMenu(7)),
			new ButtonDef("MEN", "Menu", () => UIManager.MyInstance.ToggleMenu(0)),
			new ButtonDef("POT", "Donner une potion", () => InventoryScript.MyInstance.GivePotion()),
			new ButtonDef("OR+", "Donner potion + or", () => InventoryScript.MyInstance.GivePotionAndGold()),
			new ButtonDef("ÉQU", "Donner l'équipement", () => InventoryScript.MyInstance.GiveEquipment()),
			new ButtonDef("XP", "Donner 600 XP", () => Player.MyInstance.GainXP(600)),
		};
	}

	private const float BUTTON_SIZE = 56f;
	private const float SPACING = 8f;
	private const float MARGIN_TOP = 16f;
	private const float TOOLTIP_SHOW_SECONDS = 1.2f;

	private static bool installed = false;
	private static RectTransform tooltipRoot;
	private static Text tooltipText;
	private static Coroutine hideTooltipRoutine;
	private static MonoBehaviour coroutineRunner;
	private static Font cachedFont;

	// Le texte restait invisible (boutons tout noirs, bulle de tooltip
	// vide) parce que "LegacyRuntime.ttf" -- le nom de la police intégrée
	// pour le Text classique depuis les versions récentes de Unity -- ne
	// correspond apparemment à rien dans cette version/ce projet. On
	// essaie plusieurs noms connus, et en dernier recours on construit une
	// police depuis celles installées sur le système, pour être sûr d'avoir
	// toujours quelque chose d'affichable plutôt qu'un texte invisible.
	private static Font GetUIFont()
	{
		if (cachedFont != null)
		{
			return cachedFont;
		}

		cachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
		if (cachedFont == null)
		{
			cachedFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
		}
		if (cachedFont == null)
		{
			cachedFont = Font.CreateDynamicFontFromOSFont("Arial", 24);
		}

		return cachedFont;
	}

	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
	private static void Install()
	{
		if (installed)
		{
			return;
		}
		installed = true;

		GameObject canvasGO = new GameObject("TouchMenuButtonsCanvas");
		Canvas canvas = canvasGO.AddComponent<Canvas>();
		canvas.renderMode = RenderMode.ScreenSpaceOverlay;
		canvas.sortingOrder = 1000;

		CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
		scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
		scaler.referenceResolution = new Vector2(1920, 1080);
		scaler.matchWidthOrHeight = 0.5f;

		canvasGO.AddComponent<GraphicRaycaster>();
		UnityEngine.Object.DontDestroyOnLoad(canvasGO);

		// Rangée horizontale centrée en haut de l'écran. Un HorizontalLayoutGroup
		// + ContentSizeFitter place les icônes les unes à côté des autres tout
		// seul (pas besoin de calculer chaque position à la main), et le
		// conteneur reste centré puisque c'est lui qui est ancré, pas ses enfants.
		GameObject rowGO = new GameObject("TouchMenuRow", typeof(RectTransform));
		rowGO.transform.SetParent(canvasGO.transform, false);
		RectTransform rowRect = rowGO.GetComponent<RectTransform>();
		rowRect.anchorMin = new Vector2(0.5f, 1f);
		rowRect.anchorMax = new Vector2(0.5f, 1f);
		rowRect.pivot = new Vector2(0.5f, 1f);
		rowRect.anchoredPosition = new Vector2(0f, -MARGIN_TOP);

		HorizontalLayoutGroup layout = rowGO.AddComponent<HorizontalLayoutGroup>();
		layout.spacing = SPACING;
		layout.childAlignment = TextAnchor.MiddleCenter;
		layout.childForceExpandWidth = false;
		layout.childForceExpandHeight = false;
		layout.childControlWidth = false;
		layout.childControlHeight = false;

		ContentSizeFitter fitter = rowGO.AddComponent<ContentSizeFitter>();
		fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
		fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

		// Bulle de tooltip partagée par tous les boutons : une seule instance
		// qu'on repositionne et dont on change juste le texte, plutôt que
		// d'en créer une par bouton.
		CreateTooltip(canvasGO.transform);

		coroutineRunner = canvasGO.AddComponent<TooltipRunner>();

		foreach (ButtonDef def in BuildButtons())
		{
			CreateButton(rowGO.transform, def);
		}
	}

	private static void CreateTooltip(Transform parent)
	{
		GameObject tooltipGO = new GameObject("TooltipBubble", typeof(RectTransform));
		tooltipGO.transform.SetParent(parent, false);

		Image bg = tooltipGO.AddComponent<Image>();
		bg.color = new Color(0f, 0f, 0f, 0.85f);
		bg.raycastTarget = false;

		RectTransform rect = tooltipGO.GetComponent<RectTransform>();
		rect.anchorMin = new Vector2(0.5f, 1f);
		rect.anchorMax = new Vector2(0.5f, 1f);
		rect.pivot = new Vector2(0.5f, 1f);
		rect.sizeDelta = new Vector2(240f, 44f);

		GameObject textGO = new GameObject("Text", typeof(RectTransform));
		textGO.transform.SetParent(tooltipGO.transform, false);
		Text text = textGO.AddComponent<Text>();
		text.alignment = TextAnchor.MiddleCenter;
		text.color = Color.white;
		text.font = GetUIFont();
		text.fontSize = 22;
		text.raycastTarget = false;
		RectTransform textRect = textGO.GetComponent<RectTransform>();
		textRect.anchorMin = Vector2.zero;
		textRect.anchorMax = Vector2.one;
		textRect.sizeDelta = Vector2.zero;
		textRect.anchoredPosition = Vector2.zero;

		tooltipRoot = rect;
		tooltipText = text;
		tooltipGO.SetActive(false);
	}

	private static void CreateButton(Transform parent, ButtonDef def)
	{
		GameObject buttonGO = new GameObject("MicroButton_" + def.Tooltip, typeof(RectTransform));
		buttonGO.transform.SetParent(parent, false);

		Image image = buttonGO.AddComponent<Image>();
		image.color = new Color(0.08f, 0.08f, 0.12f, 0.92f);

		LayoutElement layoutElement = buttonGO.AddComponent<LayoutElement>();
		layoutElement.preferredWidth = BUTTON_SIZE;
		layoutElement.preferredHeight = BUTTON_SIZE;

		Button button = buttonGO.AddComponent<Button>();
		button.targetGraphic = image;
		button.onClick.AddListener(() => def.OnTap());

		RectTransform rect = buttonGO.GetComponent<RectTransform>();
		rect.sizeDelta = new Vector2(BUTTON_SIZE, BUTTON_SIZE);

		GameObject textGO = new GameObject("Text", typeof(RectTransform));
		textGO.transform.SetParent(buttonGO.transform, false);
		Text text = textGO.AddComponent<Text>();
		text.text = def.Sigle;
		text.alignment = TextAnchor.MiddleCenter;
		text.color = Color.white;
		text.font = GetUIFont();
		text.fontStyle = FontStyle.Bold;
		text.raycastTarget = false;
		text.resizeTextForBestFit = true;
		text.resizeTextMinSize = 10;
		text.resizeTextMaxSize = 20;
		RectTransform textRect = textGO.GetComponent<RectTransform>();
		textRect.anchorMin = new Vector2(0f, 0f);
		textRect.anchorMax = new Vector2(1f, 1f);
		textRect.sizeDelta = new Vector2(-6f, -6f); // petite marge pour que le sigle ne touche pas les bords
		textRect.anchoredPosition = Vector2.zero;

		TooltipTrigger trigger = buttonGO.AddComponent<TooltipTrigger>();
		trigger.Label = def.Tooltip;
	}

	// Composant minimaliste posé sur chaque icône : affiche la bulle de
	// tooltip juste au-dessus dès qu'on pose le doigt (ou le clic), elle
	// disparaît toute seule après un court délai.
	private class TooltipTrigger : MonoBehaviour, IPointerDownHandler
	{
		public string Label;

		public void OnPointerDown(PointerEventData eventData)
		{
			if (tooltipRoot == null || tooltipText == null)
			{
				return;
			}

			RectTransform selfRect = GetComponent<RectTransform>();
			tooltipRoot.gameObject.SetActive(true);
			tooltipText.text = Label;

			// La rangée de boutons est collée tout en haut de l'écran, donc
			// une bulle au-dessus n'a pas de place pour s'afficher (elle
			// part hors de l'écran, invisible). On la met plutôt juste EN
			// DESSOUS du bouton, où il y a de la place.
			Vector3 worldPos = selfRect.position - new Vector3(0f, selfRect.rect.height * selfRect.lossyScale.y * 0.5f + 8f, 0f);
			tooltipRoot.position = worldPos;

			if (hideTooltipRoutine != null && coroutineRunner != null)
			{
				coroutineRunner.StopCoroutine(hideTooltipRoutine);
			}
			if (coroutineRunner != null)
			{
				hideTooltipRoutine = coroutineRunner.StartCoroutine(HideAfterDelay());
			}
		}

		private IEnumerator HideAfterDelay()
		{
			yield return new WaitForSeconds(TOOLTIP_SHOW_SECONDS);
			if (tooltipRoot != null)
			{
				tooltipRoot.gameObject.SetActive(false);
			}
		}
	}

	// MonoBehaviour vide, juste pour avoir un objet vivant (DontDestroyOnLoad)
	// sur lequel lancer la coroutine qui cache la bulle de tooltip.
	private class TooltipRunner : MonoBehaviour
	{
	}
}
