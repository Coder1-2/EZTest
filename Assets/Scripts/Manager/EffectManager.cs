using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine;

public class EffectManager : MonoBehaviour
{
    private static Queue<GameObject> effectPool = new ();

    private GameSettings _gameSettings;

    private void Awake()
    {
        // Initialize pool
        _gameSettings = Resources.Load<GameSettings>("GameSettings");

        InitializePool(effectPool, 20);
    }

    private void InitializePool(Queue<GameObject> pool, int size)
    {
        for (int i = 0; i < size; i++)
        {
            GameObject obj = Instantiate(_gameSettings.effectHit, Vector3.zero, Quaternion.identity);
            obj.SetActive(false);
            pool.Enqueue(obj);
        }
    }

    public void CreateEffectHit(Vector3 position)
    {
        GameObject newEffect = GetPooledObject(effectPool);
        newEffect.transform.position = position;
        newEffect.transform.rotation = Quaternion.identity;
        newEffect.SetActive(true);

        DelayedReturn(newEffect, 0.6f).Forget();
    }

    private GameObject GetPooledObject(Queue<GameObject> pool)
    {
        while (pool.Count > 0)
        {
            GameObject obj = pool.Dequeue();
            if (obj != null)
            {
                return obj;
            }
        }

        // If no inactive object is available, create a new one
        GameObject newObj = Instantiate(_gameSettings.effectHit, Vector3.zero, Quaternion.identity);
        return newObj;
    }

    public static void ReturnToPool(GameObject obj)
    {
        obj.SetActive(false);
        effectPool.Enqueue(obj);
    }
    private async UniTaskVoid DelayedReturn(GameObject obj, float delay)
    {
        await UniTask.Delay(System.TimeSpan.FromSeconds(delay), cancellationToken: this.GetCancellationTokenOnDestroy());
        ReturnToPool(obj);
    }
}