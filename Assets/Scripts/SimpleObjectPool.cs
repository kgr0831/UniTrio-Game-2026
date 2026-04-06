using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 범용 오브젝트 풀링 시스템.
/// 'Instantiate'와 'Destroy'로 인한 CPU 부하 및 가비지 컬렉션을 최소화합니다.
/// </summary>
public class SimpleObjectPool : MonoBehaviour
{
    private static SimpleObjectPool _instance;
    public static SimpleObjectPool Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<SimpleObjectPool>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("SimpleObjectPool");
                    _instance = go.AddComponent<SimpleObjectPool>();
                }
            }
            return _instance;
        }
    }

    private Dictionary<int, Queue<GameObject>> _pool = new Dictionary<int, Queue<GameObject>>();

    // 풀링된 오브젝트가 자신의 고향(Pool)을 기억하게 하는 도우미 컴포넌트
    private class PoolMember : MonoBehaviour
    {
        public int PrefabKey;
    }

    /// <summary>
    /// 풀에서 오브젝트를 가져오거나 없으면 새로 생성합니다.
    /// </summary>
    public GameObject Get(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        int key = prefab.GetInstanceID();

        if (!_pool.ContainsKey(key))
            _pool[key] = new Queue<GameObject>();

        GameObject obj;
        if (_pool[key].Count > 0)
        {
            obj = _pool[key].Dequeue();
            obj.transform.position = position;
            obj.transform.rotation = rotation;
            obj.SetActive(true);
        }
        else
        {
            obj = Instantiate(prefab, position, rotation);
            // 풀 멤버 정보를 부착하여 반환 시 키 값을 알 수 있게 합니다.
            PoolMember pm = obj.AddComponent<PoolMember>();
            pm.PrefabKey = key;
        }

        return obj;
    }

    /// <summary>
    /// 인스턴스만 넘기면 자동으로 올바른 풀로 반환합니다.
    /// </summary>
    public void Release(GameObject instance)
    {
        PoolMember pm = instance.GetComponent<PoolMember>();
        if (pm == null)
        {
            Debug.LogWarning($"[SimpleObjectPool] 풀링되지 않은 오브젝트({instance.name})를 반환하려고 시도했습니다. 파괴합니다.");
            Destroy(instance);
            return;
        }

        int key = pm.PrefabKey;
        if (!_pool.ContainsKey(key))
            _pool[key] = new Queue<GameObject>();

        instance.SetActive(false);
        _pool[key].Enqueue(instance);
    }

    /// <summary>
    /// (레거시 지원) 프리팹 키를 직접 전달하여 반환합니다.
    /// </summary>
    public void Release(GameObject prefab, GameObject instance)
    {
        Release(instance);
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);
    }
}
