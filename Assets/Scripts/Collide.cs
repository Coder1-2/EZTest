using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static UnityEngine.UI.GridLayoutGroup;

public class Collide : MonoBehaviour
{
    private CharacterData _characterData;
    public void Init(CharacterData characterData)
    {
        _characterData = characterData;
    }
    private void OnTriggerEnter(Collider other)
    {
        if (_characterData == null) return;
        var data = other.GetComponentInParent<CharacterData>();
        if (data == null || data == _characterData || data.Team == _characterData.Team) return;
        if (other.TryGetComponent<Bullet>(out var bullet))
        {
            _characterData.TakeDamage(bullet.Dame);
        }
    }
}
