using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

/// <summary>
/// Barre d'icônes tactiles en bas à droite de l'écran, juste au-dessus de la
/// barre de sacs (comme le "micro menu" de Wow) : panneaux (Personnage,
/// Sorts, Métiers, Quêtes, Talents, Menu) + raccourcis de debug (potion,
/// potion+or, équipement, XP) -- l'équivalent tactile des touches C / I / P /
/// L / N / Echap / M / U / H / X, pour quand il n'y a pas de clavier
/// (téléphone, tablette).
///
/// Comme TouchJoystick.cs, ce script s'installe tout seul au lancement du
/// jeu : rien à glisser dans la scène, il suffit que ce fichier soit dans le
/// projet. Ne s'affiche que dans la scène de jeu (Demo, voir
/// UpdateVisibility) -- pas sur Login ni CharacterSelect. Chaque bouton est
/// une petite icône carrée avec juste un sigle dessus (pas de rectangle avec
/// un nom complet dessus) ; appuyer dessus fait l'action tout de suite ET
/// affiche brièvement une bulle au-dessus avec le nom complet, pour qu'on
/// sache ce qu'on vient de presser.
/// </summary>
public class TouchMenuButtons : MonoBehaviour
{
	private struct ButtonDef
	{
		public string Sigle;   // texte affiché si pas d'icône (secours, ex. MEN)
		public string Tooltip; // texte affiché dans la bulle au-dessus
		public Sprite Icon;    // icône du projet affichée sur le bouton (voir TouchMenuButtonSkin)
		public Action OnTap;

		public ButtonDef(string sigle, string tooltip, Sprite icon, Action onTap)
		{
			Sigle = sigle;
			Tooltip = tooltip;
			Icon = icon;
			OnTap = onTap;
		}
	}

	// Les index correspondent à UIManager.menus[] -- voir UIManager.Update()
	// pour la correspondance avec les raccourcis clavier (Echap=0,
	// SpellBook=1, CharacterPanel=2, Questlog=3, Profession=6, TalentTree=7).
	// Les raccourcis de debug (POT/OR+/ÉQU/XP) ont été retirés de cette
	// barre -- il ne reste que les 6 panneaux. "skin" peut être null si
	// jamais l'asset Resources/TouchMenuButtonSkin manque : chaque bouton
	// retombe alors sur son sigle texte plutôt que de planter.
	private static List<ButtonDef> BuildButtons(TouchMenuButtonSkin skin)
	{
		return new List<ButtonDef>
		{
			new ButtonDef("PER", "Personnage", skin != null ? skin.characterIcon : null, () => UIManager.MyInstance.ToggleMenu(2)),
			new ButtonDef("SOR", "Sorts", skin != null ? skin.spellsIcon : null, () => UIManager.MyInstance.ToggleMenu(1)),
			new ButtonDef("MÉT", "Métiers", skin != null ? skin.professionIcon : null, () => UIManager.MyInstance.ToggleMenu(6)),
			new ButtonDef("QUÊ", "Quêtes", skin != null ? skin.questIcon : null, () => UIManager.MyInstance.ToggleMenu(3)),
			new ButtonDef("TAL", "Talents", skin != null ? skin.talentIcon : null, () => UIManager.MyInstance.ToggleMenu(7)),
			new ButtonDef("MEN", "Menu", null, () => UIManager.MyInstance.ToggleMenu(0)),
		};
	}

	// Cases un peu plus petites qu'avant (34 -> 26) pour ne plus toucher
	// les cases de sacs juste a cote.
	private const float BUTTON_SIZE = 26f;
	private const float SPACING = 4f;
	// Repoussee de 20px vers la droite par rapport au reglage precedent
	// (30 - 20 = 10).
	private const float MARGIN_RIGHT = 10f;
	// Hauteur au-dessus du coin bas-droit a laquelle la rangee est ancree,
	// pour la placer juste au-dessus des cases de sacs (voir BagBar dans
	// Demo.unity, ancree pareil en bas a droite) -- comme le "micro menu"
	// de Wow, juste au-dessus de la barre de sacs. Remontee de 6px par
	// rapport au reglage precedent (0 -> 6).
	private const float MARGIN_BOTTOM = 6f;
	private const float TOOLTIP_SHOW_SECONDS = 1.2f;

