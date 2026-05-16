using UnityEngine;

public class HitStunTimer : MonoBehaviour
{
    private float _timer;
    private MonsterRuntimeData _runtime;
    private MonsterNavigator _nav;

    public void Refresh(float duration)
    {
        _timer = duration;

        if (_runtime == null) _runtime = GetComponent<MonsterRuntimeData>();
        if (_nav == null) _nav = GetComponent<MonsterNavigator>();

        if (_runtime != null) _runtime.IsStaggered = true;
        if (_nav != null) _nav.Stop();
    }

    private void Update()
    {
        if (_timer <= 0f) return;

        _timer -= Time.deltaTime;
        if (_timer <= 0f)
        {
            if (_runtime != null) _runtime.IsStaggered = false;
            Destroy(this);
        }
    }
}
