using UnityEngine;

/// <summary>
/// Entrees manette (Xbox 360 ou compatible, branchee sous Windows) : stick
/// gauche pour se deplacer, boutons de face + gachettes d'epaule pour la
/// barre de sorts, clic du stick droit pour valider une cible de sort de
/// zone (AOE).
///
/// S'appuie uniquement sur des entrees DEJA presentes dans
/// ProjectSettings/InputManager.asset par defaut (les axes "Horizontal"/
/// "Vertical" d'Unity incluent nativement le stick gauche de n'importe
/// quelle manette en plus du clavier -- voir leurs entrees "type: 2" dans
/// InputManager.asset) et sur les KeyCode.JoystickButtonN generiques, qui
/// fonctionnent sans configuration prealable. Le mapping bouton -> numero
/// pour une manette Xbox 360 sous Windows (A=0, B=1, X=2, Y=3, LB=4, RB=5,
/// clic stick droit=9) est celui qu'Unity/Windows exposent de facon quasi
/// universelle pour ce type de manette -- a verifier en jeu, et facilement
/// modifiable ici si la numerotation differe sur une manette non-Xbox.
///
/// Pas de stick droit pour viser les sorts de zone : son numero d'axe
/// varie trop d'un pilote/systeme a l'autre pour le cabler sans manette
/// sous la main pour tester. On vise donc avec le stick gauche (celui qui
/// sert aussi a se deplacer) pendant le ciblage -- voir Player.Update().
/// </summary>
public static class GamepadInput
{
    private const float DEADZONE = 0.2f;