	// Seule la scene de jeu (Demo.unity) doit afficher cette barre -- pas
	// Login ni CharacterSelect, qui n'ont pas de personnage/HUD a gerer.
	private const string GameSceneName = "Demo";

	// Meme palette bronze/or que CharacterSelectManager, pour un theme
	// coherent avec le reste de l'UI (plutot que les carres noirs plats
	// d'avant).
	private static readonly Color PanelColor = new Color(0.10f, 0.08f, 0.07f, 0.94f);
	private static readonly Color BorderColor = new Color(0.22f, 0.14f, 0.03f, 0.95f);
	private static readonly Color GoldTextColor = new Color(0.95f, 0.82f, 0.45f, 1f);

	private static bool installed = false;
	private static GameObject canvasGO;
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

		TouchMenuButtonSkin skin = Resources.Load<TouchMenuButtonSkin>("TouchMenuButtonSkin");

		canvasGO = new GameObject("TouchMenuButtonsCanvas");
		Canvas canvas = canvasGO.AddComponent<Canvas>();
		canvas.renderMode = RenderMode.ScreenSpaceOverlay;
		canvas.sortingOrder = 1000;

		// Meme reference (800x600, ajustement par la hauteur) que le Canvas
		// du HUD de jeu (voir Demo.unity, BagBar) -- pour que nos propres
		// unites d'ancrage se comportent pareil a toutes les resolutions
		// que celles de la barre de sacs, a cote de laquelle on se place.
		CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
		scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
		scaler.referenceResolution = new Vector2(800, 600);
		scaler.matchWidthOrHeight = 1f;

		canvasGO.AddComponent<GraphicRaycaster>();
		UnityEngine.Object.DontDestroyOnLoad(canvasGO);

		// Rangée ancrée en bas à droite, juste au-dessus de la barre de
		// sacs (voir MARGIN_BOTTOM) -- comme le "micro menu" de Wow. Un
		// HorizontalLayoutGroup + ContentSizeFitter place les icônes les
		// unes à côté des autres tout seul et le conteneur grandit vers la
		// gauche en gardant son bord droit ancré au même endroit.
		GameObject rowGO = new GameObject("TouchMenuRow", typeof(RectTransform));
		rowGO.transform.SetParent(canvasGO.transform, false);
		RectTransform rowRect = rowGO.GetComponent<RectTransform>();
		rowRect.anchorMin = new Vector2(1f, 0f);
		rowRect.anchorMax = new Vector2(1f, 0f);
		rowRect.pivot = new Vector2(1f, 0f);
		rowRect.anchoredPosition = new Vector2(-MARGIN_RIGHT, MARGIN_BOTTOM);

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

		foreach (ButtonDef def in BuildButtons(skin))
		{
			CreateButton(rowGO.transform, def);
		}

