using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Bullet : MonoBehaviour
{
    public HitType HitType => _hitType;
    public float Dame => _dame;
    private float _dame;
    private HitType _hitType;
    public void Init(float dame, HitType hitType)
    {
        _dame = dame;
        _hitType = hitType;
    }
}
