using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine;

public class DynamicTextManager : MonoBehaviour
{
    public static DynamicTextData defaultData;
    public static GameObject canvasPrefab;
    public static Transform mainCamera;

    [SerializeField] private GameObject _canvasPrefab;
    [SerializeField] private Transform _mainCamera;
    [SerializeField] private int initialPoolSize = 20;

    private static Queue<GameObject> text3DPool = new ();

    private void Awake()
    {
        mainCamera = _mainCamera;
        canvasPrefab = _canvasPrefab;

        // Initialize pools
        InitializePool(text3DPool, initialPoolSize, typeof(DynamicText));
    }

    private void InitializePool(Queue<GameObject> pool, int size, System.Type componentType)
    {
        for (int i = 0; i < size; i++)
        {
            GameObject obj = Instantiate(canvasPrefab, Vector3.zero, Quaternion.identity);
            obj.SetActive(false);
            // Ensure the correct component is attached
            if (obj.GetComponent(componentType) == null)
            {
                obj.AddComponent(componentType);
            }
            pool.Enqueue(obj);
        }
    }

    public void CreateText(Vector3 position, string text, DynamicTextData data)
    {
        GameObject newText = GetPooledObject(text3DPool, typeof(DynamicText));
        newText.transform.position = position;
        newText.transform.rotation = Quaternion.identity;
        newText.GetComponent<DynamicText>().Initialise(text, data);
        newText.SetActive(true);

        DelayedReturn(newText, 0.6f).Forget();
    }

    private GameObject GetPooledObject(Queue<GameObject> pool, System.Type componentType)
    {
        while (pool.Count > 0)
        {
            GameObject obj = pool.Dequeue();
            if (obj != null)
            {
                return obj;
            }
        }

        GameObject newObj = Instantiate(canvasPrefab, Vector3.zero, Quaternion.identity);
        if (newObj.GetComponent(componentType) == null)
        {
            newObj.AddComponent(componentType);
        }
        return newObj;
    }

    public void ReturnToPool(GameObject obj)
    {
        obj.SetActive(false);
        text3DPool.Enqueue(obj);
    }
    private async UniTaskVoid DelayedReturn(GameObject obj, float delay)
    {
        await UniTask.Delay(System.TimeSpan.FromSeconds(delay), cancellationToken: this.GetCancellationTokenOnDestroy());
        ReturnToPool(obj);
    }
}
