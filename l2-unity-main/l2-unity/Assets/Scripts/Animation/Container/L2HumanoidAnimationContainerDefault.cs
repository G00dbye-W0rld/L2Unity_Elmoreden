// Concrete type definitions with Awake methods
using UnityEngine;

[CreateAssetMenu(fileName = "Default", menuName = "L2/Animations/HumanoidDefault")]
public class L2HumanoidAnimationContainerDefault : L2HumanoidAnimationContainer<HumanoidAnimationDefaultEvent, L2HumanoidAnimation<HumanoidAnimationDefaultEvent>>
{
}