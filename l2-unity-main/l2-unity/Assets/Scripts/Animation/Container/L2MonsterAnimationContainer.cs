using UnityEngine;

[CreateAssetMenu(fileName = "Monster", menuName = "L2/Animations/Monster")]
public class L2MonsterAnimationContainer : ScriptableObject
{
    [SerializeField]
    private L2MonsterAnimation[] _animations;

    public L2MonsterAnimation[] Animations { get => _animations; }
}