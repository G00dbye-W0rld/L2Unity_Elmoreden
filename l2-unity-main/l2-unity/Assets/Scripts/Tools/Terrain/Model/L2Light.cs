#if (UNITY_EDITOR)
using UnityEngine;

[System.Serializable]
public class L2Light {
    public string name;
    public Vector3 position;
    public float brightness;
    public float radius;
    public int hue;
    public int saturation;

    public override string ToString() {
        return $"Name: {name}, position: {position}, brightness: {brightness}, " +
            $"radius: {radius}, hue: {hue}, saturation: {saturation}";
    }
}
#endif
