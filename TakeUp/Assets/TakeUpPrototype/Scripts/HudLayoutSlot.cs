using UnityEngine;

namespace TakeUp.Prototype
{
    // A stable identity independent of the editable object name and hierarchy order.
    [AddComponentMenu("")]
    public sealed class HudLayoutSlot : MonoBehaviour
    {
        [HideInInspector] public int index;
    }
}
