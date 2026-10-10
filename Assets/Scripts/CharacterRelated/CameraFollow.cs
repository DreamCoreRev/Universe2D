using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class CameraFollow : MonoBehaviour {

    /// <summary>
    /// The camera's target in our case it s the player
    /// </summary>
    private Transform target;

    /// <summary>
    /// The minimum and maximum value of the camera
    /// </summary>
    private float xMax, xMin, yMin, yMax;

    /// <summary>
    /// A reference to the ground tilemap
    /// </summary>
    [SerializeField]
    private Tilemap tilemap;

    /// <summary>
    /// A reference to the player
    /// </summary>
    private Player player;

    private bool initialized = false;

	// Use this for initialization
	void Start ()
    {
        // En solo, le Player tague "Player" existe deja (place a la main
        // dans la scene) -- on peut initialiser tout de suite comme avant.
        // En reseau, il n'existe pas encore a cet instant (Mirror le cree
        // juste apres la connexion), donc FindGameObjectWithTag renverrait
        // null et plantait ici -- dans ce cas on attend que Player.cs nous
        // appelle lui-meme (voir Initialize ci-dessous) des qu'il est pret.
        GameObject tagged = GameObject.FindGameObjectWithTag("Player");
        if (tagged != null)
        {
            Player taggedPlayer = tagged.GetComponent<Player>();
            if (taggedPlayer != null)
            {
                Initialize(taggedPlayer);
            }
        }
	}

    /// <summary>
    /// Branche la camera sur ce joueur et calcule les limites de la carte.
    /// Appele automatiquement en solo (voir Start ci-dessus), et par
    /// Player.cs des que notre joueur reseau est pret. Protege par
    /// "initialized" pour ne jamais faire le travail deux fois si les deux
    /// chemins finissent par se declencher (cas solo).
    /// </summary>
    public void Initialize(Player targetPlayer)
    {
        Debug.Log("[CameraFollow] Initialize() appele. initialized=" + initialized + " targetPlayer=" + (targetPlayer == null ? "null" : targetPlayer.name) + " tilemap=" + (tilemap == null ? "NULL" : "ok") + " this.enabled=" + enabled + " gameObject.activeInHierarchy=" + gameObject.activeInHierarchy);

        if (initialized || targetPlayer == null)
        {
            return;
        }

        initialized = true;

        player = targetPlayer;
        target = targetPlayer.transform;

        //Calculates the min and max postion
        Vector3 minTile = tilemap.CellToWorld(tilemap.cellBounds.min);
        Vector3 maxTile = tilemap.CellToWorld(tilemap.cellBounds.max);

        //Sets the limits of the camera
        SetLimits(minTile, maxTile);

        //Sets the limits of the player
        player.SetLimits(minTile, maxTile);

        Debug.Log("[CameraFollow] Initialize() termine. target=" + target.name + " position=" + target.position);
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        //Makes sure the camera doesn't go further than our world
        transform.position = new Vector3(Mathf.Clamp(target.position.x, xMin, xMax), Mathf.Clamp(target.position.y, yMin, yMax), -10);
    }

    /// <summary>
    /// Sets the cameras limits, this is used to make sure it can't go over the edge of the main tilemap
    /// </summary>
    /// <param name="minTile">The position of the minimum tile</param>
    /// <param name="maxTile">The position of the maximum tile</param>
    private void SetLimits(Vector3 minTile, Vector3 maxTile)
    {
        Camera cam = Camera.main;

        float height = 2f * cam.orthographicSize;
        float width = height * cam.aspect;

        xMin = minTile.x + width / 2;
        xMax = maxTile.x - width / 2;

        yMin = minTile.y + height / 2;
        yMax = maxTile.y - height / 2;
    }
}
