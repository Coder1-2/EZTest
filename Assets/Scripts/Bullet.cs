using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Bullet : MonoBehaviour
{
    public float Dame => _dame;
    private float _dame;
    public void Init(float dame)
    {
        _dame = dame;
    }
}
