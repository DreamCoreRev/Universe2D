using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

public interface ICastable
{
    event Done CastDone;

    string MyTitle
    {
        get;
    }

    Sprite MyIcon
    {
        get;
    }

    float MyCastTime
    {
        get;
    }

    Color MyBarColor
    {
        get;
    }

    bool OnCooldown
    {
        get;
        set;
    }

    float MyCooldown
    {
        get;
        set;
    }

    void OnCastDone();
}