    /// <summary>
    /// Direction du stick gauche ((0,0) si aucune manette n'est branchee ou
    /// si le stick est au centre). Pas besoin de la normaliser ici :
    /// Character applique deja Direction.normalized pour le deplacement
    /// (voir Character.cs), et Player.Update() s'en sert telle quelle
    /// (magnitude = distance de visee, pousser a fond = portee max) pour le
    /// ciblage des sorts de zone.
    /// </summary>
    public static Vector2 MoveDirection
    {
        get
        {
            Vector2 raw = new Vector2(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical"));
            return raw.sqrMagnitude >= DEADZONE * DEADZONE ? raw : Vector2.zero;
        }
    }

    // ACT1 a ACT6 de la barre de sorts (voir KeybindManager.ActionBinds) :
    // A, B, X, Y, puis les deux gachettes d'epaule LB/RB.
    private static readonly KeyCode[] ActionButtons =
    {
        KeyCode.JoystickButton0, // A  -> ACT1
        KeyCode.JoystickButton1, // B  -> ACT2
        KeyCode.JoystickButton2, // X  -> ACT3
        KeyCode.JoystickButton3, // Y  -> ACT4
        KeyCode.JoystickButton4, // LB -> ACT5
        KeyCode.JoystickButton5, // RB -> ACT6
    };

    /// <summary>
    /// Vrai une seule frame quand le bouton correspondant a l'emplacement de
    /// sort actionIndex (0 a 5, pour ACT1 a ACT6) vient d'etre presse.
    /// </summary>
    public static bool GetActionButtonDown(int actionIndex)
    {
        if (actionIndex < 0 || actionIndex >= ActionButtons.Length)
        {
            return false;
        }

        return Input.GetKeyDown(ActionButtons[actionIndex]);
    }

    /// <summary>
    /// Bouton de validation d'un sort de zone en cours de ciblage (voir
    /// Player.Update()) -- equivalent manette du clic gauche a la souris.
    /// Le clic du stick droit (R3) est utilise ici car il n'entre en
    /// conflit avec aucun des boutons de la barre de sorts ci-dessus : les
    /// reutiliser aurait annule le ciblage au lieu de le valider (voir
    /// Player.CastSpell, qui detruit unusedSpell si on ractionne un
    /// emplacement de sort pendant le ciblage -- meme comportement que le
    /// clavier aujourd'hui).
    /// </summary>
    public static bool ConfirmPressed => Input.GetKeyDown(KeyCode.JoystickButton9);

    /// <summary>
    /// Vrai une seule frame quand on vient de presser le clic du stick
    /// gauche (L3) -- sert a cibler la creature la plus proche sans souris
    /// (voir GameManager.SelectNearestEnemyForGamepad()), l'equivalent
    /// manette du clic gauche sur une creature. L3 plutot qu'un bouton de
    /// la barre de sorts (deja pris par ACT1-6) ou une gachette (LT/RT :
    /// leur numero d'axe est trop peu standard d'un pilote/systeme a
    /// l'autre pour le cabler sans manette sous la main pour tester).
    /// </summary>
    public static bool TargetPressed => Input.GetKeyDown(KeyCode.JoystickButton8);

    /// <summary>
    /// Vrai une seule frame quand on vient de presser Back/View (bouton 6)
    /// -- sert a looter une creature morte a proximite sans souris (voir
    /// GameManager.LootNearestForGamepad()), l'equivalent manette du clic
    /// gauche/droit sur un cadavre. Choisi parce qu'il est libre (A/B/X/Y
    /// et LB/RB sont deja pris par ACT1-6, L3/R3 par le ciblage et la
    /// validation de sort de zone) et, comme les autres boutons utilises
    /// ici, un numero fiable d'un pilote/systeme a l'autre, contrairement
    /// aux gachettes ou a la croix directionnelle.
    /// </summary>
    public static bool LootPressed => Input.GetKeyDown(KeyCode.JoystickButton6);

    /// <summary>
    /// Vrai une seule frame quand on vient de presser Start -- ouvre/ferme
    /// le menu principal (Echap), voir UIManager.Update(). Choisi parce
    /// que c'est le bouton standard pour le menu pause sur la plupart des
    /// manettes (Xbox 360 et compatibles).
    /// </summary>
    public static bool StartPressed => Input.GetKeyDown(KeyCode.JoystickButton7);

    /// <summary>
    /// Valide le bouton selectionne dans le menu principal (voir
    /// UIManager.UpdateMainMenuGamepadNavigation()). Reutilise A (bouton
    /// 0, le meme qu'ACT1) : sans danger de double-declenchement avec la
    /// barre de sorts puisque la boucle ACT1-6 de Player.GetInput() est
    /// desactivee pendant que le menu principal est ouvert (voir
    /// UIManager.IsMainMenuOpen).
    /// </summary>
    public static bool MenuConfirmPressed => Input.GetKeyDown(KeyCode.JoystickButton0);

    /// <summary>
    /// Ferme le menu principal sans valider (voir
    /// UIManager.UpdateMainMenuGamepadNavigation()). Reutilise B (bouton
    /// 1, le meme qu'ACT2) pour la meme raison que MenuConfirmPressed
    /// ci-dessus : aucun conflit possible puisque ACT1-6 est desactive
    /// tant que le menu principal est ouvert.
    /// </summary>
    public static bool MenuCancelPressed => Input.GetKeyDown(KeyCode.JoystickButton1);

    // Seuil de bascule de l'axe "Vertical" (stick gauche) pour la
    // navigation dans le menu principal, et etat du dernier appel pour
    // detecter le "front montant" (une seule bascule par poussee du
    // stick, meme si l'axe reste en butee plusieurs frames).
    private const float NAV_THRESHOLD = 0.5f;
    private static bool navUpHeld;
    private static bool navDownHeld;

    /// <summary>
    /// Vrai une seule frame quand le stick gauche vient d'etre pousse vers
    /// le haut au-dela de NAV_THRESHOLD -- fait remonter la selection dans
    /// le menu principal (voir UIManager.UpdateMainMenuGamepadNavigation()).
    /// A appeler une seule fois par frame (typiquement depuis
    /// UIManager.Update(), uniquement pendant que le menu est ouvert) pour
    /// que la detection de front fonctionne correctement.
    /// </summary>
    public static bool NavigateUpPressed
    {
        get
        {
            bool held = Input.GetAxis("Vertical") >= NAV_THRESHOLD;
            bool justPressed = held && !navUpHeld;
            navUpHeld = held;
            return justPressed;
        }
    }

    /// <summary>
    /// Equivalent de NavigateUpPressed pour le bas (fait descendre la
    /// selection dans le menu principal).
    /// </summary>
    public static bool NavigateDownPressed
    {
        get
        {
            bool held = Input.GetAxis("Vertical") <= -NAV_THRESHOLD;
            bool justPressed = held && !navDownHeld;
            navDownHeld = held;
            return justPressed;
        }
    }
}
