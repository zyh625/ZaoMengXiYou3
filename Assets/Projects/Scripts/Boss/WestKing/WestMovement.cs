using UnityEngine;

//不覆盖EnemyMovement的Awake/OnEnable/OnDisable，保留原有移动、受击及控制初始化。
public sealed class WestMovement : EnemyMovement { }
