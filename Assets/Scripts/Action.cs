using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Action
{
    public string name;
    public float duration; // in seconds
    public Dictionary<Goal, float> effectsOnGoals;

    public Action(string name, float duration)
    {
        this.name = name;
        effectsOnGoals = new Dictionary<Goal, float>();
        this.duration = duration;
    }

    // Add effect values to goals
    public void Perform()
    {
        foreach (var effect in effectsOnGoals)
        {
            effect.Key.value += effect.Value;
        }

        Debug.Log("Performed " + name);
    }
}