		// N'afficher la barre que dans la scene de jeu (Demo) -- pas sur
		// Login ni CharacterSelect, qui n'ont pas besoin de ces raccourcis
		// et ou elle ne faisait que se superposer au reste de l'ecran.
		SceneManager.sceneLoaded += OnSceneLoaded;
		UpdateVisibility(SceneManager.GetActiveScene().name);
	}

	private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
	{
		UpdateVisibility(scene.name);
	}

	private static void UpdateVisibility(string sceneName)
	{
		if (canvasGO != null)
		{
			canvasGO.SetActive(sceneName == GameSceneName);
		}
	}

	private static void CreateTooltip(Transform parent)
	{
		GameObject tooltipGO = new GameObject("TooltipBubble", typeof(RectTransform));
		tooltipGO.transform.SetParent(parent, false);

		Image bg = tooltipGO.AddComponent<Image>();
		bg.color = PanelColor;
		bg.raycastTarget = false;

		Outline outline = tooltipGO.AddComponent<Outline>();
		outline.effectColor = BorderColor;
		outline.effectDistance = new Vector2(1.2f, -1.2f);

		// Pivot en bas (0.5, 0) : la bulle s'etend vers le HAUT depuis la
		// position qu'on lui donne (voir TooltipTrigger.OnPointerDown),
		// puisque la rangee de boutons est maintenant en bas de l'ecran.
		RectTransform rect = tooltipGO.GetComponent<RectTransform>();
		rect.anchorMin = new Vector2(0.5f, 0f);
		rect.anchorMax = new Vector2(0.5f, 0f);
		rect.pivot = new Vector2(0.5f, 0f);
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

		// Toujours le meme fond bronze/or uni + lisere (celui que Aurora a
		// valide) -- ce n'etait pas le fond plat qui posait probleme, c'etait
		// le sprite vert utilise dans la version precedente. L'icone (quand
		// il y en a une) se superpose par-dessus, pas a la place.
		Image image = buttonGO.AddComponent<Image>();
		image.color = PanelColor;
		Outline outline = buttonGO.AddComponent<Outline>();
		outline.effectColor = BorderColor;
		outline.effectDistance = new Vector2(1.2f, -1.2f);

		LayoutElement layoutElement = buttonGO.AddComponent<LayoutElement>();
		layoutElement.preferredWidth = BUTTON_SIZE;
		layoutElement.preferredHeight = BUTTON_SIZE;

		Button button = buttonGO.AddComponent<Button>();
		button.targetGraphic = image;
		ApplyButtonColors(button);
		button.onClick.AddListener(() => def.OnTap());

		RectTransform rect = buttonGO.GetComponent<RectTransform>();
		rect.sizeDelta = new Vector2(BUTTON_SIZE, BUTTON_SIZE);

		if (def.Icon != null)
		{
			// Vraie icone du projet (portrait, sorts, quete, talent,
			// metier), en retrait a l'interieur du cadre bronze/or plutot
			// que de remplir tout le bouton -- voir TouchMenuButtonSkin.
			GameObject iconGO = new GameObject("Icon", typeof(RectTransform));
			iconGO.transform.SetParent(buttonGO.transform, false);
			Image iconImage = iconGO.AddComponent<Image>();
			iconImage.sprite = def.Icon;
			iconImage.color = Color.white;
			iconImage.preserveAspect = true;
			iconImage.raycastTarget = false;
			RectTransform iconRect = iconGO.GetComponent<RectTransform>();
			iconRect.anchorMin = new Vector2(0f, 0f);
			iconRect.anchorMax = new Vector2(1f, 1f);
			iconRect.sizeDelta = new Vector2(-8f, -8f); // marge pour laisser voir le cadre bronze/or
			iconRect.anchoredPosition = Vector2.zero;
		}
		else
		{
			// Pas d'icone correspondante dans les assets du projet (cas de
			// MEN) : on retombe sur le sigle texte, comme avant.
			GameObject textGO = new GameObject("Text", typeof(RectTransform));
			textGO.transform.SetParent(buttonGO.transform, false);
			Text text = textGO.AddComponent<Text>();
			text.text = def.Sigle;
			text.alignment = TextAnchor.MiddleCenter;
			text.color = GoldTextColor;
			text.font = GetUIFont();
			text.fontStyle = FontStyle.Bold;
			text.raycastTarget = false;
			text.resizeTextForBestFit = true;
			text.resizeTextMinSize = 7;
			text.resizeTextMaxSize = 14;
			RectTransform textRect = textGO.GetComponent<RectTransform>();
			textRect.anchorMin = new Vector2(0f, 0f);
			textRect.anchorMax = new Vector2(1f, 1f);
			textRect.sizeDelta = new Vector2(-4f, -4f); // petite marge pour que le sigle ne touche pas les bords
			textRect.anchoredPosition = Vector2.zero;
		}

		TooltipTrigger trigger = buttonGO.AddComponent<TooltipTrigger>();
		trigger.Label = def.Tooltip;
	}

	// Meme logique que CharacterSelectManager.ApplyButtonColors : le bouton
	// s'eclaircit legerement au survol et s'assombrit au clic, plutot que
	// de rester plat.
	private static void ApplyButtonColors(Button button)
	{
		ColorBlock colors = button.colors;
		colors.normalColor = Color.white;
		colors.highlightedColor = new Color(1.3f, 1.3f, 1.3f, 1f);
		colors.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
		colors.disabledColor = new Color(0.6f, 0.6f, 0.6f, 0.6f);
		button.colors = colors;
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

			// La rangée de boutons est maintenant en bas de l'écran (à côté
			// des sacs), donc une bulle en dessous partirait hors de
			// l'écran : on l'affiche plutôt juste AU-DESSUS du bouton.
			Vector3 worldPos = selfRect.position + new Vector3(0f, selfRect.rect.height * selfRect.lossyScale.y * 0.5f + 8f, 0f);
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
