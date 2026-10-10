using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Range : MonoBehaviour
{

    private Enemy parent;

    private void Start()
    {
        parent = GetComponentInParent<Enemy>();
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        Debug.Log($"[DEBUG-RANGE] OnTriggerEnter2D on {parent?.name} with tag={collision.tag} ShouldRunAI={parent?.ShouldRunAI}");

        if (collision.tag == "Player")
        {
            parent.SetTarget(collision.GetComponent<Character>());
            Debug.Log($"[DEBUG-RANGE] SetTarget called, MyTarget now={parent.MyTarget}");
        }
    }
}
