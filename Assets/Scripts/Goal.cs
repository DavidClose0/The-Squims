using UnityEngine;

public class Goal
{
    public string name;
    public float value;

    public Goal(string name, float value)
    {
        this.name = name;
        this.value = value;
    }

    // Return current discontentment
    public float GetDiscontentment()
    {
        return value * value;
    }

    // Return discontentment for new value
    public float CalculateDiscontentment(float newValue)
    {
        return newValue * newValue;
    }
}
