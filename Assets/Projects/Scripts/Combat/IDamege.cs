using UnityEngine;

public interface IDamage
{
    void TakeDamage(DamageInfo damage);
    void TakeDamage(Transform target,float damage);
}
